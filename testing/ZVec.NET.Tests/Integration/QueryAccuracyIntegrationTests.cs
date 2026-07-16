using FluentAssertions;

namespace ZVec.NET.Tests.Integration;

public class QueryAccuracyIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private readonly ZVecCollectionSchema _schema;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public QueryAccuracyIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_query_accuracy_{Guid.NewGuid():N}");
        _schema = new ZVecCollectionSchema
        {
            Name = "accuracy_integration",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecHnswIndexParam { MetricType = ZVecMetricType.L2 }
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
    public void Test_Query_Sorting_Accuracy()
    {
        Setup();
        _collection.Should().NotBeNull();

        var docs = new[]
        {
            ZVecDoc.Create("doc1", denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = new float[] { 0.1f, 0.1f, 0.1f, 0.1f } }),
            ZVecDoc.Create("doc2", denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = new float[] { 0.9f, 0.9f, 0.9f, 0.9f } }),
            ZVecDoc.Create("doc3", denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = new float[] { 0.5f, 0.5f, 0.5f, 0.5f } }),
            ZVecDoc.Create("doc4", denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = new float[] { 0.2f, 0.2f, 0.2f, 0.2f } }),
            ZVecDoc.Create("doc5", denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = new float[] { 0.8f, 0.8f, 0.8f, 0.8f } }),
        };

        var insertResult = _collection!.Insert(docs);
        insertResult.IsSuccess.Should().BeTrue();

        // Query with vector near doc2
        var queryVector = new float[] { 0.95f, 0.95f, 0.95f, 0.95f };
        var query = new ZVecQuery { FieldName = "embedding", Vector = queryVector };

        var results = _collection.Query(query, topk: 5);

        results.Should().HaveCount(5);
        results[0].Id.Should().Be("doc2");
        results[1].Id.Should().Be("doc5");
        results[2].Id.Should().Be("doc3");
        results[3].Id.Should().Be("doc4");
        results[4].Id.Should().Be("doc1");
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
