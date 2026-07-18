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
/// (not thread-pool offloads). When optional gates are enabled
/// (<c>MaxConcurrentNativeCalls</c> / <c>MaxConcurrentReads</c> &gt; 0), async paths await
/// <see cref="SemaphoreSlim.WaitAsync(System.Threading.CancellationToken)"/> for the gate only;
/// after acquire, P/Invoke still runs on the continuation thread. Mid-P/Invoke cancel is best-effort.
/// </para>
/// <para>
/// <b>DocumentId queries:</b> <see cref="ZVecQuery.DocumentId"/> is a query-by-id input convenience:
/// an extra Fetch (with vectors) loads that document’s embedding before search. Query results already
/// include document IDs from native — this is not a result-ID gap.
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

    public ValueTask<ZVecStatus> InsertAsync(ZVecDoc doc, CancellationToken ct = default) =>
        _writes.InsertAsync(doc, ct);

    public ValueTask<IReadOnlyList<ZVecWriteResult>> InsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default) =>
        _writes.InsertWithResultsAsync(docs, ct);

    public ZVecStatus Update(ZVecDoc doc) => _writes.Update(doc);
    public ZVecStatus Update(ReadOnlySpan<ZVecDoc> docs) => _writes.Update(docs);
    public IReadOnlyList<ZVecWriteResult> UpdateWithResults(ReadOnlySpan<ZVecDoc> docs) => _writes.UpdateWithResults(docs);

    public ValueTask<ZVecStatus> UpdateAsync(ZVecDoc doc, CancellationToken ct = default) =>
        _writes.UpdateAsync(doc, ct);

    public ValueTask<ZVecStatus> UpdateAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default) =>
        _writes.UpdateAsync(docs, ct);

    public ZVecStatus Upsert(ZVecDoc doc) => _writes.Upsert(doc);
    public ZVecStatus Upsert(ReadOnlySpan<ZVecDoc> docs) => _writes.Upsert(docs);
    public IReadOnlyList<ZVecWriteResult> UpsertWithResults(ReadOnlySpan<ZVecDoc> docs) => _writes.UpsertWithResults(docs);

    public ValueTask<ZVecStatus> UpsertAsync(ZVecDoc doc, CancellationToken ct = default) =>
        _writes.UpsertAsync(doc, ct);

    public ValueTask<ZVecStatus> UpsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default) =>
        _writes.UpsertAsync(docs, ct);

    public ZVecStatus Delete(string pk) => _writes.Delete(pk);
    public ZVecStatus Delete(ReadOnlySpan<string> pks) => _writes.Delete(pks);
    public IReadOnlyList<ZVecWriteResult> DeleteWithResults(ReadOnlySpan<string> pks) => _writes.DeleteWithResults(pks);
    public ZVecStatus DeleteByFilter(string filter) => _writes.DeleteByFilter(filter);

    public ValueTask<ZVecStatus> DeleteAsync(string pk, CancellationToken ct = default) =>
        _writes.DeleteAsync(pk, ct);

    public ValueTask<ZVecStatus> DeleteAsync(IReadOnlyList<string> pks, CancellationToken ct = default) =>
        _writes.DeleteAsync(pks, ct);

    public ValueTask<ZVecStatus> DeleteByFilterAsync(string filter, CancellationToken ct = default) =>
        _writes.DeleteByFilterAsync(filter, ct);

    public ZVecDoc? Fetch(string pk, bool includeVector = false) => _reads.Fetch(pk, includeVector);
    public IReadOnlyList<ZVecDoc> Fetch(ReadOnlySpan<string> pks, bool includeVector = false) => _reads.Fetch(pks, includeVector);

    public ValueTask<ZVecDoc?> FetchAsync(string pk, bool includeVector = false, CancellationToken ct = default) =>
        _reads.FetchAsync(pk, includeVector, ct);

    public ValueTask<IReadOnlyList<ZVecDoc>> FetchAsync(IReadOnlyList<string> pks, bool includeVector = false, CancellationToken ct = default) =>
        _reads.FetchAsync(pks, includeVector, ct);

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
        CancellationToken ct = default) =>
        _reads.QueryAsync(query, topk, filter, includeVector, ct);

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(
        ZVecQuery query,
        int topk,
        ZVecFilterBuilder filter,
        bool includeVector = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return QueryAsync(query, topk, filter.Build(), includeVector, ct);
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(
        IReadOnlyList<ZVecQuery> queries,
        int topk = 10,
        ZVecReranker? reranker = null,
        string? filter = null,
        bool includeVector = true,
        CancellationToken ct = default) =>
        _reads.QueryAsync(queries, topk, reranker, filter, includeVector, ct);

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(
        IReadOnlyList<ZVecQuery> queries,
        int topk,
        ZVecReranker? reranker,
        ZVecFilterBuilder filter,
        bool includeVector = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return QueryAsync(queries, topk, reranker, filter.Build(), includeVector, ct);
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

    public ValueTask AddColumnAsync(ZVecFieldSchema field, string? defaultExpression = null, CancellationToken ct = default) =>
        _ddl.AddColumnAsync(field, defaultExpression, ct);

    public ValueTask DropColumnAsync(string columnName, CancellationToken ct = default) =>
        _ddl.DropColumnAsync(columnName, ct);

    public ValueTask AlterColumnAsync(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null, CancellationToken ct = default) =>
        _ddl.AlterColumnAsync(columnName, newName, newSchema, ct);

    public ValueTask CreateIndexAsync(string columnName, ZVecIndexParam indexParam, CancellationToken ct = default) =>
        _ddl.CreateIndexAsync(columnName, indexParam, ct);

    public ValueTask DropIndexAsync(string columnName, CancellationToken ct = default) =>
        _ddl.DropIndexAsync(columnName, ct);

    public ValueTask OptimizeAsync(CancellationToken ct = default) =>
        _ddl.OptimizeAsync(ct);
}
