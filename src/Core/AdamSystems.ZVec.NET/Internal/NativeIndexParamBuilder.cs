using AdamSystems.ZVec.NET.Interop;

namespace AdamSystems.ZVec.NET.Internal;

internal sealed class NativeIndexParamBuilder : IDisposable
{
    private readonly nint _handle;
    private bool _disposed;

    public nint Handle => _handle;

    public NativeIndexParamBuilder(ZVecIndexParam param)
    {
        if (param == null)
            throw new ArgumentNullException(nameof(param));

        ZVecIndexType type = param switch
        {
            ZVecHnswIndexParam => ZVecIndexType.Hnsw,
            ZVecHnswRabitqIndexParam => ZVecIndexType.HnswRabitq,
            ZVecIvfIndexParam => ZVecIndexType.Ivf,
            ZVecFlatIndexParam => ZVecIndexType.Flat,
            ZVecVamanaIndexParam => ZVecIndexType.Vamana,
            ZVecDiskAnnIndexParam => ZVecIndexType.DiskAnn,
            ZVecInvertIndexParam => ZVecIndexType.Invert,
            ZVecFtsIndexParam => ZVecIndexType.Fts,
            _ => throw new NotSupportedException($"Index parameter type '{param.GetType().Name}' is not supported.")
        };

        _handle = NativeMethods.zvec_index_params_create((int)type);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeIndexParamsCreateFailed);

        try
        {
            ConfigureParams(param);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void ConfigureParams(ZVecIndexParam param)
    {
        switch (param)
        {
            case ZVecHnswIndexParam hnsw:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_metric_type(_handle, (int)hnsw.MetricType), 
                    nameof(NativeMethods.zvec_index_params_set_metric_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_quantize_type(_handle, (int)hnsw.QuantizeType), 
                    nameof(NativeMethods.zvec_index_params_set_quantize_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_hnsw_params(_handle, hnsw.M, hnsw.EfConstruction), 
                    nameof(NativeMethods.zvec_index_params_set_hnsw_params));
                break;

            case ZVecHnswRabitqIndexParam rabitq:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_metric_type(_handle, (int)rabitq.MetricType), 
                    nameof(NativeMethods.zvec_index_params_set_metric_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_hnsw_params(_handle, rabitq.M, rabitq.EfConstruction), 
                    nameof(NativeMethods.zvec_index_params_set_hnsw_params));
                break;

            case ZVecIvfIndexParam ivf:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_metric_type(_handle, (int)ivf.MetricType), 
                    nameof(NativeMethods.zvec_index_params_set_metric_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_quantize_type(_handle, (int)ivf.QuantizeType), 
                    nameof(NativeMethods.zvec_index_params_set_quantize_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_ivf_params(_handle, ivf.CentroidsNum, 0, false), 
                    nameof(NativeMethods.zvec_index_params_set_ivf_params));
                break;

            case ZVecFlatIndexParam flat:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_metric_type(_handle, (int)flat.MetricType), 
                    nameof(NativeMethods.zvec_index_params_set_metric_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_quantize_type(_handle, (int)flat.QuantizeType), 
                    nameof(NativeMethods.zvec_index_params_set_quantize_type));
                break;

            case ZVecVamanaIndexParam vamana:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_metric_type(_handle, (int)vamana.MetricType), 
                    nameof(NativeMethods.zvec_index_params_set_metric_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_quantize_type(_handle, (int)vamana.QuantizeType), 
                    nameof(NativeMethods.zvec_index_params_set_quantize_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_vamana_params(
                        _handle, 
                        vamana.MaxDegree, 
                        vamana.SearchListSize, 
                        vamana.Alpha, 
                        vamana.SaturateGraph, 
                        vamana.UseContiguousMemory), 
                    nameof(NativeMethods.zvec_index_params_set_vamana_params));
                break;

            case ZVecDiskAnnIndexParam diskann:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_metric_type(_handle, (int)diskann.MetricType), 
                    nameof(NativeMethods.zvec_index_params_set_metric_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_quantize_type(_handle, (int)diskann.QuantizeType), 
                    nameof(NativeMethods.zvec_index_params_set_quantize_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_diskann_params(_handle, diskann.MaxDegree, diskann.ListSize, diskann.PqChunkNum), 
                    nameof(NativeMethods.zvec_index_params_set_diskann_params));
                break;

            case ZVecInvertIndexParam invert:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_invert_params(_handle, invert.EnableRangeOptimization, invert.EnableExtendedWildcard), 
                    nameof(NativeMethods.zvec_index_params_set_invert_params));
                break;

            case ZVecFtsIndexParam fts:
                nint filtersArray = IntPtr.Zero;
                try
                {
                    filtersArray = NativeMethods.zvec_string_array_create((nuint)fts.Filters.Count);
                    for (int i = 0; i < fts.Filters.Count; i++)
                    {
                        NativeMethods.zvec_string_array_add(filtersArray, (nuint)i, fts.Filters[i].ToString().ToLowerInvariant());
                    }

                    string? extraJson = fts.ExtraParams?.ToNativeJson();
                    ZVecError.ThrowIfFailed(
                        (ZVecErrorCode)NativeMethods.zvec_index_params_set_fts_params(
                            _handle, 
                            fts.Tokenizer.ToString().ToLowerInvariant(), 
                            filtersArray, 
                            extraJson), 
                        nameof(NativeMethods.zvec_index_params_set_fts_params));
                }
                finally
                {
                    if (filtersArray != IntPtr.Zero)
                    {
                        NativeMethods.zvec_string_array_destroy(filtersArray);
                    }
                }
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.zvec_index_params_destroy(_handle);
        }
    }
}
