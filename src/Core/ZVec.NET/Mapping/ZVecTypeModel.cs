using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using ZVec.NET.Exceptions;

namespace ZVec.NET.Mapping;

/// <summary>
/// Cached mapping metadata for a document CLR type (identity, fields, vectors, collection name).
/// Shared engine used by schema generation, <see cref="ZVecMapper"/>, and expression filters.
/// </summary>
public sealed class ZVecTypeModel
{
    private static readonly ConcurrentDictionary<Type, ZVecTypeModel> Cache = new();

    private readonly Dictionary<string, ZVecMappedProperty> _byPropertyName;
    private readonly Dictionary<string, ZVecMappedProperty> _byStorageName;

    private ZVecTypeModel(
        Type clrType,
        string collectionName,
        ZVecMappedProperty id,
        IReadOnlyList<ZVecMappedProperty> fields,
        IReadOnlyList<ZVecMappedProperty> vectors)
    {
        ClrType = clrType;
        CollectionName = collectionName;
        Id = id;
        Fields = fields;
        Vectors = vectors;

        var all = new List<ZVecMappedProperty>(1 + fields.Count + vectors.Count) { id };
        all.AddRange(fields);
        all.AddRange(vectors);

        Properties = all;
        _byPropertyName = all.ToDictionary(p => p.Property.Name, StringComparer.Ordinal);
        _byStorageName = all.ToDictionary(p => p.StorageName, StringComparer.Ordinal);
    }

    /// <summary>Mapped CLR type.</summary>
    public Type ClrType { get; }

    /// <summary>Collection name for schema creation.</summary>
    public string CollectionName { get; }

    /// <summary>Identity property.</summary>
    public ZVecMappedProperty Id { get; }

    /// <summary>Scalar field properties.</summary>
    public IReadOnlyList<ZVecMappedProperty> Fields { get; }

    /// <summary>Vector properties.</summary>
    public IReadOnlyList<ZVecMappedProperty> Vectors { get; }

    /// <summary>All mapped properties (id + fields + vectors).</summary>
    public IReadOnlyList<ZVecMappedProperty> Properties { get; }

    /// <summary>Gets or builds the cached model for <typeparamref name="T"/>.</summary>
    public static ZVecTypeModel Get<T>() where T : class => Get(typeof(T));

    /// <summary>Gets or builds the cached model for <paramref name="clrType"/>.</summary>
    public static ZVecTypeModel Get(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        if (!clrType.IsClass || clrType.IsAbstract)
            throw new ArgumentException($"Type '{clrType.Name}' must be a concrete class.", nameof(clrType));

        return Cache.GetOrAdd(clrType, Build);
    }

    /// <summary>Looks up a mapped property by CLR property name.</summary>
    public bool TryGetByPropertyName(string propertyName, out ZVecMappedProperty? property)
        => _byPropertyName.TryGetValue(propertyName, out property);

    /// <summary>Looks up a mapped property by native storage name.</summary>
    public bool TryGetByStorageName(string storageName, out ZVecMappedProperty? property)
        => _byStorageName.TryGetValue(storageName, out property);

    /// <summary>Resolves a mapped property or throws.</summary>
    public ZVecMappedProperty GetRequiredByPropertyName(string propertyName)
    {
        if (_byPropertyName.TryGetValue(propertyName, out var property))
            return property;

        throw new ZVecException(
            string.Format(
                CultureInfo.InvariantCulture,
                ZVecDefaults.Errors.MappingUnknownProperty,
                ClrType.Name,
                propertyName));
    }

