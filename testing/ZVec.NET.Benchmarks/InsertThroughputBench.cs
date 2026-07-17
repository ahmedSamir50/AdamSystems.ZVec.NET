using BenchmarkDotNet.Attributes;

namespace ZVec.NET.Benchmarks;

[MemoryDiagnoser]
public class InsertThroughputBench
{
    private ZVecFactory _factory = null!;
    private IZvecCollection _collection = null!;
    private ZVecDoc[] _batch = null!;
    private int _batchCursor;
    private string _tempPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (!BenchmarkEnvironment.TryInitialize(out _factory))
            return;

        _tempPath = Path.Combine(Path.GetTempPath(), $"zvec_bench_insert_{Guid.NewGuid():N}");
        _collection = _factory.CreateAndOpen(_tempPath, BenchmarkEnvironment.CreateSchema("insert_bench"));

        var vector = BenchmarkEnvironment.CreateVector();
        _batch = new ZVecDoc[BenchmarkEnvironment.BatchInsertSize];
        for (int i = 0; i < _batch.Length; i++)
            _batch[i] = BenchmarkEnvironment.CreateDoc($"batch_{i:D4}", vector, $"batch document {i}");
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
    public ZVecStatus Insert_Single()
    {
        if (!_factory.IsInitialized)
            return new ZVecStatus { Code = ZVecErrorCode.Ok };

        var vector = BenchmarkEnvironment.CreateVector(fill: 0.25f);
        var doc = BenchmarkEnvironment.CreateDoc($"single_{Interlocked.Increment(ref _batchCursor):D6}", vector, "single insert");
        return _collection.Insert(doc);
    }

    [Benchmark]
    public ZVecStatus Insert_Batch()
    {
        if (!_factory.IsInitialized)
            return new ZVecStatus { Code = ZVecErrorCode.Ok };

        int batchId = Interlocked.Increment(ref _batchCursor);
        var vector = BenchmarkEnvironment.CreateVector();
        for (int i = 0; i < _batch.Length; i++)
            _batch[i] = BenchmarkEnvironment.CreateDoc($"b{batchId}_{i:D3}", vector, $"batch {batchId}-{i}");

        return _collection.Insert(_batch);
    }
}
