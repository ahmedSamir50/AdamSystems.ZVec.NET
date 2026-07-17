using BenchmarkDotNet.Attributes;

namespace ZVec.NET.Benchmarks;

/// <summary>
/// GC allocation diagnosis. Tier A uses includeVector=false; Tier B documents vector materialization floor.
/// </summary>
[MemoryDiagnoser]
public class MemoryDiagnosisBench
{
    private ZVecFactory _factory = null!;
    private IZvecCollection _collection = null!;
    private ZVecQuery _query = null!;
    private string _tempPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (!BenchmarkEnvironment.TryInitialize(out _factory))
            return;

        _tempPath = Path.Combine(Path.GetTempPath(), $"zvec_bench_memory_{Guid.NewGuid():N}");
        _collection = _factory.CreateAndOpen(_tempPath, BenchmarkEnvironment.CreateSchema("memory_bench"));

        var vector = BenchmarkEnvironment.CreateVector();
        _query = new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = vector };
        BenchmarkEnvironment.SeedCollection(_collection, vector, BenchmarkEnvironment.SeedDocCount);

        _ = _collection.Query(_query, topk: ZVecDefaults.Query.Topk, includeVector: false);
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

    /// <summary>Tier A — search without copying result vectors.</summary>
    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_768Dim()
    {
        if (!_factory.IsInitialized)
            return [];

        return _collection.Query(_query, topk: ZVecDefaults.Query.Topk, includeVector: false);
    }

    /// <summary>Tier B — full materialization (~44 KB floor from topk vectors).</summary>
    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_768Dim_WithVectors()
    {
        if (!_factory.IsInitialized)
            return [];

        return _collection.Query(_query, topk: ZVecDefaults.Query.Topk, includeVector: true);
    }

    [Benchmark]
    public ZVecDoc? Fetch_ScalarOnly()
    {
        if (!_factory.IsInitialized)
            return null;

        return _collection.Fetch("seed_00000", includeVector: false);
    }
}