    private static ZVecTypeModel Build(Type clrType)
    {
        var collectionAttr = clrType.GetCustomAttribute<ZVecCollectionAttribute>();
        var collectionName = collectionAttr?.Name ?? clrType.Name;

        var props = clrType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        ZVecMappedProperty? id = null;
        var fields = new List<ZVecMappedProperty>();
        var vectors = new List<ZVecMappedProperty>();

        foreach (var prop in props)
        {
            if (prop.GetCustomAttribute<ZVecIgnoreAttribute>() is not null)
                continue;

            var idAttr = prop.GetCustomAttribute<ZVecIdAttribute>();
            var vectorAttr = prop.GetCustomAttribute<ZVecVectorAttribute>();
            var fieldAttr = prop.GetCustomAttribute<ZVecFieldAttribute>();

            var isIdConvention = idAttr is null
                && vectorAttr is null
                && fieldAttr is null
                && (prop.Name is "Id" or "ID")
                && prop.PropertyType == typeof(string);

            if (idAttr is not null || isIdConvention)
            {
                if (prop.PropertyType != typeof(string))
                {
                    throw new ZVecException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            ZVecDefaults.Errors.MappingUnsupportedFieldType,
                            clrType.Name,
                            prop.Name,
                            prop.PropertyType.Name));
                }

                if (id is not null)
                {
                    throw new ZVecException(
                        string.Format(CultureInfo.InvariantCulture, ZVecDefaults.Errors.MappingDuplicateId, clrType.Name));
                }

                id = new ZVecMappedProperty(
                    prop,
                    ZVecPropertyKind.Id,
                    prop.Name,
                    ZVecDataType.String,
                    nullable: false,
                    dimension: 0,
                    indexParam: null);
                continue;
            }

            if (vectorAttr is not null || ZVecClrTypeMap.IsDenseVectorMemory(prop.PropertyType) || ZVecClrTypeMap.IsSparseVectorDictionary(prop.PropertyType))
            {
                if (vectorAttr is null)
                {
                    throw new ZVecException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            ZVecDefaults.Errors.MappingVectorDimensionRequired,
                            clrType.Name,
                            prop.Name));
                }

                var storageName = vectorAttr.Name ?? prop.Name;
                var indexParam = CreateVectorIndexParam(vectorAttr);
                vectors.Add(new ZVecMappedProperty(
                    prop,
                    ZVecPropertyKind.Vector,
                    storageName,
                    vectorAttr.DataType,
                    nullable: false,
                    dimension: vectorAttr.Dimension,
                    indexParam: indexParam));
                continue;
            }

            // Scalar field by convention or explicit attribute
            if (!prop.CanWrite && fieldAttr is null)
                continue;

            if (!ZVecClrTypeMap.TryGetScalarDataType(prop.PropertyType, out var dataType))
            {
                if (fieldAttr is not null)
                {
                    throw new ZVecException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            ZVecDefaults.Errors.MappingUnsupportedFieldType,
                            clrType.Name,
                            prop.Name,
                            prop.PropertyType.Name));
                }

                continue;
            }

            var fieldName = fieldAttr?.Name ?? prop.Name;
            var nullable = fieldAttr?.Nullable
                ?? (Nullable.GetUnderlyingType(prop.PropertyType) is not null || prop.PropertyType == typeof(string));

            fields.Add(new ZVecMappedProperty(
                prop,
                ZVecPropertyKind.Field,
                fieldName,
                dataType,
                nullable,
                dimension: 0,
                indexParam: null));
        }

        if (id is null)
        {
            throw new ZVecException(
                string.Format(CultureInfo.InvariantCulture, ZVecDefaults.Errors.MappingIdRequired, clrType.Name));
        }

        return new ZVecTypeModel(clrType, collectionName, id, fields, vectors);
    }

    private static ZVecIndexParam CreateVectorIndexParam(ZVecVectorAttribute attr) => attr.Index switch
    {
        ZVecIndexType.Flat => new ZVecFlatIndexParam { MetricType = attr.Metric },
        ZVecIndexType.Ivf => new ZVecIvfIndexParam { MetricType = attr.Metric },
        ZVecIndexType.HnswRabitq => new ZVecHnswRabitqIndexParam { MetricType = attr.Metric, M = attr.M, EfConstruction = attr.EfConstruction },
        ZVecIndexType.DiskAnn => new ZVecDiskAnnIndexParam { MetricType = attr.Metric },
        ZVecIndexType.Vamana => new ZVecVamanaIndexParam { MetricType = attr.Metric },
        _ => new ZVecHnswIndexParam
        {
            MetricType = attr.Metric,
            M = attr.M,
            EfConstruction = attr.EfConstruction
        }
    };
}
