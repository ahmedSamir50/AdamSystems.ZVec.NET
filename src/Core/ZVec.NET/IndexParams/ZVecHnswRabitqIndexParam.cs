namespace ZVec.NET;

/// <summary>
/// HNSW + RaBitQ. Maps to <c>IndexType::HNSW_RABITQ = 4</c> in <c>type.h</c>.
/// <c>c_api.h</c> may omit the macro — pass value 4 until upstream adds it. x86_64/AVX2 only.
/// </summary>
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
