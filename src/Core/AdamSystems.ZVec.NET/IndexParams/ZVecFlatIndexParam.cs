namespace AdamSystems.ZVec.NET;

/// <summary>Flat (brute-force) index parameters.</summary>
public sealed class ZVecFlatIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.L2;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}
