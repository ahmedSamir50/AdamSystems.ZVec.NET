using BenchmarkDotNet.Attributes;

namespace ZVec.NET.Benchmarks;

/// <summary>US-E19.1 / US-E20.4 — GC allocation per 768-dim query (target &lt; 256 B).</summary>
[MemoryDiagnoser]
public class MemoryDiagnosisBench
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

        _tempPath = Path.Combine(Path.GetTempPath(), $"zvec_bench_memory_{Guid.NewGuid():N}");
        _collection = _factory.CreateAndOpen(_tempPath, BenchmarkEnvironment.CreateSchema("memory_bench"));

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

    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_768Dim()
    {
        if (!_factory.IsInitialized)
            return [];

        return _collection.Query(
            new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = _queryVector },
            topk: ZVecDefaults.Query.Topk);
    }

    [Benchmark]
    public ZVecDoc? Fetch_ScalarOnly()
    {
        if (!_factory.IsInitialized)
            return null;

        return _collection.Fetch("seed_0000", includeVector: false);
    }
}
