using ZVec.NET.Internal;
using ZVec.NET.Query;

namespace ZVec.NET;

/// <summary>
/// Managed wrapper around a native ZVec collection handle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dispose vs Destroy semantics:</b><br/>
/// <see cref="Dispose"/>/<see cref="DisposeAsync"/> → <c>zvec_collection_close</c> (data is preserved).<br/>
/// <see cref="Destroy"/>/<see cref="DestroyAsync"/> → <c>zvec_collection_destroy</c> then close (data is deleted).
/// Calling <see cref="Destroy"/> after <see cref="Dispose"/> throws <see cref="ObjectDisposedException"/>.
/// </para>
/// <para>
/// <b>Thread safety:</b> Idempotency for disposal paths uses <see cref="Interlocked"/> flags.
/// The native handle is owned by a <see cref="Interop.SafeZvecHandle"/> (close-only on finalizer/Dispose).
/// </para>
/// <para>
/// <b>Async APIs:</b> <c>*Async</c> methods are cancellation-aware wrappers around synchronous native calls
/// (they complete on the caller thread; they are not thread-pool offloads).
/// </para>
/// <para>
/// <b>DocumentId queries:</b> Resolving <see cref="ZVecQuery.DocumentId"/> performs an extra Fetch
/// (with vectors) before the search — expect an additional native round-trip.
/// </para>
/// </remarks>
public sealed class ZVecCollection : IZvecCollection
{
    private readonly CollectionNativeContext _ctx;
    private readonly CollectionWriteOps _writes;
    private readonly CollectionReadOps _reads;
    private readonly CollectionDdlOps _ddl;

    /// <inheritdoc/>
    public string Path { get; }

    /// <inheritdoc/>
    public ZVecCollectionSchema? Schema => _ctx.Schema;

    /// <inheritdoc/>
    public ZVecCollectionOptions Options => _ctx.Options;

    /// <inheritdoc/>
    public ZVecCollectionStats Stats => GetStats();

    internal ZVecCollection(
        nint handle,
        string path,
        ZVecCollectionSchema? schema,
        CancellationToken factoryShutdownToken,
        ZVecFactory factory,
        ZVecCollectionOptions? options = null)
    {
        Path = path;
        _ctx = new CollectionNativeContext(
            handle,
            factory,
            factoryShutdownToken,
            options ?? new ZVecCollectionOptions(),
            schema);
        _writes = new CollectionWriteOps(_ctx);
        _reads = new CollectionReadOps(_ctx);
        _ddl = new CollectionDdlOps(_ctx);
    }

    /// <summary>
    /// Closes the collection. Idempotent — safe to call multiple times and
    /// concurrently with <see cref="DisposeAsync"/>.
    /// </summary>
    public void Dispose() => _ctx.Dispose();

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Destroy() => _ctx.Destroy();

    /// <inheritdoc/>
    public ValueTask DestroyAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Destroy();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ZVecCollectionStats GetStats() => _reads.GetStats();

    public ZVecStatus Insert(ZVecDoc doc) => _writes.Insert(doc);
    public ZVecStatus Insert(ReadOnlySpan<ZVecDoc> docs) => _writes.Insert(docs);
    public IReadOnlyList<ZVecWriteResult> InsertWithResults(ReadOnlySpan<ZVecDoc> docs) => _writes.InsertWithResults(docs);

