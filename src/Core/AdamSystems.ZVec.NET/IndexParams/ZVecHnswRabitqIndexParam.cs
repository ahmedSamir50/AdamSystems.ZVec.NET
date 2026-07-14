namespace AdamSystems.ZVec.NET;

/// <summary>
/// HNSW + RaBitQ. Maps to <c>IndexType::HNSW_RABITQ = 4</c> in <c>type.h</c>.
/// <c>c_api.h</c> may omit the macro — pass value 4 until upstream adds it. x86_64/AVX2 only.
/// </summary>
public sealed class ZVecHnswRabitqIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.Cosine;
    public int M { get; init; } = ZVecDefaults.HnswRabitq.M;
    public int EfConstruction { get; init; } = ZVecDefaults.HnswRabitq.EfConstruction;
    public int TotalBits { get; init; } = ZVecDefaults.HnswRabitq.TotalBits;
    public int NumClusters { get; init; } = ZVecDefaults.HnswRabitq.NumClusters;
    public int SampleCount { get; init; } = ZVecDefaults.HnswRabitq.SampleCount;
}
