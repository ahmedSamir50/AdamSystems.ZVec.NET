namespace ZVec.NET;

/// <summary>HNSW index parameters. Maps to <c>zvec_index_params_hnsw_*</c>.</summary>
public sealed class ZVecHnswIndexParam : ZVecIndexParam
{
    /// <summary>Distance metric type. Default is Cosine.</summary>
    public ZVecMetricType MetricType { get; init; } = ZVecDefaults.Hnsw.MetricType;

    /// <summary>Number of bi-directional links (M) connected to each new element. Default is 16.</summary>
    public int M { get; init; } = ZVecDefaults.Hnsw.M;

    /// <summary>Size of search list during construction. Bigger is more accurate but slower. Default is 200.</summary>
    public int EfConstruction { get; init; } = ZVecDefaults.Hnsw.EfConstruction;

    /// <summary>Compression quantization type. Default is Undefined.</summary>
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecDefaults.Hnsw.QuantizeType;
}
