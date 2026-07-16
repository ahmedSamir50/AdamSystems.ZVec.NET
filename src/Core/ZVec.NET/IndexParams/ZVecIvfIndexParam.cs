namespace ZVec.NET;

/// <summary>IVF (Inverted File) index parameters.</summary>
public sealed class ZVecIvfIndexParam : ZVecIndexParam
{
    /// <summary>Distance metric type. Default is L2.</summary>
    public ZVecMetricType MetricType { get; init; } = ZVecDefaults.Ivf.MetricType;

    /// <summary>Number of centroids (clusters) to build. Default is 256.</summary>
    public int CentroidsNum { get; init; } = ZVecDefaults.Ivf.CentroidsNum;

    /// <summary>Alias for number of cluster lists. Default is 16.</summary>
    public int Nlist { get; init; } = ZVecDefaults.Ivf.Nlist;

    /// <summary>Number of cluster lists to probe during query time. Default is 8.</summary>
    public int Nprobe { get; init; } = ZVecDefaults.Ivf.Nprobe;

    /// <summary>Compression quantization type. Default is Undefined.</summary>
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecDefaults.Ivf.QuantizeType;
}
