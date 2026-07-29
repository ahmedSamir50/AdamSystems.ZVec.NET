using System.Buffers;
using System.Runtime.InteropServices;
using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

internal sealed class NativeMultiQueryBuilder : IDisposable
{
    private readonly nint _handle;
    private bool _disposed;
    private readonly List<nint> _subQueries = [];
    private readonly List<MemoryHandle> _pinnedHandles = [];
    private readonly List<nint> _unmanagedAllocations = [];
    private readonly List<GCHandle> _sparsePins = [];
    private readonly List<(int[] Indices, float[] Values)> _rentedSparse = [];

    public nint Handle => _handle;

    public NativeMultiQueryBuilder(
        IReadOnlyList<ZVecQuery> queries,
        int topk,
        ZVecReranker? reranker,
        string? filter = null)
    {
        _handle = NativeMethods.zvec_multi_query_create();
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeMultiQueryCreateFailed);

        try
        {
            NativeMethods.zvec_multi_query_set_topk(_handle, topk);

            if (!string.IsNullOrWhiteSpace(filter))
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_multi_query_set_filter(_handle, filter),
                    nameof(NativeMethods.zvec_multi_query_set_filter));
            }

            // Add sub-queries
            foreach (var query in queries)
            {
                nint subQuery = NativeMethods.zvec_sub_query_create();
                if (subQuery == IntPtr.Zero)
                    throw new InvalidOperationException(ZVecDefaults.Errors.NativeSubQueryCreateFailed);
                _subQueries.Add(subQuery);

                NativeMethods.zvec_sub_query_set_field_name(subQuery, query.FieldName);

                if (query.Vector is { } mem)
                {
                    var handle = mem.Pin();
                    _pinnedHandles.Add(handle);
                    unsafe
                    {
                        ZVecError.ThrowIfFailed(
                            (ZVecErrorCode)NativeMethods.zvec_sub_query_set_query_vector(
                                subQuery, 
                                (IntPtr)handle.Pointer, 
                                (nuint)(mem.Length * sizeof(float))),
                            nameof(NativeMethods.zvec_sub_query_set_query_vector));
                    }
                }
                else if (query.SparseVector is { } sparse)
                {
                    // Keep rented buffers pinned for the lifetime of this builder —
                    // native sub-query may retain pointers until multi-query executes.
                    VectorMarshaller.SerializeSparseVector(sparse, out int[] indices, out float[] values, out int count);
                    _rentedSparse.Add((indices, values));

                    var idxPin = GCHandle.Alloc(indices, GCHandleType.Pinned);
                    var valPin = GCHandle.Alloc(values, GCHandleType.Pinned);
                    _sparsePins.Add(idxPin);
                    _sparsePins.Add(valPin);

                    ZVecError.ThrowIfFailed(
                        (ZVecErrorCode)NativeMethods.zvec_sub_query_set_sparse_vector(
                            subQuery,
                            idxPin.AddrOfPinnedObject(),
                            valPin.AddrOfPinnedObject(),
                            (nuint)count),
                        nameof(NativeMethods.zvec_sub_query_set_sparse_vector));
                }
                else if (query.Fts != null)
                {
                    nint ftsHandle = NativeMethods.zvec_fts_create();
                    if (ftsHandle == IntPtr.Zero)
                        throw new InvalidOperationException(ZVecDefaults.Errors.NativeFtsQueryCreateFailed);

                    // set_fts copies the clause; caller must destroy zvec_fts_t.
                    _unmanagedAllocations.Add(ftsHandle);

                    if (!string.IsNullOrWhiteSpace(query.Fts.QueryString))
                    {
                        NativeMethods.zvec_fts_set_query_string(ftsHandle, query.Fts.QueryString);
                    }
                    if (!string.IsNullOrWhiteSpace(query.Fts.MatchString))
                    {
                        NativeMethods.zvec_fts_set_match_string(ftsHandle, query.Fts.MatchString);
                    }
                    NativeMethods.zvec_sub_query_set_fts(subQuery, ftsHandle);

                    string op = query.Fts.DefaultOperator == ZVecFtsDefaultOperator.And
                        ? ZVecDefaults.Filter.And
                        : ZVecDefaults.Filter.Or;
                    nint ftsParams = NativeMethods.zvec_query_params_fts_create(op);
                    if (ftsParams != IntPtr.Zero)
                    {
                        try
                        {
                            ZVecError.ThrowIfFailed(
                                (ZVecErrorCode)NativeMethods.zvec_sub_query_set_fts_params(subQuery, ftsParams),
                                nameof(NativeMethods.zvec_sub_query_set_fts_params));
                            // Ownership transferred to the sub-query on success.
                        }
                        catch
                        {
                            NativeMethods.zvec_query_params_fts_destroy(ftsParams);
                            throw;
                        }
                    }
                }

                NativeQueryParamsApplicator.Apply(
                    subQuery,
                    query.QueryParams,
                    NativeQueryParamsApplicator.QueryTarget.SubQuery);

                // Add to multi query
                NativeMethods.zvec_multi_query_add_sub_query(_handle, subQuery);
            }

            // Apply reranker
            if (reranker is ZVecRrfReranker rrf)
            {
                NativeMethods.zvec_multi_query_set_rerank_rrf(_handle, rrf.RankConstant);
            }
            else if (reranker is ZVecWeightedReranker weighted)
            {
                ZVecWeightedReranker.ValidateWeights(weighted.Weights, queries.Count);

                double[] weightsArray = new double[queries.Count];
                for (int i = 0; i < queries.Count; i++)
                {
                    var fieldName = queries[i].FieldName;
                    weightsArray[i] = weighted.Weights.TryGetValue(fieldName, out float w) ? w : 1.0;
                }
                
                unsafe
                {
                    fixed (double* pWeights = weightsArray)
                    {
                        NativeMethods.zvec_multi_query_set_rerank_weighted(_handle, (IntPtr)pWeights, (nuint)weightsArray.Length);
                    }
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.zvec_multi_query_destroy(_handle);
        }

        foreach (var sub in _subQueries)
        {
            NativeMethods.zvec_sub_query_destroy(sub);
        }

        // Free zvec_fts_t handles: set_fts copies the clause; caller retains ownership and must destroy.
        // FTS *query params* (zvec_query_params_fts_*) transfer on successful set_fts_params and are
        // destroyed only on failed attach above — never destroy them here.
        foreach (var ptr in _unmanagedAllocations)
        {
            NativeMethods.zvec_fts_destroy(ptr);
        }

        foreach (var pinned in _pinnedHandles)
        {
            pinned.Dispose();
        }

        foreach (var pin in _sparsePins)
        {
            if (pin.IsAllocated)
                pin.Free();
        }
        _sparsePins.Clear();

        foreach (var (indices, values) in _rentedSparse)
        {
            VectorMarshaller.ReturnSparseArrays(indices, values);
        }
        _rentedSparse.Clear();
    }
}
