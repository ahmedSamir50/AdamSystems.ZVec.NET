using System.Globalization;
using ZVec.NET.Exceptions;

namespace ZVec.NET.Mapping;

/// <summary>
/// Maps between typed POCOs and <see cref="ZVecDoc"/>. Part of the shared ODM engine (reuse seam for future adapters).
/// </summary>
public static class ZVecMapper
{
    /// <summary>Converts a typed record to a <see cref="ZVecDoc"/>.</summary>
    public static ZVecDoc ToDoc<T>(T record) where T : class
    {
        ArgumentNullException.ThrowIfNull(record);
        var model = ZVecTypeModel.Get<T>();
        return ToDoc(record, model);
    }

    /// <summary>Converts a typed record using a precomputed model.</summary>
    public static ZVecDoc ToDoc<T>(T record, ZVecTypeModel model) where T : class
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(model);

        var idObj = model.Id.GetValue(record);
        var id = idObj as string;
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Document Id must be a non-empty string.", nameof(record));

        var fields = new Dictionary<string, object>(model.Fields.Count);
        foreach (var field in model.Fields)
        {
            var value = field.GetValue(record);
            if (value is null)
            {
                if (!field.Nullable)
                {
                    throw new ZVecSchemaMismatchException(
                        model.ClrType.Name,
                        $"Non-nullable field '{field.StorageName}' is null.");
                }

                continue;
            }

            fields[field.StorageName] = value;
        }

        var dense = new Dictionary<string, ReadOnlyMemory<float>>(model.Vectors.Count);
        var sparse = new Dictionary<string, IReadOnlyDictionary<int, float>>();

        foreach (var vector in model.Vectors)
        {
            var value = vector.GetValue(record);
            if (value is null)
                continue;

            if (value is ReadOnlyMemory<float> rom)
            {
                dense[vector.StorageName] = rom;
            }
            else if (value is float[] arr)
            {
                dense[vector.StorageName] = arr;
            }
            else if (value is IReadOnlyDictionary<int, float> sparseDict)
            {
                sparse[vector.StorageName] = sparseDict;
            }
            else if (value is IDictionary<int, float> dict)
            {
                sparse[vector.StorageName] = new Dictionary<int, float>(dict);
            }
            else
            {
                throw new ZVecException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        ZVecDefaults.Errors.MappingUnsupportedFieldType,
                        model.ClrType.Name,
                        vector.Property.Name,
                        value.GetType().Name));
            }
        }

        return ZVecDoc.Create(id, dense, sparse.Count == 0 ? null : sparse, fields);
    }

    /// <summary>Converts a <see cref="ZVecDoc"/> to a typed record. Unknown native fields are ignored.</summary>
    public static T FromDoc<T>(ZVecDoc doc) where T : class
    {
        ArgumentNullException.ThrowIfNull(doc);
        var model = ZVecTypeModel.Get<T>();
        return FromDoc<T>(doc, model);
    }

    /// <summary>Converts a <see cref="ZVecDoc"/> using a precomputed model.</summary>
    public static T FromDoc<T>(ZVecDoc doc, ZVecTypeModel model) where T : class
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(model);

        var instance = Activator.CreateInstance<T>()
            ?? throw new InvalidOperationException($"Type '{typeof(T).Name}' must have a public parameterless constructor.");

        model.Id.SetValue(instance, doc.Id);

        foreach (var field in model.Fields)
        {
            if (!doc.Fields.TryGetValue(field.StorageName, out var value) || value is null)
                continue;

            SetConverted(field, instance, value);
        }

        foreach (var vector in model.Vectors)
        {
            if (doc.DenseVectors.TryGetValue(vector.StorageName, out var dense))
            {
                if (vector.Property.PropertyType == typeof(float[]))
                    vector.SetValue(instance, dense.ToArray());
                else
                    vector.SetValue(instance, dense);
                continue;
            }

            if (doc.SparseVectors.TryGetValue(vector.StorageName, out var sparse))
                vector.SetValue(instance, sparse);
        }

        return instance;
    }

    /// <summary>
    /// Validates that every mapped scalar/vector on <typeparamref name="T"/> exists on <paramref name="schema"/>.
    /// Unknown native columns are allowed (leftover after Drop from a previous type shape).
    /// </summary>
    public static void EnsureModelMatchesSchema<T>(ZVecCollectionSchema? schema) where T : class
    {
        var model = ZVecTypeModel.Get<T>();
        EnsureModelMatchesSchema(model, schema);
    }

    /// <summary>Validates model against an open collection schema.</summary>
    public static void EnsureModelMatchesSchema(ZVecTypeModel model, ZVecCollectionSchema? schema)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (schema is null)
            return;

        var fieldNames = schema.Fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        var vectorNames = schema.Vectors.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var field in model.Fields)
        {
            if (!fieldNames.Contains(field.StorageName))
            {
                throw new ZVecSchemaMismatchException(
                    model.ClrType.Name,
                    $"missing field '{field.StorageName}'. Call AddColumn/EnsureSchemaAsync.");
            }
        }

        foreach (var vector in model.Vectors)
        {
            if (!vectorNames.Contains(vector.StorageName))
            {
                throw new ZVecSchemaMismatchException(
                    model.ClrType.Name,
                    $"missing vector '{vector.StorageName}'.");
            }
        }
    }

    private static void SetConverted(ZVecMappedProperty field, object instance, object value)
    {
        var targetType = Nullable.GetUnderlyingType(field.Property.PropertyType) ?? field.Property.PropertyType;
        if (targetType.IsInstanceOfType(value))
        {
            field.SetValue(instance, value);
            return;
        }

        try
        {
            var converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            field.SetValue(instance, converted);
        }
        catch (Exception ex)
        {
            throw new ZVecException(
                $"Cannot convert field '{field.StorageName}' value to {targetType.Name}.",
                ex);
        }
    }
}
