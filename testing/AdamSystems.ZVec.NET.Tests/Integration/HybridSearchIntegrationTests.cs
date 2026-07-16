using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Integration;

public class HybridSearchIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private readonly ZVecCollectionSchema _schema;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public HybridSearchIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_hybrid_integration_{Guid.NewGuid():N}");
        _schema = new ZVecCollectionSchema
        {
            Name = "hybrid_integration",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "vector1",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecHnswIndexParam { MetricType = ZVecMetricType.Cosine }
                },
                new ZVecVectorSchema
                {
                    Name = "vector2",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecHnswIndexParam { MetricType = ZVecMetricType.Cosine }
                }
            ]
        };
    }

    private void Setup()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();
        _collection = _factory.CreateAndOpen(_testPath, _schema);
    }

    [Fact]
    public void Test_Hybrid_MultiVector_Weighted_Rerank()
    {
        Setup();
        _collection.Should().NotBeNull();

        // Insert documents with 2 vectors
        var docs = new[]
        {
            ZVecDoc.Create("doc1", denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
            {
                ["vector1"] = new float[] { 1.0f, 0.0f, 0.0f, 0.0f },
                ["vector2"] = new float[] { 0.0f, 1.0f, 0.0f, 0.0f }
            }),
            ZVecDoc.Create("doc2", denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
            {
                ["vector1"] = new float[] { 0.0f, 1.0f, 0.0f, 0.0f },
                ["vector2"] = new float[] { 1.0f, 0.0f, 0.0f, 0.0f }
            })
        };

        var insertResult = _collection!.Insert(docs);
        insertResult.IsSuccess.Should().BeTrue();

        // Query both fields
        var q1 = new ZVecQuery { FieldName = "vector1", Vector = new float[] { 1.0f, 0.0f, 0.0f, 0.0f } };
        var q2 = new ZVecQuery { FieldName = "vector2", Vector = new float[] { 1.0f, 0.0f, 0.0f, 0.0f } };

        var reranker = new ZVecWeightedReranker
        {
            TopN = 2,
            Metric = ZVecMetricType.Cosine,
            Weights = new Dictionary<string, float>
            {
                ["vector1"] = 0.8f,
                ["vector2"] = 0.2f
            }
        };

        var results = _collection.Query([q1, q2], topk: 2, reranker: reranker);

        results.Should().HaveCount(2);
        // doc1 matches vector1 (weight 0.8) and doc2 matches vector2 (weight 0.2)
        // doc2 matches vector1 (cosine similarity 0) and doc1 matches vector2 (cosine similarity 0)
        // So doc1 should score higher than doc2
        results[0].Id.Should().Be("doc1");
        results[1].Id.Should().Be("doc2");
    }

    public void Dispose()
    {
        _collection?.Dispose();
        _factory?.Dispose();
        if (Directory.Exists(_testPath))
        {
            try
            {
                Directory.Delete(_testPath, true);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }
    }
}
