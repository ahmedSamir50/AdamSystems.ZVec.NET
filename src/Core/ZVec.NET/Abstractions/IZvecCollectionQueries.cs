using ZVec.NET.Query;

namespace ZVec.NET;

/// <summary>Fetch, query, and stats surface for a ZVec collection.</summary>
/// <remarks>
/// Async methods are cancellation-aware wrappers around synchronous native calls.
/// Queries that set <see cref="ZVecQuery.DocumentId"/> perform an extra Fetch before search.
/// Pass <c>includeVector: false</c> on Query to skip copying result vectors (lower latency/alloc).
/// </remarks>
public interface IZvecCollectionQueries
{
    /// <summary>Live collection statistics from the native store.</summary>
    ZVecCollectionStats Stats { get; }

    /// <summary>Refreshes and returns collection statistics from the native store.</summary>
    ZVecCollectionStats GetStats();

    ZVecDoc? Fetch(string pk, bool includeVector = false);
    IReadOnlyList<ZVecDoc> Fetch(ReadOnlySpan<string> pks, bool includeVector = false);
    ValueTask<ZVecDoc?> FetchAsync(string pk, bool includeVector = false, CancellationToken ct = default);
    ValueTask<IReadOnlyList<ZVecDoc>> FetchAsync(IReadOnlyList<string> pks, bool includeVector = false, CancellationToken ct = default);

    IReadOnlyList<ZVecDoc> Query(ZVecQuery query, int topk = 10, string? filter = null, bool includeVector = true);
    IReadOnlyList<ZVecDoc> Query(ZVecQuery query, int topk, ZVecFilterBuilder filter, bool includeVector = true);
    IReadOnlyList<ZVecDoc> Query(IReadOnlyList<ZVecQuery> queries, int topk = 10, ZVecReranker? reranker = null, string? filter = null, bool includeVector = true);
    IReadOnlyList<ZVecDoc> Query(IReadOnlyList<ZVecQuery> queries, int topk, ZVecReranker? reranker, ZVecFilterBuilder filter, bool includeVector = true);
    ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(ZVecQuery query, int topk = 10, string? filter = null, bool includeVector = true, CancellationToken ct = default);
    ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(ZVecQuery query, int topk, ZVecFilterBuilder filter, bool includeVector = true, CancellationToken ct = default);
    ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(IReadOnlyList<ZVecQuery> queries, int topk = 10, ZVecReranker? reranker = null, string? filter = null, bool includeVector = true, CancellationToken ct = default);
    ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(IReadOnlyList<ZVecQuery> queries, int topk, ZVecReranker? reranker, ZVecFilterBuilder filter, bool includeVector = true, CancellationToken ct = default);

    /// <summary>Not supported until upstream exposes a collection-level group-by DQL entry point.</summary>
    [Obsolete(ZVecDefaults.Errors.NativeGroupByQueryNotSupported)]
    IReadOnlyList<ZVecDoc> QueryGroupBy(ZVecGroupByQuery groupQuery);

    /// <summary>Not supported until upstream exposes a collection-level group-by DQL entry point.</summary>
    [Obsolete(ZVecDefaults.Errors.NativeGroupByQueryNotSupported)]
    ValueTask<IReadOnlyList<ZVecDoc>> QueryGroupByAsync(ZVecGroupByQuery groupQuery, CancellationToken ct = default);
}
