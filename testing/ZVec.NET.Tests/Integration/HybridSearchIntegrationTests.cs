using FluentAssertions;
using ZVec.NET.Query;

namespace ZVec.NET.Tests.Integration;

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
            Fields =
            [
                new ZVecFieldSchema { Name = "category", DataType = ZVecDataType.String }
            ],
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
                    Name = "sparse1",
                    DataType = ZVecDataType.SparseVectorFp32,
                    Dimension = 64,
                    IndexParam = new ZVecFlatIndexParam { MetricType = ZVecMetricType.Ip }
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
    public void Test_Hybrid_Dense_Sparse_Filter_Rerank()
    {
        Setup();
        _collection.Should().NotBeNull();

        var docs = new[]
        {
            ZVecDoc.Create("doc1",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
                {
                    ["vector1"] = new float[] { 1.0f, 0.0f, 0.0f, 0.0f }
                },
                sparseVectors: new Dictionary<string, IReadOnlyDictionary<int, float>>
                {
                    ["sparse1"] = new Dictionary<int, float> { [0] = 1.0f, [3] = 0.5f }
                },
                fields: new Dictionary<string, object> { ["category"] = "keep" }),
            ZVecDoc.Create("doc2",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
                {
                    ["vector1"] = new float[] { 0.0f, 1.0f, 0.0f, 0.0f }
                },
                sparseVectors: new Dictionary<string, IReadOnlyDictionary<int, float>>
                {
                    ["sparse1"] = new Dictionary<int, float> { [1] = 1.0f, [4] = 0.5f }
                },
                fields: new Dictionary<string, object> { ["category"] = "drop" }),
            ZVecDoc.Create("doc3",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
                {
                    ["vector1"] = new float[] { 0.9f, 0.1f, 0.0f, 0.0f }
                },
                sparseVectors: new Dictionary<string, IReadOnlyDictionary<int, float>>
                {
                    ["sparse1"] = new Dictionary<int, float> { [0] = 0.8f, [3] = 0.4f }
                },
                fields: new Dictionary<string, object> { ["category"] = "keep" })
        };

        var insertResult = _collection!.Insert(docs);
        insertResult.IsSuccess.Should().BeTrue();

        var denseQ = new ZVecQuery
        {
            FieldName = "vector1",
            Vector = new float[] { 1.0f, 0.0f, 0.0f, 0.0f }
        };
        var sparseQ = new ZVecQuery
        {
            FieldName = "sparse1",
            SparseVector = new Dictionary<int, float> { [0] = 1.0f, [3] = 0.5f }
        };

        var filter = ZVecFilterBuilder.Create()
            .Where("category", ZVecCompareOp.Eq, "keep")
            .Build();

        var reranker = new ZVecRrfReranker { TopN = 5, RankConstant = ZVecDefaults.Rerank.RankConstant };
        var results = _collection.Query([denseQ, sparseQ], topk: 5, reranker: reranker, filter: filter);

        results.Should().NotBeEmpty();
        results.Select(r => r.Id).Should().NotContain("doc2");
        results.Select(r => r.Id).Should().Contain("doc1");
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
