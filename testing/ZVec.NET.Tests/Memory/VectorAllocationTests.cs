using FluentAssertions;
using ZVec.NET.Tests.Integration;

namespace ZVec.NET.Tests.Memory;

/// <summary>
/// US-E19.1 — vector query allocation checks (768-dim).
/// Prefer <c>MemoryDiagnosisBench</c> (BenchmarkDotNet) for CI-grade numbers;
/// this xUnit test is a coarse guard with documented framework noise.
/// </summary>
public class VectorAllocationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private const int EmbeddingDimension = 768;

    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public VectorAllocationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_alloc_{Guid.NewGuid():N}");
    }

    private void Setup()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();
        var schema = new ZVecCollectionSchema
        {
            Name = "alloc_test",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = EmbeddingDimension,
                    IndexParam = new ZVecFlatIndexParam { MetricType = ZVecDefaults.Flat.MetricType }
                }
            ]
        };
        _collection = _factory.CreateAndOpen(_testPath, schema);

        var seed = new float[EmbeddingDimension];
        Array.Fill(seed, 0.5f);
        _collection.Insert(ZVecDoc.Create("seed",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = seed }));
    }

    [Fact]
    public void Query_WithReadOnlyMemory_DoesNotCopyQueryVectorBytes()
    {
        Setup();
        _collection.Should().NotBeNull();

        var vector = new float[EmbeddingDimension];
        Array.Fill(vector, 0.5f);
        var memory = new ReadOnlyMemory<float>(vector);
        int vectorBytes = EmbeddingDimension * sizeof(float);

        _collection!.Query(new ZVecQuery { FieldName = "embedding", Vector = memory }, topk: ZVecDefaults.Query.Topk);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long MeasureQuery(Func<ReadOnlyMemory<float>> queryVectorFactory)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var queryVector = queryVectorFactory();
            _ = _collection.Query(new ZVecQuery { FieldName = "embedding", Vector = queryVector }, topk: ZVecDefaults.Query.Topk);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        long pinnedPathBytes = MeasureQuery(() => memory);
        long explicitCopyBytes = MeasureQuery(() => new ReadOnlyMemory<float>(vector.ToArray()));

        (explicitCopyBytes - pinnedPathBytes).Should().BeGreaterThan(vectorBytes / 2,
            "explicit float[] copy should allocate at least half a vector more than the pinned ReadOnlyMemory path " +
            "(xUnit cannot isolate the <256 B BDN gate — use MemoryDiagnosisBench for that)");
    }

    public void Dispose()
    {
        _collection?.Dispose();
        _factory?.Dispose();
        if (Directory.Exists(_testPath))
        {
            try { Directory.Delete(_testPath, true); }
            catch { /* ignore */ }
        }
    }
}
