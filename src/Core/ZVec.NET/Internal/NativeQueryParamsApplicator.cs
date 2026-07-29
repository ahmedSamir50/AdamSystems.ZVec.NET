using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

/// <summary>
/// Builds native query-param handles and attaches them to vector, sub, or group-by queries.
/// </summary>
/// <remarks>
/// Each <c>set_*_params</c> call transfers ownership on success (see <c>c_api.h</c>).
/// On failure before attach completes, this type destroys the params handle to avoid leaks.
/// Upstream removed <c>QueryParams::set_type()</c>; typed create + set_*_params is the only path.
/// </remarks>
internal static class NativeQueryParamsApplicator
{
    internal enum QueryTarget
    {
        VectorQuery,
        SubQuery,
        GroupByQuery
    }

    public static void Apply(nint queryHandle, ZVecQueryParams? queryParams, QueryTarget target)
    {
        if (queryParams is null)
            return;

        switch (queryParams)
        {
            case ZVecHnswQueryParams hnsw:
                ApplyHnsw(queryHandle, hnsw, target);
                break;
            case ZVecIvfQueryParams ivf:
                ApplyIvf(queryHandle, ivf, target);
                break;
            case ZVecFlatQueryParams flat:
                ApplyFlat(queryHandle, flat, target);
                break;
            case ZVecVamanaQueryParams vamana:
                ApplyVamana(queryHandle, vamana, target);
                break;
            case ZVecDiskAnnQueryParams diskann:
                ApplyDiskAnn(queryHandle, diskann, target);
                break;
            default:
                throw new NotSupportedException(
                    string.Format(ZVecDefaults.Errors.UnsupportedQueryParamsType, queryParams.GetType().Name));
        }
    }

    private static void ApplyHnsw(nint queryHandle, ZVecHnswQueryParams hnsw, QueryTarget target)
    {
        nint handle = NativeMethods.zvec_query_params_hnsw_create(
            hnsw.EfSearch ?? ZVecDefaults.Query.HnswEfSearch,
            hnsw.Radius ?? 0f,
            hnsw.IsLinear ?? false,
            hnsw.IsUsingRefiner ?? false);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeQueryCreateFailed);

