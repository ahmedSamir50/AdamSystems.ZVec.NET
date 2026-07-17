namespace ZVec.NET;

/// <summary>
/// HNSW + RaBitQ. Maps to <c>IndexType::HNSW_RABITQ = 4</c> in <c>type.h</c>.
/// <c>c_api.h</c> may omit the macro — pass value 4 until upstream adds it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Platform requirement:</b> HNSW-RaBitQ is currently supported only on x86_64 with AVX2 or higher
/// instruction set support. It is not available on ARM architectures.
/// </para>
/// <para>
/// The SDK throws <see cref="PlatformNotSupportedException"/> on Arm/Arm64 before calling native APIs.
/// </para>
/// </remarks>
public sealed class ZVecHnswRabitqIndexParam : ZVecIndexParam
{
    /// <summary>Distance metric type. Default is Cosine.</summary>
    public ZVecMetricType MetricType { get; init; } = ZVecDefaults.HnswRabitq.MetricType;

    /// <summary>Number of bi-directional links (M) connected to each new element. Default is 16.</summary>
    public int M { get; init; } = ZVecDefaults.HnswRabitq.M;

    /// <summary>Size of search list during construction. Default is 200.</summary>
    public int EfConstruction { get; init; } = ZVecDefaults.HnswRabitq.EfConstruction;

    /// <summary>Total bits for RaBitQ quantization. Default is 7.</summary>
    public int TotalBits { get; init; } = ZVecDefaults.HnswRabitq.TotalBits;

    /// <summary>Number of cluster groups for quantization lookup. Default is 16.</summary>
    public int NumClusters { get; init; } = ZVecDefaults.HnswRabitq.NumClusters;

    /// <summary>Number of samples to extract during cluster training. 0 = auto. Default is 0.</summary>
    public int SampleCount { get; init; } = ZVecDefaults.HnswRabitq.SampleCount;
}
