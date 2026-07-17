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
                    IndexParam = new ZVecFlatIndexParam { MetricType = ZVecDefaults.Flat.MetricType }
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

        const int docCount = 1000;
        var docs = new ZVecDoc[docCount];
        for (int i = 0; i < docCount; i++)
        {
            float v = i / (float)(docCount - 1);
            docs[i] = ZVecDoc.Create($"doc{i:D4}",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
                {
                    ["embedding"] = new float[] { v, v, v, v }
                });
        }

        var insertResult = _collection!.Insert(docs);
        insertResult.IsSuccess.Should().BeTrue();

        var queryVector = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        var query = new ZVecQuery { FieldName = "embedding", Vector = queryVector };

        var results = _collection.Query(query, topk: ZVecDefaults.Query.Topk);

        results.Should().HaveCount(ZVecDefaults.Query.Topk);
        results[0].Id.Should().Be("doc0999");
        results[1].Id.Should().Be("doc0998");
        results[2].Id.Should().Be("doc0997");
        for (int i = 1; i < results.Count; i++)
        {
            int prev = int.Parse(results[i - 1].Id!["doc".Length..]);
            int curr = int.Parse(results[i].Id!["doc".Length..]);
            curr.Should().BeLessThan(prev, "L2 ranking should return decreasing doc indices (closer vectors first)");
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
