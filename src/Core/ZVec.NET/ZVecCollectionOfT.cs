using System.Globalization;
using System.Linq.Expressions;
using ZVec.NET.Exceptions;
using ZVec.NET.Mapping;

namespace ZVec.NET;

/// <summary>
/// Typed collection façade over <see cref="IZvecCollection"/> for document type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Mapped document class.</typeparam>
public sealed class ZVecCollection<T> : IZvecCollection<T> where T : class
{
    private readonly ZVecTypeModel _model;

    /// <summary>Wraps an existing untyped collection.</summary>
    public ZVecCollection(IZvecCollection inner)
    {
        Untyped = inner ?? throw new ArgumentNullException(nameof(inner));
        _model = ZVecTypeModel.Get<T>();
    }

    /// <inheritdoc/>
    public IZvecCollection Untyped { get; }

    /// <inheritdoc/>
    public string Path => Untyped.Path;

    /// <inheritdoc/>
    public ZVecCollectionSchema? Schema => Untyped.Schema;

    /// <inheritdoc/>
    public ZVecCollectionOptions Options => Untyped.Options;

    /// <inheritdoc/>
    public ZVecCollectionStats Stats => Untyped.Stats;

    /// <inheritdoc/>
    public void Dispose() => Untyped.Dispose();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => Untyped.DisposeAsync();

    /// <inheritdoc/>
    public void Destroy() => Untyped.Destroy();

    /// <inheritdoc/>
    public ValueTask DestroyAsync(CancellationToken ct = default) => Untyped.DestroyAsync(ct);

    /// <inheritdoc/>
    public ZVecStatus Insert(T record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ZVecMapper.EnsureModelMatchesSchema(_model, Untyped.Schema);
        return Untyped.Insert(ZVecMapper.ToDoc(record, _model));
    }

    /// <inheritdoc/>
    public ZVecStatus Insert(IReadOnlyList<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        ZVecMapper.EnsureModelMatchesSchema(_model, Untyped.Schema);
        var docs = new ZVecDoc[records.Count];
        for (var i = 0; i < records.Count; i++)
            docs[i] = ZVecMapper.ToDoc(records[i], _model);
        return Untyped.Insert(docs);
    }

