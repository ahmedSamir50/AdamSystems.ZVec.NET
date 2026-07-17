using BenchmarkDotNet.Attributes;

namespace ZVec.NET.Benchmarks;

/// <summary>
/// Sync query latency on the local binding suite (10k Flat).
/// Upstream engine scale (VectorDBBench Cohere 1M/10M, homepage 8500+ QPS) is
/// <see cref="UpstreamEngineScaleBaseline"/> — not comparable 1:1 to these means.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class QueryThroughputBench
{
    private ZVecFactory _factory = null!;
    private IZvecCollection _collection = null!;
    private IZvecCollection _tinyCollection = null!;
    private ReadOnlyMemory<float> _queryVector = default;
    private string _tempPath = null!;
    private string _tinyTempPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (!BenchmarkEnvironment.TryInitialize(out _factory))
            return;

        var vector = BenchmarkEnvironment.CreateVector();
        _queryVector = vector;

        _tempPath = Path.Combine(Path.GetTempPath(), $"zvec_bench_query_{Guid.NewGuid():N}");
        _collection = _factory.CreateAndOpen(_tempPath, BenchmarkEnvironment.CreateSchema("query_bench"));
        BenchmarkEnvironment.SeedCollection(_collection, vector, BenchmarkEnvironment.SeedDocCount);

        _tinyTempPath = Path.Combine(Path.GetTempPath(), $"zvec_bench_query_tiny_{Guid.NewGuid():N}");
        _tinyCollection = _factory.CreateAndOpen(_tinyTempPath, BenchmarkEnvironment.CreateSchema("query_tiny"));
        BenchmarkEnvironment.SeedCollection(_tinyCollection, vector, BenchmarkEnvironment.TinyCorpusSeedCount);

        // Warm native path once before timed iterations.
        _ = _collection.Query(
            new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = _queryVector },
            topk: ZVecDefaults.Query.Topk);
        _ = _tinyCollection.Query(
            new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = _queryVector },
            topk: ZVecDefaults.Query.Topk);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        DestroyCollection(ref _collection);
        DestroyCollection(ref _tinyCollection);
        _factory?.Shutdown();
        TryDeleteDir(_tempPath);
        TryDeleteDir(_tinyTempPath);
    }

    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_Sync()
    {
        if (!_factory.IsInitialized)
            return [];

        return _collection.Query(
            new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = _queryVector },
            topk: ZVecDefaults.Query.Topk);
    }

    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_WithFilter()
    {
        if (!_factory.IsInitialized)
            return [];

        return _collection.Query(
            new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = _queryVector },
            topk: ZVecDefaults.Query.Topk,
            filter: BenchmarkEnvironment.SampleFilter());
    }

    /// <summary>
    /// Warm query on a tiny Flat corpus — proxy for “sync call overhead”, not a no-op P/Invoke stub.
    /// </summary>
    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_WarmTinyCorpus()
    {
        if (!_factory.IsInitialized)
            return [];

        return _tinyCollection.Query(
            new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = _queryVector },
            topk: ZVecDefaults.Query.Topk);
    }

    private static void DestroyCollection(ref IZvecCollection collection)
    {
        try { collection.Destroy(); } catch { /* ignore */ }
    }

    private static void TryDeleteDir(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { /* ignore */ }
    }
}
