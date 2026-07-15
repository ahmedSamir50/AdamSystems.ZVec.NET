namespace AdamSystems.ZVec.NET;

/// <summary>
/// Defines a ZVec vector collection — lifecycle, CRUD, query, and DDL operations.
/// </summary>
/// <remarks>
/// Dispose/DisposeAsync perform a <c>zvec_collection_close</c> (safe to call multiple times;
/// internally idempotent). <see cref="Destroy"/> and <see cref="DestroyAsync"/> first call
/// <c>zvec_collection_destroy</c> (deletes on-disk data) then close. All idempotency and
/// mutual exclusion is achieved via <see cref="Interlocked"/> — no custom locks.
/// </remarks>
public interface IZvecCollection : IDisposable, IAsyncDisposable
{
    /// <summary>The file system path where the collection is stored.</summary>
    string Path { get; }

    /// <summary>
    /// The schema configuration of this collection.
    /// May be <c>null</c> when opened without a schema (e.g. via <c>Open</c>).
    /// </summary>
    ZVecCollectionSchema? Schema { get; }

    /// <summary>
    /// Destroys the collection: deletes all on-disk data, then closes the handle.
    /// Idempotent — subsequent calls are no-ops.
    /// </summary>
    void Destroy();

    /// <summary>
    /// Asynchronously destroys the collection.
    /// </summary>
    ValueTask DestroyAsync(CancellationToken ct = default);

    // =========================================================================
    // Epic E12 — CRUD
    // =========================================================================

    ZVecStatus Insert(ZVecDoc doc);
    ZVecStatus Insert(ReadOnlySpan<ZVecDoc> docs);
    IReadOnlyList<ZVecWriteResult> InsertWithResults(ReadOnlySpan<ZVecDoc> docs);
    ValueTask<ZVecStatus> InsertAsync(ZVecDoc doc, CancellationToken ct = default);
    ValueTask<IReadOnlyList<ZVecWriteResult>> InsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default);

    ZVecStatus Update(ZVecDoc doc);
    ZVecStatus Upsert(ZVecDoc doc);

    ZVecStatus Delete(string pk);
    ZVecStatus Delete(ReadOnlySpan<string> pks);
    IReadOnlyList<ZVecWriteResult> DeleteWithResults(ReadOnlySpan<string> pks);
    ZVecStatus DeleteByFilter(string filter);
    ValueTask<ZVecStatus> DeleteAsync(string pk, CancellationToken ct = default);
    ValueTask<ZVecStatus> DeleteByFilterAsync(string filter, CancellationToken ct = default);

    ZVecDoc? Fetch(string pk, bool includeVector = false);
    IReadOnlyList<ZVecDoc> Fetch(ReadOnlySpan<string> pks, bool includeVector = false);
    ValueTask<ZVecDoc?> FetchAsync(string pk, bool includeVector = false, CancellationToken ct = default);

    // =========================================================================
    // Epic E13 — Query
    // =========================================================================

    IReadOnlyList<ZVecDoc> Query(ZVecQuery query, int topk = 10, string? filter = null);
    IReadOnlyList<ZVecDoc> Query(IReadOnlyList<ZVecQuery> queries, int topk = 10, ZVecReranker? reranker = null);
    ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(ZVecQuery query, int topk = 10, string? filter = null, CancellationToken ct = default);

    // =========================================================================
    // Epic E14 — DDL
    // =========================================================================

    void AddColumn(ZVecFieldSchema field, string? defaultExpression = null);
    void DropColumn(string columnName);
    void AlterColumn(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null);
    void CreateIndex(string columnName, ZVecIndexParam indexParam);
    void DropIndex(string columnName);
    void Optimize();
    ValueTask AddColumnAsync(ZVecFieldSchema field, string? defaultExpression = null, CancellationToken ct = default);
    ValueTask DropColumnAsync(string columnName, CancellationToken ct = default);
    ValueTask OptimizeAsync(CancellationToken ct = default);
}
