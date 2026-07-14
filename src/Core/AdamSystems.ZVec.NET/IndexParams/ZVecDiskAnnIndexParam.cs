namespace AdamSystems.ZVec.NET;

/// <summary>DiskANN index parameters. Linux-only per upstream docs.</summary>
public sealed class ZVecDiskAnnIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.L2;
    public int MaxDegree { get; init; } = ZVecDefaults.DiskAnn.MaxDegree;
    public int ListSize { get; init; } = ZVecDefaults.DiskAnn.ListSize;
    public int PqChunkNum { get; init; } = ZVecDefaults.DiskAnn.PqChunkNum;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}
