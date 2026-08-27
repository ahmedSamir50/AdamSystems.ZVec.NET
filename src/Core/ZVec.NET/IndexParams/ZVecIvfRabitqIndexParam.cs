namespace ZVec.NET;

/// <summary>
/// IVF + RaBitQ index parameters. Maps to <c>IndexType::IVF_RABITQ = 7</c> in <c>type.h</c>.
/// </summary>
/// <remarks>
/// Supported on x86_64 via the official C API (<c>zvec_index_params_create(IVF_RABITQ)</c>).
/// Not available on ARM/Arm64 (same platform gate as HNSW-RaBitQ).
/// </remarks>
public sealed class ZVecIvfRabitqIndexParam : ZVecIndexParam
{
    /// <summary>Distance metric type. Default is L2.</summary>
    public ZVecMetricType MetricType { get; init; } = ZVecDefaults.IvfRabitq.MetricType;

    /// <summary>Number of cluster lists (nlist). Default follows upstream IVF-RaBitQ.</summary>
    public int Nlist { get; init; } = ZVecDefaults.IvfRabitq.Nlist;

    /// <summary>Total bits for RaBitQ quantization. Default is 7.</summary>
    public int TotalBits { get; init; } = ZVecDefaults.IvfRabitq.TotalBits;

    /// <summary>Sample count for training; 0 means use all vectors.</summary>
    public int SampleCount { get; init; } = ZVecDefaults.IvfRabitq.SampleCount;
}
