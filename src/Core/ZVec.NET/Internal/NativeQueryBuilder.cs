using System.Buffers;
using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

internal sealed unsafe class NativeQueryBuilder : IDisposable
{
    private readonly nint _handle;
    private bool _disposed;
    private IntPtr _ftsHandle;
    private IntPtr _ftsParamsHandle;
    private readonly List<MemoryHandle> _pinnedHandles = [];

    public nint Handle => _handle;

    public NativeQueryBuilder(ZVecQuery query, int topk, string? filter, bool includeVector = true)
    {
        _handle = NativeMethods.zvec_vector_query_create();
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeQueryCreateFailed);

        try
        {
            ZVecError.ThrowIfFailed(
                (ZVecErrorCode)NativeMethods.zvec_vector_query_set_field_name(_handle, query.FieldName), 
                nameof(NativeMethods.zvec_vector_query_set_field_name));
                
            ZVecError.ThrowIfFailed(
                (ZVecErrorCode)NativeMethods.zvec_vector_query_set_topk(_handle, topk), 
                nameof(NativeMethods.zvec_vector_query_set_topk));

            if (!string.IsNullOrWhiteSpace(filter))
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_vector_query_set_filter(_handle, filter), 
                    nameof(NativeMethods.zvec_vector_query_set_filter));
            }

            if (query.Vector.HasValue)
            {
                var memHandle = query.Vector.Value.Pin();
                _pinnedHandles.Add(memHandle);
                
                nuint size = (nuint)(query.Vector.Value.Length * sizeof(float));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_vector_query_set_query_vector(_handle, new IntPtr(memHandle.Pointer), size), 
                    nameof(NativeMethods.zvec_vector_query_set_query_vector));
            }

            ApplyQueryParams(query.QueryParams);

            ZVecError.ThrowIfFailed(
                (ZVecErrorCode)NativeMethods.zvec_vector_query_set_include_vector(_handle, includeVector), 
                nameof(NativeMethods.zvec_vector_query_set_include_vector));

            if (query.Fts != null)
            {
                _ftsHandle = NativeMethods.zvec_fts_create();
                if (_ftsHandle == IntPtr.Zero)
                    throw new InvalidOperationException(ZVecDefaults.Errors.NativeFtsQueryCreateFailed);

                if (!string.IsNullOrWhiteSpace(query.Fts.QueryString))
                {
                    ZVecError.ThrowIfFailed(
                        (ZVecErrorCode)NativeMethods.zvec_fts_set_query_string(_ftsHandle, query.Fts.QueryString),
                        nameof(NativeMethods.zvec_fts_set_query_string));
                }

                if (!string.IsNullOrWhiteSpace(query.Fts.MatchString))
                {
                    ZVecError.ThrowIfFailed(
                        (ZVecErrorCode)NativeMethods.zvec_fts_set_match_string(_ftsHandle, query.Fts.MatchString),
                        nameof(NativeMethods.zvec_fts_set_match_string));
                }

                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_vector_query_set_fts(_handle, _ftsHandle),
                    nameof(NativeMethods.zvec_vector_query_set_fts));

                string op = query.Fts.DefaultOperator == ZVecFtsDefaultOperator.And
                    ? ZVecDefaults.Filter.And
                    : ZVecDefaults.Filter.Or;
                _ftsParamsHandle = NativeMethods.zvec_query_params_fts_create(op);
                if (_ftsParamsHandle != IntPtr.Zero)
                {
                    ZVecError.ThrowIfFailed(
                        (ZVecErrorCode)NativeMethods.zvec_vector_query_set_fts_params(_handle, _ftsParamsHandle),
                        nameof(NativeMethods.zvec_vector_query_set_fts_params));
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void ApplyQueryParams(ZVecQueryParams? queryParams)
    {
        if (queryParams is null)
            return;

        switch (queryParams)
        {
            case ZVecHnswQueryParams hnsw:
            {
                int ef = hnsw.EfSearch ?? ZVecDefaults.Query.HnswEfSearch;
                nint handle = NativeMethods.zvec_query_params_hnsw_create(ef, 0f, false, false);
                if (handle == IntPtr.Zero)
                    throw new InvalidOperationException(ZVecDefaults.Errors.NativeQueryCreateFailed);

                if (hnsw.EfSearch.HasValue)
                {
                    ZVecError.ThrowIfFailed(
                        (ZVecErrorCode)NativeMethods.zvec_query_params_hnsw_set_ef(handle, hnsw.EfSearch.Value),
                        nameof(NativeMethods.zvec_query_params_hnsw_set_ef));
                }

                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_vector_query_set_hnsw_params(_handle, handle),
                    nameof(NativeMethods.zvec_vector_query_set_hnsw_params));
                break;
            }
            case ZVecIvfQueryParams ivf:
            {
                int nprobe = ivf.Nprobe ?? ZVecDefaults.Query.IvfNprobe;
                nint handle = NativeMethods.zvec_query_params_ivf_create(
                    nprobe, false, ZVecDefaults.Query.IvfScaleFactor);
                if (handle == IntPtr.Zero)
                    throw new InvalidOperationException(ZVecDefaults.Errors.NativeQueryCreateFailed);

                if (ivf.Nprobe.HasValue)
                {
                    ZVecError.ThrowIfFailed(
                        (ZVecErrorCode)NativeMethods.zvec_query_params_ivf_set_nprobe(handle, ivf.Nprobe.Value),
                        nameof(NativeMethods.zvec_query_params_ivf_set_nprobe));
                }

                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_vector_query_set_ivf_params(_handle, handle),
                    nameof(NativeMethods.zvec_vector_query_set_ivf_params));
                break;
            }
            default:
                throw new NotSupportedException(
                    string.Format(ZVecDefaults.Errors.UnsupportedQueryParamsType, queryParams.GetType().Name));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.zvec_vector_query_destroy(_handle);
        }
        
        if (_ftsHandle != IntPtr.Zero)
        {
            NativeMethods.zvec_fts_destroy(_ftsHandle);
            _ftsHandle = IntPtr.Zero;
        }

        if (_ftsParamsHandle != IntPtr.Zero)
        {
            // zvec_vector_query_set_fts_params takes ownership of the ftsParams struct.
            // Do NOT call zvec_query_params_fts_destroy on it, otherwise it causes a double free!
            _ftsParamsHandle = IntPtr.Zero;
        }

        foreach (var pinned in _pinnedHandles)
        {
            pinned.Dispose();
        }
        _pinnedHandles.Clear();
    }
}
