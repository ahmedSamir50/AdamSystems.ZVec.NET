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

        // ≥100 docs with known distances from the query vector (ascending scale).
        const int docCount = 100;
        var docs = new ZVecDoc[docCount];
        for (int i = 0; i < docCount; i++)
        {
            float v = i / (float)(docCount - 1); // 0 .. 1
            docs[i] = ZVecDoc.Create($"doc{i:D3}",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
                {
                    ["embedding"] = new float[] { v, v, v, v }
                });
        }

        var insertResult = _collection!.Insert(docs);
        insertResult.IsSuccess.Should().BeTrue();

        // Query near the high end — closest should be doc099, then doc098, ...
        var queryVector = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        var query = new ZVecQuery { FieldName = "embedding", Vector = queryVector };

        var results = _collection.Query(query, topk: 10);

        results.Should().HaveCount(10);
        results[0].Id.Should().Be("doc099");
        results[1].Id.Should().Be("doc098");
        results[2].Id.Should().Be("doc097");
        for (int i = 1; i < results.Count; i++)
        {
            // Scores for L2: lower is closer — ensure non-decreasing distance order via Id rank.
            int prev = int.Parse(results[i - 1].Id!["doc".Length..]);
            int curr = int.Parse(results[i].Id!["doc".Length..]);
            curr.Should().BeLessThan(prev);
        }
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