    /// <inheritdoc/>
    public async ValueTask<ZVecStatus> InsertAsync(T record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ZVecMapper.EnsureModelMatchesSchema(_model, Untyped.Schema);
        return await Untyped.InsertAsync(ZVecMapper.ToDoc(record, _model), ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ZVecStatus> InsertAsync(IReadOnlyList<T> records, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        ZVecMapper.EnsureModelMatchesSchema(_model, Untyped.Schema);
        var docs = new ZVecDoc[records.Count];
        for (var i = 0; i < records.Count; i++)
            docs[i] = ZVecMapper.ToDoc(records[i], _model);
        var results = await Untyped.InsertAsync(docs, ct).ConfigureAwait(false);
        foreach (var result in results)
        {
            if (!result.IsSuccess)
                return new ZVecStatus { Code = result.Code, Message = result.Message };
        }

        return new ZVecStatus { Code = ZVecErrorCode.Ok };
    }

    /// <inheritdoc/>
    public ZVecStatus Update(T record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ZVecMapper.EnsureModelMatchesSchema(_model, Untyped.Schema);
        return Untyped.Update(ZVecMapper.ToDoc(record, _model));
    }

    /// <inheritdoc/>
    public async ValueTask<ZVecStatus> UpdateAsync(T record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ZVecMapper.EnsureModelMatchesSchema(_model, Untyped.Schema);
        return await Untyped.UpdateAsync(ZVecMapper.ToDoc(record, _model), ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ZVecStatus Upsert(T record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ZVecMapper.EnsureModelMatchesSchema(_model, Untyped.Schema);
        return Untyped.Upsert(ZVecMapper.ToDoc(record, _model));
    }

    /// <inheritdoc/>
    public async ValueTask<ZVecStatus> UpsertAsync(T record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ZVecMapper.EnsureModelMatchesSchema(_model, Untyped.Schema);
        return await Untyped.UpsertAsync(ZVecMapper.ToDoc(record, _model), ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ZVecStatus Delete(string id) => Untyped.Delete(id);

    /// <inheritdoc/>
    public ZVecStatus DeleteByFilter(Expression<Func<T, bool>> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return Untyped.DeleteByFilter(ZVecExpressionFilter.Translate(_model, filter));
    }

    /// <inheritdoc/>
    public ValueTask<ZVecStatus> DeleteAsync(string id, CancellationToken ct = default)
        => Untyped.DeleteAsync(id, ct);

    /// <inheritdoc/>
    public ValueTask<ZVecStatus> DeleteByFilterAsync(Expression<Func<T, bool>> filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return Untyped.DeleteByFilterAsync(ZVecExpressionFilter.Translate(_model, filter), ct);
    }

    /// <inheritdoc/>
    public T? Fetch(string id, bool includeVector = false)
    {
        var doc = Untyped.Fetch(id, includeVector);
        return doc is null ? null : ZVecMapper.FromDoc<T>(doc, _model);
    }

    /// <inheritdoc/>
    public async ValueTask<T?> FetchAsync(string id, bool includeVector = false, CancellationToken ct = default)
    {
        var doc = await Untyped.FetchAsync(id, includeVector, ct).ConfigureAwait(false);
        return doc is null ? null : ZVecMapper.FromDoc<T>(doc, _model);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ZVecHit<T>> Query(
        Expression<Func<T, ReadOnlyMemory<float>>> vectorProperty,
        ReadOnlyMemory<float> queryVector,
        int topK = 10,
        Expression<Func<T, bool>>? filter = null,
        bool includeVector = true)
    {
        var storageName = ResolveDenseVectorStorageName(vectorProperty);
        var filterString = filter is null ? null : ZVecExpressionFilter.Translate(_model, filter);
        var docs = Untyped.Query(
            new ZVecQuery { FieldName = storageName, Vector = queryVector },
            topK,
            filterString,
            includeVector);
        return MapHits(docs);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ZVecHit<T>>> QueryAsync(
        Expression<Func<T, ReadOnlyMemory<float>>> vectorProperty,
        ReadOnlyMemory<float> queryVector,
        int topK = 10,
        Expression<Func<T, bool>>? filter = null,
        bool includeVector = true,
        CancellationToken ct = default)
    {
        var storageName = ResolveDenseVectorStorageName(vectorProperty);
        var filterString = filter is null ? null : ZVecExpressionFilter.Translate(_model, filter);
        var docs = await Untyped.QueryAsync(
            new ZVecQuery { FieldName = storageName, Vector = queryVector },
            topK,
            filterString,
            includeVector,
            ct).ConfigureAwait(false);
        return MapHits(docs);
    }

    /// <inheritdoc/>
    public void AddColumn<TProp>(Expression<Func<T, TProp>> property, string? defaultExpression = null)
    {
        var mapped = ZVecMemberPath.ResolveProperty(_model, property);
        if (mapped.Kind != ZVecPropertyKind.Field)
        {
            throw new ZVecException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    ZVecDefaults.Errors.MappingAddColumnScalarsOnly,
                    _model.ClrType.Name,
                    mapped.Property.Name));
        }

        Untyped.AddColumn(ToAddableFieldSchema(mapped), defaultExpression ?? DefaultExpressionFor(mapped.DataType));
    }

    /// <inheritdoc/>
    public async ValueTask AddColumnAsync<TProp>(
        Expression<Func<T, TProp>> property,
        string? defaultExpression = null,
        CancellationToken ct = default)
    {
        var mapped = ZVecMemberPath.ResolveProperty(_model, property);
        if (mapped.Kind != ZVecPropertyKind.Field)
        {
            throw new ZVecException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    ZVecDefaults.Errors.MappingAddColumnScalarsOnly,
                    _model.ClrType.Name,
                    mapped.Property.Name));
        }

        await Untyped.AddColumnAsync(
            ToAddableFieldSchema(mapped),
            defaultExpression ?? DefaultExpressionFor(mapped.DataType),
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void DropColumn<TProp>(Expression<Func<T, TProp>> property)
    {
        var mapped = ZVecMemberPath.ResolveProperty(_model, property);
        Untyped.DropColumn(mapped.StorageName);
    }

    /// <inheritdoc/>
    public ValueTask DropColumnAsync<TProp>(Expression<Func<T, TProp>> property, CancellationToken ct = default)
    {
        var mapped = ZVecMemberPath.ResolveProperty(_model, property);
        return Untyped.DropColumnAsync(mapped.StorageName, ct);
    }

    /// <inheritdoc/>
    public void AlterColumn<TProp>(
        Expression<Func<T, TProp>> property,
        string? newName = null,
        ZVecFieldSchema? newSchema = null)
    {
        var mapped = ZVecMemberPath.ResolveProperty(_model, property);
        Untyped.AlterColumn(mapped.StorageName, newName, newSchema);
    }

    /// <inheritdoc/>
    public ValueTask AlterColumnAsync<TProp>(
        Expression<Func<T, TProp>> property,
        string? newName = null,
        ZVecFieldSchema? newSchema = null,
        CancellationToken ct = default)
    {
        var mapped = ZVecMemberPath.ResolveProperty(_model, property);
        return Untyped.AlterColumnAsync(mapped.StorageName, newName, newSchema, ct);
    }

    /// <inheritdoc/>
    public void CreateIndex<TProp>(Expression<Func<T, TProp>> property, ZVecIndexParam indexParam)
    {
        var mapped = ZVecMemberPath.ResolveProperty(_model, property);
        Untyped.CreateIndex(mapped.StorageName, indexParam);
    }

    /// <inheritdoc/>
    public ValueTask CreateIndexAsync<TProp>(
        Expression<Func<T, TProp>> property,
        ZVecIndexParam indexParam,
        CancellationToken ct = default)
    {
        var mapped = ZVecMemberPath.ResolveProperty(_model, property);
        return Untyped.CreateIndexAsync(mapped.StorageName, indexParam, ct);
    }

    /// <inheritdoc/>
    public void DropIndex<TProp>(Expression<Func<T, TProp>> property)
    {
        var mapped = ZVecMemberPath.ResolveProperty(_model, property);
        Untyped.DropIndex(mapped.StorageName);
    }

    /// <inheritdoc/>
    public ValueTask DropIndexAsync<TProp>(Expression<Func<T, TProp>> property, CancellationToken ct = default)
    {
        var mapped = ZVecMemberPath.ResolveProperty(_model, property);
        return Untyped.DropIndexAsync(mapped.StorageName, ct);
    }

    /// <inheritdoc/>
    public void EnsureSchema()
    {
        var schema = Untyped.Schema
            ?? throw new ZVecSchemaMismatchException(
                _model.ClrType.Name,
                "collection was opened without an in-memory schema; cannot EnsureSchema.");

        var fieldNames = schema.Fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        var vectorNames = schema.Vectors.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var vector in _model.Vectors)
        {
            if (!vectorNames.Contains(vector.StorageName))
            {
                throw new ZVecException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        ZVecDefaults.Errors.MappingEnsureSchemaCannotAddVector,
                        _model.ClrType.Name,
                        vector.StorageName));
            }
        }

        foreach (var field in _model.Fields)
        {
            if (!fieldNames.Contains(field.StorageName))
            {
                // Native add_column requires nullable columns with a default expression.
                Untyped.AddColumn(ToAddableFieldSchema(field), DefaultExpressionFor(field.DataType));
            }
        }
    }

    /// <summary>
    /// Native DDL only allows adding nullable scalar columns; force nullable for add paths.
    /// </summary>
    private static ZVecFieldSchema ToAddableFieldSchema(ZVecMappedProperty field)
        => new()
        {
            Name = field.StorageName,
            DataType = field.DataType,
            Nullable = true,
            IndexParam = field.IndexParam as ZVecInvertIndexParam
        };

    private static string DefaultExpressionFor(ZVecDataType dataType) => dataType switch
    {
        ZVecDataType.Bool => "false",
        ZVecDataType.Int32 or ZVecDataType.Int64 or ZVecDataType.UInt32 or ZVecDataType.UInt64 => "0",
        ZVecDataType.Float or ZVecDataType.Double => "0.0",
        _ => "0"
    };

    /// <inheritdoc/>
    public ValueTask EnsureSchemaAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureSchema();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Optimize() => Untyped.Optimize();

    /// <inheritdoc/>
    public ValueTask OptimizeAsync(CancellationToken ct = default) => Untyped.OptimizeAsync(ct);

    private string ResolveDenseVectorStorageName(Expression<Func<T, ReadOnlyMemory<float>>> vectorProperty)
    {
        var mapped = ZVecMemberPath.ResolveProperty(_model, vectorProperty);
        if (mapped.Kind != ZVecPropertyKind.Vector)
        {
            throw new ZVecException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    ZVecDefaults.Errors.MappingUnknownProperty,
                    _model.ClrType.Name,
                    mapped.Property.Name));
        }

        return mapped.StorageName;
    }

    private IReadOnlyList<ZVecHit<T>> MapHits(IReadOnlyList<ZVecDoc> docs)
    {
        var hits = new ZVecHit<T>[docs.Count];
        for (var i = 0; i < docs.Count; i++)
        {
            hits[i] = new ZVecHit<T>
            {
                Record = ZVecMapper.FromDoc<T>(docs[i], _model),
                Score = docs[i].Score
            };
        }

        return hits;
    }
}
