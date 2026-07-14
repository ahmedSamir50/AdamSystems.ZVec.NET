namespace AdamSystems.ZVec.NET;

/// <summary>Vamana index parameters.</summary>
public sealed class ZVecVamanaIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.L2;
    public int MaxDegree { get; init; } = ZVecDefaults.Vamana.MaxDegree;
    public int SearchListSize { get; init; } = ZVecDefaults.Vamana.SearchListSize;
    public float Alpha { get; init; } = ZVecDefaults.Vamana.Alpha;
    public bool SaturateGraph { get; init; } = false;
    public bool UseContiguousMemory { get; init; } = false;
    public bool UseIdMap { get; init; } = false;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}
