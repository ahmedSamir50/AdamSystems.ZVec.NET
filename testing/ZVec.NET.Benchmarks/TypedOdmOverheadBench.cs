using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using ZVec.NET.Mapping;
using ZVec.NET.Query;

namespace ZVec.NET.Benchmarks;

/// <summary>
/// Compares dynamic <see cref="ZVecDoc"/> paths vs typed <see cref="IZvecCollection{T}"/> overhead.
/// Existing query/insert throughput benches remain the primary latency suite (ZVecDoc-only).
/// </summary>
[MemoryDiagnoser]
[RankColumn]
[SimpleJob(warmupCount: 1, iterationCount: 5)]
public class TypedOdmOverheadBench
{
    private ZVecFactory _factory = null!;
    private IZvecCollection _dynamic = null!;
    private IZvecCollection<BenchDoc> _typed = null!;
    private ZVecQuery _query = null!;
    private float[] _vector = null!;
    private BenchDoc _record = null!;
    private ZVecDoc _doc = null!;
    private Expression<Func<BenchDoc, bool>> _filterExpr = null!;
    private string _filterString = null!;
    private string _tempPath = null!;
    private int _insertCursor;

    [GlobalSetup]
    public void Setup()
    {
        if (!BenchmarkEnvironment.TryInitialize(out _factory))
            return;

        _vector = BenchmarkEnvironment.CreateVector();
        _query = new ZVecQuery { FieldName = "Embedding", Vector = _vector };
        _filterString = ZVecFilterBuilder.Create()
            .Where("Content", ZVecCompareOp.Eq, "seed")
            .Build();
        _filterExpr = d => d.Content == "seed";

        _tempPath = Path.Combine(Path.GetTempPath(), $"zvec_bench_typed_{Guid.NewGuid():N}");
        var schema = ZVecCollectionSchemaBuilder.From<BenchDoc>().Build();
        _dynamic = _factory.CreateAndOpen(_tempPath, schema);
        _typed = new ZVecCollection<BenchDoc>(_dynamic);

        for (var i = 0; i < BenchmarkEnvironment.TinyCorpusSeedCount; i++)
        {
            _typed.Insert(new BenchDoc
            {
                Id = $"s{i:D4}",
                Content = "seed",
                Embedding = _vector
            });
        }

        _record = new BenchDoc
        {
            Id = "probe",
            Content = "seed",
            Embedding = _vector
        };
        _doc = ZVecMapper.ToDoc(_record);

        _ = _dynamic.Query(_query, topk: ZVecDefaults.Query.Topk, includeVector: false);
        _ = _typed.Query(d => d.Embedding, _vector, topK: ZVecDefaults.Query.Topk, includeVector: false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try { _dynamic?.Destroy(); } catch { /* ignore */ }
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
    public ZVecStatus Insert_Dynamic()
    {
        if (!_factory.IsInitialized)
            return new ZVecStatus { Code = ZVecErrorCode.Ok };

        var id = $"d{Interlocked.Increment(ref _insertCursor):D6}";
        return _dynamic.Insert(ZVecDoc.Create(
            id,
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["Embedding"] = _vector },
            fields: new Dictionary<string, object> { ["Content"] = "seed" }));
    }

    [Benchmark]
    public ZVecStatus Insert_Typed()
    {
        if (!_factory.IsInitialized)
            return new ZVecStatus { Code = ZVecErrorCode.Ok };

        var id = $"t{Interlocked.Increment(ref _insertCursor):D6}";
        return _typed.Insert(new BenchDoc
        {
            Id = id,
            Content = "seed",
            Embedding = _vector
        });
    }

    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_Dynamic()
    {
        if (!_factory.IsInitialized)
            return [];

        return _dynamic.Query(_query, topk: ZVecDefaults.Query.Topk, includeVector: false);
    }

    [Benchmark]
    public IReadOnlyList<ZVecHit<BenchDoc>> Query_Typed()
    {
        if (!_factory.IsInitialized)
            return [];

        return _typed.Query(d => d.Embedding, _vector, topK: ZVecDefaults.Query.Topk, includeVector: false);
    }

    [Benchmark]
    public IReadOnlyList<ZVecDoc> QueryFilter_Dynamic()
    {
        if (!_factory.IsInitialized)
            return [];

        return _dynamic.Query(_query, topk: ZVecDefaults.Query.Topk, filter: _filterString, includeVector: false);
    }

    [Benchmark]
    public IReadOnlyList<ZVecHit<BenchDoc>> QueryFilter_Typed()
    {
        if (!_factory.IsInitialized)
            return [];

        return _typed.Query(d => d.Embedding, _vector, topK: ZVecDefaults.Query.Topk, filter: _filterExpr, includeVector: false);
    }

    [Benchmark]
    public ZVecDoc Mapper_ToDoc() => ZVecMapper.ToDoc(_record);

    [Benchmark]
    public BenchDoc Mapper_FromDoc() => ZVecMapper.FromDoc<BenchDoc>(_doc);

    [Benchmark]
    public string ExpressionFilter_Translate() => ZVecExpressionFilter.Translate(_filterExpr);

    [ZVecCollection("typed_bench")]
    public sealed class BenchDoc
    {
        public string Id { get; set; } = "";
        public string Content { get; set; } = "";
        [ZVecVector(768, Index = ZVecIndexType.Flat)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
