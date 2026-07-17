using BenchmarkDotNet.Attributes;

namespace ZVec.NET.Benchmarks;

/// <summary>
/// Sync query latency on the local binding suite (10k Flat).
/// Primary latency benches use <c>includeVector: false</c> (search + id/score/scalars).
/// <see cref="Query_Sync_WithVectors"/> documents the full materialization path.
/// Upstream engine scale is <see cref="UpstreamEngineScaleBaseline"/>.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class QueryThroughputBench
{
    private ZVecFactory _factory = null!;
    private IZvecCollection _collection = null!;
    private IZvecCollection _tinyCollection = null!;
    private ZVecQuery _query = null!;
    private string _tempPath = null!;
    private string _tinyTempPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (!BenchmarkEnvironment.TryInitialize(out _factory))
            return;

        var vector = BenchmarkEnvironment.CreateVector();
        _query = new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = vector };

        _tempPath = Path.Combine(Path.GetTempPath(), $"zvec_bench_query_{Guid.NewGuid():N}");
        _collection = _factory.CreateAndOpen(_tempPath, BenchmarkEnvironment.CreateSchema("query_bench"));
        BenchmarkEnvironment.SeedCollection(_collection, vector, BenchmarkEnvironment.SeedDocCount);

        _tinyTempPath = Path.Combine(Path.GetTempPath(), $"zvec_bench_query_tiny_{Guid.NewGuid():N}");
        _tinyCollection = _factory.CreateAndOpen(_tinyTempPath, BenchmarkEnvironment.CreateSchema("query_tiny"));
        BenchmarkEnvironment.SeedCollection(_tinyCollection, vector, BenchmarkEnvironment.TinyCorpusSeedCount);

        _ = _collection.Query(_query, topk: ZVecDefaults.Query.Topk, includeVector: false);
        _ = _tinyCollection.Query(_query, topk: ZVecDefaults.Query.Topk, includeVector: false);
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

    /// <summary>Primary 10k Flat latency (no result vectors).</summary>
    [Benchmark(Baseline = true)]
    public IReadOnlyList<ZVecDoc> Query_Sync()
    {
        if (!_factory.IsInitialized)
            return [];

        return _collection.Query(_query, topk: ZVecDefaults.Query.Topk, includeVector: false);
    }

    /// <summary>Full materialization path (topk vectors copied) — documents ~44 KB alloc floor.</summary>
    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_Sync_WithVectors()
    {
        if (!_factory.IsInitialized)
            return [];

        return _collection.Query(_query, topk: ZVecDefaults.Query.Topk, includeVector: true);
    }

    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_WithFilter()
    {
        if (!_factory.IsInitialized)
            return [];

        return _collection.Query(
            _query,
            topk: ZVecDefaults.Query.Topk,
            filter: BenchmarkEnvironment.SampleFilter(),
            includeVector: false);
    }

    /// <summary>
    /// Warm query on a tiny Flat corpus — binding/search overhead with includeVector=false.
    /// </summary>
    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_WarmTinyCorpus()
    {
        if (!_factory.IsInitialized)
            return [];

        return _tinyCollection.Query(_query, topk: ZVecDefaults.Query.Topk, includeVector: false);
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
