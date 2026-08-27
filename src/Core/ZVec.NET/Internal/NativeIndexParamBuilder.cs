using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

internal sealed class NativeIndexParamBuilder : IDisposable
{
    private readonly nint _handle;
    private bool _disposed;

    public nint Handle => _handle;

    public NativeIndexParamBuilder(ZVecIndexParam param)
    {
        ArgumentNullException.ThrowIfNull(param);
        ZVecPlatformRequirements.ThrowIfUnsupported(param);

        if (param is ZVecHnswRabitqIndexParam)
        {
            throw new NotSupportedException(ZVecDefaults.Errors.NativeHnswRabitqParamsNotSupported);
        }

        ZVecIndexType type = param switch
        {
            ZVecHnswIndexParam => ZVecIndexType.Hnsw,
            ZVecIvfIndexParam => ZVecIndexType.Ivf,
            ZVecIvfRabitqIndexParam => ZVecIndexType.IvfRabitq,
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
                ApplyEnableRotate(hnsw.QuantizeType, hnsw.EnableRotate);
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_hnsw_params(_handle, hnsw.M, hnsw.EfConstruction), 
                    nameof(NativeMethods.zvec_index_params_set_hnsw_params));
                break;

            case ZVecIvfIndexParam ivf:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_metric_type(_handle, (int)ivf.MetricType), 
                    nameof(NativeMethods.zvec_index_params_set_metric_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_quantize_type(_handle, (int)ivf.QuantizeType), 
                    nameof(NativeMethods.zvec_index_params_set_quantize_type));
                ApplyEnableRotate(ivf.QuantizeType, ivf.EnableRotate);
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_ivf_params(_handle, ivf.CentroidsNum, 0, false), 
                    nameof(NativeMethods.zvec_index_params_set_ivf_params));
                break;

            case ZVecIvfRabitqIndexParam ivfRq:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_metric_type(_handle, (int)ivfRq.MetricType),
                    nameof(NativeMethods.zvec_index_params_set_metric_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_ivf_rabitq_params(
                        _handle, ivfRq.Nlist, ivfRq.TotalBits, ivfRq.SampleCount),
                    nameof(NativeMethods.zvec_index_params_set_ivf_rabitq_params));
                break;

            case ZVecFlatIndexParam flat:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_metric_type(_handle, (int)flat.MetricType), 
                    nameof(NativeMethods.zvec_index_params_set_metric_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_quantize_type(_handle, (int)flat.QuantizeType), 
                    nameof(NativeMethods.zvec_index_params_set_quantize_type));
                ApplyEnableRotate(flat.QuantizeType, flat.EnableRotate);
                break;

            case ZVecVamanaIndexParam vamana:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_metric_type(_handle, (int)vamana.MetricType), 
                    nameof(NativeMethods.zvec_index_params_set_metric_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_quantize_type(_handle, (int)vamana.QuantizeType), 
                    nameof(NativeMethods.zvec_index_params_set_quantize_type));
                ApplyEnableRotate(vamana.QuantizeType, vamana.EnableRotate);
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_vamana_params(
                        _handle, 
                        vamana.MaxDegree, 
                        vamana.SearchListSize, 
                        vamana.Alpha, 
                        vamana.SaturateGraph, 
                        vamana.UseContiguousMemory), 
                    nameof(NativeMethods.zvec_index_params_set_vamana_params));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_vamana_two_pass_build(
                        _handle, vamana.TwoPassBuild),
                    nameof(NativeMethods.zvec_index_params_set_vamana_two_pass_build));
                break;

            case ZVecDiskAnnIndexParam diskann:
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_metric_type(_handle, (int)diskann.MetricType), 
                    nameof(NativeMethods.zvec_index_params_set_metric_type));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_index_params_set_quantize_type(_handle, (int)diskann.QuantizeType), 
                    nameof(NativeMethods.zvec_index_params_set_quantize_type));
                ApplyEnableRotate(diskann.QuantizeType, diskann.EnableRotate);
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
                        NativeMethods.zvec_string_array_add(
                            filtersArray,
                            (nuint)i,
                            ZVecNativeStrings.ToNative(fts.Filters[i]));
                    }

                    string? extraJson = fts.ExtraParams?.ToNativeJson();
                    ZVecError.ThrowIfFailed(
                        (ZVecErrorCode)NativeMethods.zvec_index_params_set_fts_params(
                            _handle, 
                            ZVecNativeStrings.ToNative(fts.Tokenizer),
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

    private void ApplyEnableRotate(ZVecQuantizeType quantizeType, bool enableRotate)
    {
        if (!enableRotate)
            return;
        if (quantizeType is not (ZVecQuantizeType.Int8 or ZVecQuantizeType.Int4))
            return;

        ZVecError.ThrowIfFailed(
            (ZVecErrorCode)NativeMethods.zvec_index_params_set_quantizer_enable_rotate(_handle, true),
            nameof(NativeMethods.zvec_index_params_set_quantizer_enable_rotate));
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
