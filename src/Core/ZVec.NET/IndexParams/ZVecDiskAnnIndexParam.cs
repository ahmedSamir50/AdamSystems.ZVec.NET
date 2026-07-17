namespace ZVec.NET;

/// <summary>DiskANN index parameters.</summary>
/// <remarks>
/// <para>
/// <b>Platform requirement:</b> DiskANN is currently supported on Linux only and requires the
/// libaio library (Linux asynchronous I/O) to be installed on the system.
/// </para>
/// <para>
/// The SDK throws <see cref="PlatformNotSupportedException"/> on non-Linux platforms before calling native APIs.
/// </para>
/// </remarks>
public sealed class ZVecDiskAnnIndexParam : ZVecIndexParam
{
    /// <summary>Distance metric type. Default is L2.</summary>
    public ZVecMetricType MetricType { get; init; } = ZVecDefaults.DiskAnn.MetricType;

    /// <summary>Maximum degree of the search graph (out-degree limit). Default is 100.</summary>
    public int MaxDegree { get; init; } = ZVecDefaults.DiskAnn.MaxDegree;

    /// <summary>Size of search candidate list during search/construction. Default is 50.</summary>
    public int ListSize { get; init; } = ZVecDefaults.DiskAnn.ListSize;

    /// <summary>Number of chunks for Product Quantization (PQ). 0 = auto-configured. Default is 0.</summary>
    public int PqChunkNum { get; init; } = ZVecDefaults.DiskAnn.PqChunkNum;

    /// <summary>Compression quantization type. Default is Undefined.</summary>
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecDefaults.DiskAnn.QuantizeType;
}
