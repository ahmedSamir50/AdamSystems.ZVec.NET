using BenchmarkDotNet.Attributes;

namespace ZVec.NET.Benchmarks;

/// <summary>
/// Surfaces published upstream VectorDBBench engine-scale figures alongside the
/// local binding suite. These methods do <b>not</b> download Cohere 1M/10M or run
/// VectorDBBench — they document the official published baseline for README /
/// report correlation. Local QPS is derived from <see cref="QueryThroughputBench"/>.
/// </summary>
[MemoryDiagnoser]
public class EngineScaleReferenceBench
{
    private ZVecFactory _factory = null!;
    private IZvecCollection _collection = null!;
    private ReadOnlyMemory<float> _queryVector = default;
    private string _tempPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine(UpstreamEngineScaleBaseline.SummaryLine);
        Console.WriteLine(
            $"Published: {UpstreamEngineScaleBaseline.Cohere10MCase} → " +
            $"{UpstreamEngineScaleBaseline.Cohere10MPublishedQps} QPS, " +
            $"index build {UpstreamEngineScaleBaseline.Cohere10MPublishedIndexBuild}. " +
            $"Also: {UpstreamEngineScaleBaseline.Cohere1MCase}. Docs: {UpstreamEngineScaleBaseline.BenchmarksDocUrl}");

        if (!BenchmarkEnvironment.TryInitialize(out _factory))
            return;

        _tempPath = Path.Combine(Path.GetTempPath(), $"zvec_bench_engine_ref_{Guid.NewGuid():N}");
        _collection = _factory.CreateAndOpen(_tempPath, BenchmarkEnvironment.CreateSchema("engine_ref"));
        var vector = BenchmarkEnvironment.CreateVector();
        _queryVector = vector;
        BenchmarkEnvironment.SeedCollection(_collection, vector, BenchmarkEnvironment.SeedDocCount);
        _ = _collection.Query(
            new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = _queryVector },
            topk: ZVecDefaults.Query.Topk);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_collection is not null)
        {
            try { _collection.Destroy(); } catch { /* ignore */ }
        }

        _factory?.Shutdown();

        if (!string.IsNullOrEmpty(_tempPath))
        {
            try
            {
                if (Directory.Exists(_tempPath))
                    Directory.Delete(_tempPath, true);
            }
            catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Local binding-suite query (10k Flat) — report Mean and convert to QPS in README
    /// next to published Cohere 10M 8500+ QPS.
    /// </summary>
    [Benchmark(Description = "Local_10k_Flat_Query_vs_Upstream_8500plus_QPS")]
    public IReadOnlyList<ZVecDoc> Local_10k_Query_ForEngineScaleContext()
    {
        if (!_factory.IsInitialized)
            return [];

        return _collection.Query(
            new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = _queryVector },
            topk: ZVecDefaults.Query.Topk);
    }
}
