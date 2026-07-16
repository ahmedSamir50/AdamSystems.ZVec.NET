namespace ZVec.NET;

/// <summary>Vamana graph index parameters. Designed for high performance out-of-core and memory-mapped searches.</summary>
public sealed class ZVecVamanaIndexParam : ZVecIndexParam
{
    /// <summary>Distance metric type. Default is L2.</summary>
    public ZVecMetricType MetricType { get; init; } = ZVecDefaults.Vamana.MetricType;

    /// <summary>Maximum out-degree of graph nodes. Default is 64.</summary>
    public int MaxDegree { get; init; } = ZVecDefaults.Vamana.MaxDegree;

    /// <summary>Candidate pool size during search. Default is 100.</summary>
    public int SearchListSize { get; init; } = ZVecDefaults.Vamana.SearchListSize;

    /// <summary>Scaling parameter (alpha) determining the breadth of navigation. Default is 1.2.</summary>
    public float Alpha { get; init; } = ZVecDefaults.Vamana.Alpha;

    /// <summary>Whether to saturate the search graph to optimize search paths. Default is false.</summary>
    public bool SaturateGraph { get; init; } = ZVecDefaults.Vamana.SaturateGraph;

    /// <summary>Whether to layout the graph in contiguous memory. Default is false.</summary>
    public bool UseContiguousMemory { get; init; } = ZVecDefaults.Vamana.UseContiguousMemory;

    /// <summary>Whether to use an ID map to map vector IDs to internal offsets. Default is false.</summary>
    public bool UseIdMap { get; init; } = ZVecDefaults.Vamana.UseIdMap;

    /// <summary>Quantization compression type. Default is Undefined.</summary>
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecDefaults.Vamana.QuantizeType;
}
