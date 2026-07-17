using BenchmarkDotNet.Attributes;

namespace ZVec.NET.Benchmarks;

[MemoryDiagnoser]
public class VectorMarshallingBench
{
    private ZVecFactory _factory = null!;
    private IZvecCollection _collection = null!;
    private ReadOnlyMemory<float> _queryMemory = default;
    private float[] _queryArray = null!;
    private string _tempPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (!BenchmarkEnvironment.TryInitialize(out _factory))
            return;

        _tempPath = Path.Combine(Path.GetTempPath(), $"zvec_bench_marshal_{Guid.NewGuid():N}");
        _collection = _factory.CreateAndOpen(_tempPath, BenchmarkEnvironment.CreateSchema("marshal_bench"));

        _queryArray = BenchmarkEnvironment.CreateVector();
        _queryMemory = _queryArray;
        BenchmarkEnvironment.SeedCollection(_collection, _queryArray, BenchmarkEnvironment.SeedDocCount);
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

    [Benchmark(Baseline = true)]
    public IReadOnlyList<ZVecDoc> Query_ReadOnlyMemory()
    {
        if (!_factory.IsInitialized)
            return [];

        return _collection.Query(
            new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = _queryMemory },
            topk: ZVecDefaults.Query.Topk);
    }

    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_ExplicitCopy()
    {
        if (!_factory.IsInitialized)
            return [];

        float[] copy = _queryArray.ToArray();
        return _collection.Query(
            new ZVecQuery { FieldName = BenchmarkEnvironment.VectorField, Vector = copy },
            topk: ZVecDefaults.Query.Topk);
    }
}