    public ValueTask<ZVecStatus> InsertAsync(ZVecDoc doc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Insert(doc));
    }

    public ValueTask<IReadOnlyList<ZVecWriteResult>> InsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(docs);
        if (docs is ZVecDoc[] arr) return ValueTask.FromResult(InsertWithResults(arr));
        return ValueTask.FromResult(InsertWithResults(docs.ToArray()));
    }

    public ZVecStatus Update(ZVecDoc doc) => _writes.Update(doc);
    public ZVecStatus Update(ReadOnlySpan<ZVecDoc> docs) => _writes.Update(docs);
    public IReadOnlyList<ZVecWriteResult> UpdateWithResults(ReadOnlySpan<ZVecDoc> docs) => _writes.UpdateWithResults(docs);

    public ValueTask<ZVecStatus> UpdateAsync(ZVecDoc doc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Update(doc));
    }

    public ValueTask<ZVecStatus> UpdateAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(docs);
        if (docs is ZVecDoc[] arr) return ValueTask.FromResult(Update(arr));
        return ValueTask.FromResult(Update(docs.ToArray()));
    }

    public ZVecStatus Upsert(ZVecDoc doc) => _writes.Upsert(doc);
    public ZVecStatus Upsert(ReadOnlySpan<ZVecDoc> docs) => _writes.Upsert(docs);
    public IReadOnlyList<ZVecWriteResult> UpsertWithResults(ReadOnlySpan<ZVecDoc> docs) => _writes.UpsertWithResults(docs);

    public ValueTask<ZVecStatus> UpsertAsync(ZVecDoc doc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Upsert(doc));
    }

    public ValueTask<ZVecStatus> UpsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(docs);
        if (docs is ZVecDoc[] arr) return ValueTask.FromResult(Upsert(arr));
        return ValueTask.FromResult(Upsert(docs.ToArray()));
    }

    public ZVecStatus Delete(string pk) => _writes.Delete(pk);
    public ZVecStatus Delete(ReadOnlySpan<string> pks) => _writes.Delete(pks);
    public IReadOnlyList<ZVecWriteResult> DeleteWithResults(ReadOnlySpan<string> pks) => _writes.DeleteWithResults(pks);
    public ZVecStatus DeleteByFilter(string filter) => _writes.DeleteByFilter(filter);

    public ValueTask<ZVecStatus> DeleteAsync(string pk, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Delete(pk));
    }

    public ValueTask<ZVecStatus> DeleteAsync(IReadOnlyList<string> pks, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pks);
        if (pks is string[] arr) return ValueTask.FromResult(Delete(arr));
        return ValueTask.FromResult(Delete(pks.ToArray()));
    }

    public ValueTask<ZVecStatus> DeleteByFilterAsync(string filter, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(DeleteByFilter(filter));
    }

    public ZVecDoc? Fetch(string pk, bool includeVector = false) => _reads.Fetch(pk, includeVector);
    public IReadOnlyList<ZVecDoc> Fetch(ReadOnlySpan<string> pks, bool includeVector = false) => _reads.Fetch(pks, includeVector);

    public ValueTask<ZVecDoc?> FetchAsync(string pk, bool includeVector = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Fetch(pk, includeVector));
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> FetchAsync(IReadOnlyList<string> pks, bool includeVector = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pks);
        if (pks is string[] arr) return ValueTask.FromResult(Fetch(arr, includeVector));
        return ValueTask.FromResult(Fetch(pks.ToArray(), includeVector));
    }

    public IReadOnlyList<ZVecDoc> Query(ZVecQuery query, int topk = 10, string? filter = null, bool includeVector = true) =>
        _reads.Query(query, topk, filter, includeVector);

    public IReadOnlyList<ZVecDoc> Query(ZVecQuery query, int topk, ZVecFilterBuilder filter, bool includeVector = true)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return Query(query, topk, filter.Build(), includeVector);
    }

    public IReadOnlyList<ZVecDoc> Query(
        IReadOnlyList<ZVecQuery> queries,
        int topk = 10,
        ZVecReranker? reranker = null,
        string? filter = null,
        bool includeVector = true) =>
        _reads.Query(queries, topk, reranker, filter, includeVector);

    public IReadOnlyList<ZVecDoc> Query(
        IReadOnlyList<ZVecQuery> queries,
        int topk,
        ZVecReranker? reranker,
        ZVecFilterBuilder filter,
        bool includeVector = true)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return Query(queries, topk, reranker, filter.Build(), includeVector);
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(
        ZVecQuery query,
        int topk = 10,
        string? filter = null,
        bool includeVector = true,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);
        try
        {
            return new ValueTask<IReadOnlyList<ZVecDoc>>(Query(query, topk, filter, includeVector));
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
        }
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(
        ZVecQuery query,
        int topk,
        ZVecFilterBuilder filter,
        bool includeVector = true,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);
        try
        {
            return new ValueTask<IReadOnlyList<ZVecDoc>>(Query(query, topk, filter, includeVector));
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
        }
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(
        IReadOnlyList<ZVecQuery> queries,
        int topk = 10,
        ZVecReranker? reranker = null,
        string? filter = null,
        bool includeVector = true,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);
        try
        {
            return new ValueTask<IReadOnlyList<ZVecDoc>>(Query(queries, topk, reranker, filter, includeVector));
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
        }
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(
        IReadOnlyList<ZVecQuery> queries,
        int topk,
        ZVecReranker? reranker,
        ZVecFilterBuilder filter,
        bool includeVector = true,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);
        try
        {
            return new ValueTask<IReadOnlyList<ZVecDoc>>(Query(queries, topk, reranker, filter, includeVector));
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
        }
    }

    /// <inheritdoc/>
    [Obsolete(ZVecDefaults.Errors.NativeGroupByQueryNotSupported)]
    public IReadOnlyList<ZVecDoc> QueryGroupBy(ZVecGroupByQuery groupQuery)
    {
        _ctx.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(groupQuery);
        throw new NotSupportedException(ZVecDefaults.Errors.NativeGroupByQueryNotSupported);
    }

    /// <inheritdoc/>
    [Obsolete(ZVecDefaults.Errors.NativeGroupByQueryNotSupported)]
    public ValueTask<IReadOnlyList<ZVecDoc>> QueryGroupByAsync(ZVecGroupByQuery groupQuery, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);
        try
        {
#pragma warning disable CS0618
            return new ValueTask<IReadOnlyList<ZVecDoc>>(QueryGroupBy(groupQuery));
#pragma warning restore CS0618
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
        }
    }

    public void AddColumn(ZVecFieldSchema field, string? defaultExpression = null) =>
        _ddl.AddColumn(field, defaultExpression);

    public void DropColumn(string columnName) => _ddl.DropColumn(columnName);

    public void AlterColumn(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null) =>
        _ddl.AlterColumn(columnName, newName, newSchema);

    public void CreateIndex(string columnName, ZVecIndexParam indexParam) =>
        _ddl.CreateIndex(columnName, indexParam);

    public void DropIndex(string columnName) => _ddl.DropIndex(columnName);

    public void Optimize() => _ddl.Optimize();

    public ValueTask AddColumnAsync(ZVecFieldSchema field, string? defaultExpression = null, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            AddColumn(field, defaultExpression);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    public ValueTask DropColumnAsync(string columnName, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            DropColumn(columnName);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    public ValueTask AlterColumnAsync(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            AlterColumn(columnName, newName, newSchema);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    public ValueTask CreateIndexAsync(string columnName, ZVecIndexParam indexParam, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            CreateIndex(columnName, indexParam);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    public ValueTask DropIndexAsync(string columnName, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            DropIndex(columnName);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    public ValueTask OptimizeAsync(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            Optimize();
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }
}
