namespace ZVec.NET;

/// <summary>Flat (brute-force) index parameters.</summary>
public sealed class ZVecFlatIndexParam : ZVecIndexParam
{
    /// <summary>Distance metric type. Default is L2.</summary>
    public ZVecMetricType MetricType { get; init; } = ZVecDefaults.Flat.MetricType;

    /// <summary>Compression quantization type. Default is Undefined.</summary>
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecDefaults.Flat.QuantizeType;

    /// <summary>
    /// When true and <see cref="QuantizeType"/> is Int8 or Int4, apply random rotation before quantization.
    /// </summary>
    public bool EnableRotate { get; init; } = ZVecDefaults.Quantizer.EnableRotate;
}