        AttachOrDestroy(
            handle,
            NativeMethods.zvec_query_params_hnsw_destroy,
            queryHandle,
            target,
            NativeMethods.zvec_vector_query_set_hnsw_params,
            NativeMethods.zvec_sub_query_set_hnsw_params,
            NativeMethods.zvec_group_by_vector_query_set_hnsw_params,
            nameof(NativeMethods.zvec_vector_query_set_hnsw_params));
    }

    private static void ApplyIvf(nint queryHandle, ZVecIvfQueryParams ivf, QueryTarget target)
    {
        nint handle = NativeMethods.zvec_query_params_ivf_create(
            ivf.Nprobe ?? ZVecDefaults.Query.IvfNprobe,
            ivf.IsUsingRefiner ?? false,
            ivf.ScaleFactor ?? ZVecDefaults.Query.IvfScaleFactor);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeQueryCreateFailed);

        try
        {
            // Radius / IsLinear are not constructor args on the C API.
            if (ivf.Radius.HasValue)
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_query_params_ivf_set_radius(handle, ivf.Radius.Value),
                    nameof(NativeMethods.zvec_query_params_ivf_set_radius));
            }

            if (ivf.IsLinear.HasValue)
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_query_params_ivf_set_is_linear(handle, ivf.IsLinear.Value),
                    nameof(NativeMethods.zvec_query_params_ivf_set_is_linear));
            }
        }
        catch
        {
            NativeMethods.zvec_query_params_ivf_destroy(handle);
            throw;
        }

        AttachOrDestroy(
            handle,
            NativeMethods.zvec_query_params_ivf_destroy,
            queryHandle,
            target,
            NativeMethods.zvec_vector_query_set_ivf_params,
            NativeMethods.zvec_sub_query_set_ivf_params,
            NativeMethods.zvec_group_by_vector_query_set_ivf_params,
            nameof(NativeMethods.zvec_vector_query_set_ivf_params));
    }

    private static void ApplyFlat(nint queryHandle, ZVecFlatQueryParams flat, QueryTarget target)
    {
        nint handle = NativeMethods.zvec_query_params_flat_create(
            flat.IsUsingRefiner ?? false,
            flat.ScaleFactor ?? ZVecDefaults.Query.FlatScaleFactor);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeQueryCreateFailed);

        try
        {
            if (flat.Radius.HasValue)
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_query_params_flat_set_radius(handle, flat.Radius.Value),
                    nameof(NativeMethods.zvec_query_params_flat_set_radius));
            }

            if (flat.IsLinear.HasValue)
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_query_params_flat_set_is_linear(handle, flat.IsLinear.Value),
                    nameof(NativeMethods.zvec_query_params_flat_set_is_linear));
            }
        }
        catch
        {
            NativeMethods.zvec_query_params_flat_destroy(handle);
            throw;
        }

        AttachOrDestroy(
            handle,
            NativeMethods.zvec_query_params_flat_destroy,
            queryHandle,
            target,
            NativeMethods.zvec_vector_query_set_flat_params,
            NativeMethods.zvec_sub_query_set_flat_params,
            NativeMethods.zvec_group_by_vector_query_set_flat_params,
            nameof(NativeMethods.zvec_vector_query_set_flat_params));
    }

    private static void ApplyVamana(nint queryHandle, ZVecVamanaQueryParams vamana, QueryTarget target)
    {
        nint handle = NativeMethods.zvec_query_params_vamana_create(
            vamana.EfSearch ?? ZVecDefaults.Query.VamanaEfSearch,
            vamana.Radius ?? 0f,
            vamana.IsLinear ?? false,
            vamana.IsUsingRefiner ?? false);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeQueryCreateFailed);

        AttachOrDestroy(
            handle,
            NativeMethods.zvec_query_params_vamana_destroy,
            queryHandle,
            target,
            NativeMethods.zvec_vector_query_set_vamana_params,
            NativeMethods.zvec_sub_query_set_vamana_params,
            NativeMethods.zvec_group_by_vector_query_set_vamana_params,
            nameof(NativeMethods.zvec_vector_query_set_vamana_params));
    }

    private static void ApplyDiskAnn(nint queryHandle, ZVecDiskAnnQueryParams diskann, QueryTarget target)
    {
        nint handle = NativeMethods.zvec_query_params_diskann_create(
            diskann.ListSize ?? ZVecDefaults.Query.DiskAnnListSize);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeQueryCreateFailed);

        try
        {
            if (diskann.Radius.HasValue)
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_query_params_diskann_set_radius(handle, diskann.Radius.Value),
                    nameof(NativeMethods.zvec_query_params_diskann_set_radius));
            }

            if (diskann.IsLinear.HasValue)
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_query_params_diskann_set_is_linear(handle, diskann.IsLinear.Value),
                    nameof(NativeMethods.zvec_query_params_diskann_set_is_linear));
            }

            if (diskann.IsUsingRefiner.HasValue)
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_query_params_diskann_set_is_using_refiner(
                        handle, diskann.IsUsingRefiner.Value),
                    nameof(NativeMethods.zvec_query_params_diskann_set_is_using_refiner));
            }
        }
        catch
        {
            NativeMethods.zvec_query_params_diskann_destroy(handle);
            throw;
        }

        AttachOrDestroy(
            handle,
            NativeMethods.zvec_query_params_diskann_destroy,
            queryHandle,
            target,
            NativeMethods.zvec_vector_query_set_diskann_params,
            NativeMethods.zvec_sub_query_set_diskann_params,
            NativeMethods.zvec_group_by_vector_query_set_diskann_params,
            nameof(NativeMethods.zvec_vector_query_set_diskann_params));
    }

    private static void AttachOrDestroy(
        nint paramsHandle,
        Action<nint> destroy,
        nint queryHandle,
        QueryTarget target,
        Func<nint, nint, int> setOnVector,
        Func<nint, nint, int> setOnSub,
        Func<nint, nint, int> setOnGroupBy,
        string opName)
    {
        try
        {
            int rc = target switch
            {
                QueryTarget.VectorQuery => setOnVector(queryHandle, paramsHandle),
                QueryTarget.SubQuery => setOnSub(queryHandle, paramsHandle),
                QueryTarget.GroupByQuery => setOnGroupBy(queryHandle, paramsHandle),
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, opName);
            // Ownership transferred to the query on success — do not destroy paramsHandle.
        }
        catch
        {
            // Attach failed (or target invalid): destroy before rethrow so the handle cannot leak.
            destroy(paramsHandle);
            throw;
        }
    }
}
