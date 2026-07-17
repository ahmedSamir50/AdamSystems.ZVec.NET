namespace ZVec.NET.Benchmarks;

/// <summary>
/// Published upstream VectorDBBench engine-scale figures from official ZVec docs.
/// These are <b>not</b> measured by the local 10k Flat binding suite — they frame
/// engine capacity (Cohere 1M / 10M) vs SDK binding latency/throughput at 10k.
/// Source: https://zvec.org/en/docs/db/benchmarks/
/// </summary>
public static class UpstreamEngineScaleBaseline
{
    public const string BenchmarksDocUrl = "https://zvec.org/en/docs/db/benchmarks/";
    public const string HomepageUrl = "https://zvec.org/en/";

    /// <summary>VectorDBBench case: Cohere 1M × 768-dim.</summary>
    public const string Cohere1MCase = "Performance768D1M";

    /// <summary>VectorDBBench case: Cohere 10M × 768-dim.</summary>
    public const string Cohere10MCase = "Performance768D10M";

    /// <summary>Published homepage claim: QPS on Cohere 10M (engine scale).</summary>
    public const string Cohere10MPublishedQps = "8500+";

    /// <summary>Published homepage claim: index build time for Cohere 10M.</summary>
    public const string Cohere10MPublishedIndexBuild = "~1 hour";

    public static string SummaryLine =>
        $"Upstream VectorDBBench (engine scale, not binding-comparable): " +
        $"{Cohere1MCase} + {Cohere10MCase}; homepage {Cohere10MPublishedQps} QPS @ Cohere 10M. " +
        $"See {BenchmarksDocUrl}";
}
