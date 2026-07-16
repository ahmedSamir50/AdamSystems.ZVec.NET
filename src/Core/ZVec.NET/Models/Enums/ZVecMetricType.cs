namespace ZVec.NET;

/// <summary>Maps to <c>zvec::MetricType</c> / <c>ZVEC_METRIC_TYPE_*</c>.</summary>
public enum ZVecMetricType
{
    Undefined = 0,
    L2 = 1,
    Ip = 2,
    Cosine = 3,
    MipsL2 = 4
}
