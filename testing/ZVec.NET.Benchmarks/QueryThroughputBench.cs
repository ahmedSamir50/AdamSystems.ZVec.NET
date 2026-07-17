using BenchmarkDotNet.Attributes;

namespace ZVec.NET.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class QueryThroughputBench
{
    private ZVecFactory _factory = null!;
    private IZvecCollection _collection = null!;
    private ReadOnlyMemory<float> _queryVector = default;
    private string _tempPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (!BenchmarkEnvironment.TryInitialize(out _factory))
            return;

        _tempPath = Path.Combine(Path.GetTempPath(), $"zvec_bench_query_{Guid.NewGuid():N}");
        _collection = _factory.CreateAndOpen(_tempPath, BenchmarkEnvironment.CreateSchema("query_bench"));

        var vector = BenchmarkEnvironment.CreateVector();
        _queryVector = vector;
        BenchmarkEnvironment.SeedCollection(_collection, vector, BenchmarkEnvironment.SeedDocCount);
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
}
