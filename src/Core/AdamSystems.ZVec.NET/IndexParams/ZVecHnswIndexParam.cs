namespace AdamSystems.ZVec.NET;

/// <summary>HNSW index parameters. Maps to <c>zvec_index_params_hnsw_*</c>.</summary>
public sealed class ZVecHnswIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.Cosine;
    public int M { get; init; } = ZVecDefaults.Hnsw.M;
    public int EfConstruction { get; init; } = ZVecDefaults.Hnsw.EfConstruction;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}
