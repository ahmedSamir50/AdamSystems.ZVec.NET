using System.Runtime.InteropServices;
using ZVec.NET.Interop;
using ZVec.NET.Query;

namespace ZVec.NET.Internal;

/// <summary>Fetch, query, and stats native read paths.</summary>
internal sealed class CollectionReadOps
{
    private readonly CollectionNativeContext _ctx;

    public CollectionReadOps(CollectionNativeContext ctx) => _ctx = ctx;

    public ZVecCollectionStats GetStats()
    {
        _ctx.ThrowIfDisposed();
        nint handle = _ctx.DangerousHandle;

        _ctx.Gate.EnterRead();
        try
        {
            return GetStatsCore(handle);
        }
        finally
        {
            _ctx.Gate.ExitRead();
        }
    }

    public ZVecDoc? Fetch(string pk, bool includeVector = false)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(pk);

        var list = Fetch([pk], includeVector);
        return list.Count > 0 ? list[0] : null;
    }

    public IReadOnlyList<ZVecDoc> Fetch(ReadOnlySpan<string> pks, bool includeVector = false)
    {
        _ctx.ThrowIfDisposed();
        if (pks.IsEmpty) return [];

        _ctx.Gate.EnterRead();
        try
        {
            return FetchCore(pks, includeVector);
        }
        finally
        {
            _ctx.Gate.ExitRead();
        }
    }

    public ValueTask<ZVecDoc?> FetchAsync(string pk, bool includeVector = false, CancellationToken ct = default)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(pk);
        ct.ThrowIfCancellationRequested();
        if (!_ctx.Gate.NeedsAsyncWaitForRead)
            return new ValueTask<ZVecDoc?>(Fetch(pk, includeVector));
        return FetchSingleAsyncCore(pk, includeVector, ct);
    }

    private async ValueTask<ZVecDoc?> FetchSingleAsyncCore(string pk, bool includeVector, CancellationToken ct)
    {
        var list = await FetchAsyncCore([pk], includeVector, ct).ConfigureAwait(false);
        return list.Count > 0 ? list[0] : null;
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> FetchAsync(
        IReadOnlyList<string> pks,
        bool includeVector = false,
        CancellationToken ct = default)
    {
        _ctx.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(pks);
        ct.ThrowIfCancellationRequested();
        if (pks.Count == 0) return new ValueTask<IReadOnlyList<ZVecDoc>>([]);
        if (!_ctx.Gate.NeedsAsyncWaitForRead)
        {
            if (pks is string[] arr) return new ValueTask<IReadOnlyList<ZVecDoc>>(Fetch(arr, includeVector));
            return new ValueTask<IReadOnlyList<ZVecDoc>>(Fetch(pks.ToArray(), includeVector));
        }

        var copy = pks is string[] a ? a : pks.ToArray();
        return FetchAsyncCore(copy, includeVector, ct);
    }

    private async ValueTask<IReadOnlyList<ZVecDoc>> FetchAsyncCore(
        string[] pks,
        bool includeVector,
        CancellationToken ct)
    {
        await _ctx.Gate.EnterReadAsync(ct).ConfigureAwait(false);
        try
        {
            return FetchCore(pks, includeVector);
        }
        finally
        {
            _ctx.Gate.ExitRead();
        }
    }

    /// <summary>
    /// Resolves <see cref="ZVecQuery.DocumentId"/> into a vector query via Fetch.
    /// Callers must invoke this <b>before</b> taking the outer query read gate to avoid nested EnterRead.
    /// </summary>
    public ZVecQuery PrepareQuery(ZVecQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.DocumentId))
            return query;

        if (query.Vector.HasValue || query.SparseVector is { Count: > 0 } || query.Fts != null)
            throw new ArgumentException(ZVecDefaults.Errors.QueryDocumentIdConflict, nameof(query));

        var doc = Fetch(query.DocumentId, includeVector: true);
        return BuildQueryFromFetchedDocument(query, doc);
    }

    public async ValueTask<ZVecQuery> PrepareQueryAsync(ZVecQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.DocumentId))
            return query;

        if (query.Vector.HasValue || query.SparseVector is { Count: > 0 } || query.Fts != null)
            throw new ArgumentException(ZVecDefaults.Errors.QueryDocumentIdConflict, nameof(query));

        var doc = await FetchAsync(query.DocumentId, includeVector: true, ct).ConfigureAwait(false);
        return BuildQueryFromFetchedDocument(query, doc);
    }

    private static ZVecQuery BuildQueryFromFetchedDocument(ZVecQuery query, ZVecDoc? doc)
    {
        if (doc is null)
            throw new KeyNotFoundException(string.Format(ZVecDefaults.Errors.QueryDocumentNotFound, query.DocumentId));

        if (doc.DenseVectors.TryGetValue(query.FieldName, out var dense))
        {
            return new ZVecQuery
            {
                FieldName = query.FieldName,
                Vector = dense,
                Fts = query.Fts,
                QueryParams = query.QueryParams
            };
        }

        if (doc.SparseVectors.TryGetValue(query.FieldName, out var sparse))
        {
            return new ZVecQuery
            {
                FieldName = query.FieldName,
                SparseVector = sparse,
                Fts = query.Fts,
                QueryParams = query.QueryParams
            };
        }

        throw new ArgumentException(
            string.Format(ZVecDefaults.Errors.QueryFieldNotOnDocument, query.FieldName, query.DocumentId),
            nameof(query));
    }

    public IReadOnlyList<ZVecDoc> Query(
        ZVecQuery query,
        int topk = 10,
        string? filter = null,
        bool includeVector = true)
    {
        _ctx.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(query);

        query = PrepareQuery(query);
        if (RequiresMultiQuery(query))
            return QueryPrepared([query], topk, reranker: null, filter, includeVector);

        nint handle = _ctx.DangerousHandle;
        _ctx.Gate.EnterRead();
        try
        {
            return QuerySingleCore(handle, query, topk, filter, includeVector);
        }
        finally
        {
            _ctx.Gate.ExitRead();
        }
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(
        ZVecQuery query,
        int topk = 10,
        string? filter = null,
        bool includeVector = true,
        CancellationToken ct = default)
    {
        _ctx.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(query);
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);
        if (!_ctx.Gate.NeedsAsyncWaitForRead && string.IsNullOrWhiteSpace(query.DocumentId))
        {
            try
            {
                return new ValueTask<IReadOnlyList<ZVecDoc>>(Query(query, topk, filter, includeVector));
            }
            catch (Exception ex)
            {
                return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
            }
        }

        return QueryAsyncCore(query, topk, filter, includeVector, ct);
    }

    private async ValueTask<IReadOnlyList<ZVecDoc>> QueryAsyncCore(
        ZVecQuery query,
        int topk,
        string? filter,
        bool includeVector,
        CancellationToken ct)
    {
        query = await PrepareQueryAsync(query, ct).ConfigureAwait(false);
        if (RequiresMultiQuery(query))
            return await QueryPreparedAsync([query], topk, reranker: null, filter, includeVector, ct).ConfigureAwait(false);

        nint handle = _ctx.DangerousHandle;
        await _ctx.Gate.EnterReadAsync(ct).ConfigureAwait(false);
        try
        {
            return QuerySingleCore(handle, query, topk, filter, includeVector);
        }
        finally
        {
            _ctx.Gate.ExitRead();
        }
    }

    public IReadOnlyList<ZVecDoc> Query(
        IReadOnlyList<ZVecQuery> queries,
        int topk = 10,
        ZVecReranker? reranker = null,
        string? filter = null,
        bool includeVector = true)
    {
        _ctx.ThrowIfDisposed();
        if (queries is null || queries.Count == 0) return [];

        var prepared = new ZVecQuery[queries.Count];
        for (int i = 0; i < queries.Count; i++)
            prepared[i] = PrepareQuery(queries[i]);

        return QueryPrepared(prepared, topk, reranker, filter, includeVector);
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(
        IReadOnlyList<ZVecQuery> queries,
        int topk = 10,
        ZVecReranker? reranker = null,
        string? filter = null,
        bool includeVector = true,
        CancellationToken ct = default)
    {
        _ctx.ThrowIfDisposed();
        if (queries is null || queries.Count == 0)
            return new ValueTask<IReadOnlyList<ZVecDoc>>([]);
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);

        bool needsPrepareAsync = false;
        for (int i = 0; i < queries.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(queries[i].DocumentId))
            {
                needsPrepareAsync = true;
                break;
            }
        }

        if (!_ctx.Gate.NeedsAsyncWaitForRead && !needsPrepareAsync)
        {
            try
            {
                return new ValueTask<IReadOnlyList<ZVecDoc>>(Query(queries, topk, reranker, filter, includeVector));
            }
            catch (Exception ex)
            {
                return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
            }
        }

        return QueryMultiAsyncCore(queries, topk, reranker, filter, includeVector, ct);
    }

    private async ValueTask<IReadOnlyList<ZVecDoc>> QueryMultiAsyncCore(
        IReadOnlyList<ZVecQuery> queries,
        int topk,
        ZVecReranker? reranker,
        string? filter,
        bool includeVector,
        CancellationToken ct)
    {
        var prepared = new ZVecQuery[queries.Count];
        for (int i = 0; i < queries.Count; i++)
            prepared[i] = await PrepareQueryAsync(queries[i], ct).ConfigureAwait(false);

        return await QueryPreparedAsync(prepared, topk, reranker, filter, includeVector, ct).ConfigureAwait(false);
    }

    private IReadOnlyList<ZVecDoc> QueryPrepared(
        IReadOnlyList<ZVecQuery> prepared,
        int topk,
        ZVecReranker? reranker,
        string? filter,
        bool includeVector)
    {
        nint handle = _ctx.DangerousHandle;
        _ctx.Gate.EnterRead();
        try
        {
            return QueryMultiCore(handle, prepared, topk, reranker, filter, includeVector);
        }
        finally
        {
            _ctx.Gate.ExitRead();
        }
    }

    private async ValueTask<IReadOnlyList<ZVecDoc>> QueryPreparedAsync(
        IReadOnlyList<ZVecQuery> prepared,
        int topk,
        ZVecReranker? reranker,
        string? filter,
        bool includeVector,
        CancellationToken ct)
    {
        nint handle = _ctx.DangerousHandle;
        await _ctx.Gate.EnterReadAsync(ct).ConfigureAwait(false);
        try
        {
            return QueryMultiCore(handle, prepared, topk, reranker, filter, includeVector);
        }
        finally
        {
            _ctx.Gate.ExitRead();
        }
    }

    private IReadOnlyList<ZVecDoc> QuerySingleCore(
        nint handle,
        ZVecQuery query,
        int topk,
        string? filter,
        bool includeVector)
    {
        using var builder = new NativeQueryBuilder(query, topk, filter, includeVector);

        int rc = NativeMethods.zvec_collection_query(
            handle,
            builder.Handle,
            out nint resultsPtr,
            out nuint resultCount);

        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Query));
        return _ctx.UnmarshalDocs(resultsPtr, resultCount, includeVector);
    }

    private IReadOnlyList<ZVecDoc> QueryMultiCore(
        nint handle,
        IReadOnlyList<ZVecQuery> prepared,
        int topk,
        ZVecReranker? reranker,
        string? filter,
        bool includeVector)
    {
        using var builder = new NativeMultiQueryBuilder(prepared, topk, reranker, filter);
        int rc = NativeMethods.zvec_collection_multi_query(
            handle,
            builder.Handle,
            out nint resultsPtr,
            out nuint resultCount);

        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Query));
        return _ctx.UnmarshalDocs(resultsPtr, resultCount, includeVector);
    }

    private ZVecCollectionStats GetStatsCore(nint handle)
    {
        int rc = NativeMethods.zvec_collection_get_stats(handle, out nint statsPtr);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(GetStats));

        try
        {
            ulong docCount = NativeMethods.zvec_collection_stats_get_doc_count(statsPtr);
            nuint indexCount = NativeMethods.zvec_collection_stats_get_index_count(statsPtr);
            var completeness = new Dictionary<string, float>((int)indexCount);

            for (nuint i = 0; i < indexCount; i++)
            {
                nint namePtr = NativeMethods.zvec_collection_stats_get_index_name(statsPtr, i);
                string name = namePtr != IntPtr.Zero
                    ? Marshal.PtrToStringUTF8(namePtr) ?? string.Empty
                    : string.Empty;
                completeness[name] = NativeMethods.zvec_collection_stats_get_index_completeness(statsPtr, i);
            }

            return new ZVecCollectionStats
            {
                DocCount = (long)docCount,
                IndexCompleteness = completeness
            };
        }
        finally
        {
            NativeMethods.zvec_collection_stats_destroy(statsPtr);
        }
    }

    private static bool RequiresMultiQuery(ZVecQuery query) =>
        query.SparseVector is { Count: > 0 };

    private unsafe IReadOnlyList<ZVecDoc> FetchCore(ReadOnlySpan<string> pks, bool includeVector)
    {
        nint handle = _ctx.DangerousHandle;
        var utf8Pks = new byte[pks.Length][];
        nint[] ptrs = new nint[pks.Length];
        var handles = new GCHandle[pks.Length];

        try
        {
            for (int i = 0; i < pks.Length; i++)
            {
                utf8Pks[i] = System.Text.Encoding.UTF8.GetBytes(pks[i] + "\0");
                handles[i] = GCHandle.Alloc(utf8Pks[i], GCHandleType.Pinned);
                ptrs[i] = handles[i].AddrOfPinnedObject();
            }

            fixed (nint* p = ptrs)
            {
                var rc = NativeMethods.zvec_collection_fetch(
                    handle, (nint)p, (nuint)pks.Length, IntPtr.Zero, 0, includeVector, out nint docsPtr, out nuint docsCount);
                ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Fetch));
                return _ctx.UnmarshalDocs(docsPtr, docsCount, includeVector);
            }
        }
        finally
        {
            foreach (var h in handles) if (h.IsAllocated) h.Free();
        }
    }
}
