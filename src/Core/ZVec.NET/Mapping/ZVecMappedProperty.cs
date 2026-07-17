using System.Globalization;
using System.Reflection;

namespace ZVec.NET.Mapping;

/// <summary>Metadata for one mapped property on a document type.</summary>
public sealed class ZVecMappedProperty
{
    internal ZVecMappedProperty(
        PropertyInfo property,
        ZVecPropertyKind kind,
        string storageName,
        ZVecDataType dataType,
        bool nullable,
        int dimension,
        ZVecIndexParam? indexParam)
    {
        Property = property;
        Kind = kind;
        StorageName = storageName;
        DataType = dataType;
        Nullable = nullable;
        Dimension = dimension;
        IndexParam = indexParam;
    }

    /// <summary>CLR property.</summary>
    public PropertyInfo Property { get; }

    /// <summary>Mapping role.</summary>
    public ZVecPropertyKind Kind { get; }

    /// <summary>Native storage / schema name.</summary>
    public string StorageName { get; }

    /// <summary>ZVec data type.</summary>
    public ZVecDataType DataType { get; }

    /// <summary>Whether the scalar field is nullable.</summary>
    public bool Nullable { get; }

    /// <summary>Vector dimension (0 for non-vectors).</summary>
    public int Dimension { get; }

    /// <summary>Optional index parameters (vectors or invert indexes).</summary>
    public ZVecIndexParam? IndexParam { get; }

    /// <summary>Builds a scalar field schema for this property.</summary>
    public ZVecFieldSchema ToFieldSchema()
    {
        if (Kind != ZVecPropertyKind.Field)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    ZVecDefaults.Errors.MappingAddColumnScalarsOnly,
                    Property.DeclaringType?.Name ?? "?",
                    Property.Name));
        }

        return new ZVecFieldSchema
        {
            Name = StorageName,
            DataType = DataType,
            Nullable = Nullable,
            IndexParam = IndexParam as ZVecInvertIndexParam
        };
    }

    /// <summary>Builds a vector schema for this property.</summary>
    public ZVecVectorSchema ToVectorSchema()
    {
        if (Kind != ZVecPropertyKind.Vector)
            throw new InvalidOperationException($"Property '{Property.Name}' is not a vector.");

        return new ZVecVectorSchema
        {
            Name = StorageName,
            DataType = DataType,
            Dimension = Dimension,
            IndexParam = IndexParam
        };
    }

    internal object? GetValue(object instance) => Property.GetValue(instance);

    internal void SetValue(object instance, object? value) => Property.SetValue(instance, value);
}
