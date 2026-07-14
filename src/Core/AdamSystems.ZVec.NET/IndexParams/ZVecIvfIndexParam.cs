namespace AdamSystems.ZVec.NET;

/// <summary>IVF index parameters.</summary>
public sealed class ZVecIvfIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.L2;
    public int CentroidsNum { get; init; } = ZVecDefaults.Ivf.CentroidsNum;
    public int Nlist { get; init; } = ZVecDefaults.Ivf.Nlist;
    public int Nprobe { get; init; } = ZVecDefaults.Ivf.Nprobe;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}
