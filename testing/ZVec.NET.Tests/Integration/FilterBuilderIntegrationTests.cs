using FluentAssertions;
using ZVec.NET.Query;

namespace ZVec.NET.Tests.Integration;

public class FilterBuilderIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public FilterBuilderIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_filter_builder_{Guid.NewGuid():N}");
    }

    private void Setup()
    {
        _fixture.SkipIfNotAvailable();

        var schema = new ZVecCollectionSchemaBuilder("filter_builder_integration")
            .AddField("name", ZVecDataType.String)
            .AddField("age", ZVecDataType.Int32)
            .AddField("active", ZVecDataType.Bool)
            .AddField("status", ZVecDataType.String)
            .AddVector("embedding", ZVecDataType.VectorFp32, 4, new ZVecHnswIndexParam())
            .Build();

        _factory = new ZVecFactory();
        _factory.Initialize();
        _collection = _factory.CreateAndOpen(_testPath, schema);

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var docs = new[]
        {
            ZVecDoc.Create("doc1",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
                fields: new Dictionary<string, object>
                {
                    ["name"] = "Alice",
                    ["age"] = 25,
                    ["active"] = true,
                    ["status"] = "open"
                }),
            ZVecDoc.Create("doc2",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
                fields: new Dictionary<string, object>
                {
                    ["name"] = "O'Brien",
                    ["age"] = 40,
                    ["active"] = false,
                    ["status"] = "closed"
                }),
            ZVecDoc.Create("doc3",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
                fields: new Dictionary<string, object>
                {
                    ["name"] = "Bob",
                    ["age"] = 17,
                    ["active"] = true,
                    ["status"] = "pending"
                })
        };

        var insert = _collection.Insert(docs);
        insert.IsSuccess.Should().BeTrue();
    }

    private IReadOnlyList<ZVecDoc> QueryWithFilter(string filter)
    {
        var query = new ZVecQuery
        {
            FieldName = "embedding",
            Vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
        };
        return _collection!.Query(query, topk: 10, filter: filter);
    }

    [Fact]
    public void FilterBuilder_WhereAgeGt_FiltersHitsAgainstEngine()
    {
        Setup();

        var filter = ZVecFilterBuilder.Create()
            .Where("age", ZVecCompareOp.Gt, 18)
            .ToString();

        var results = QueryWithFilter(filter);
        results.Select(d => d.Id).Should().BeEquivalentTo(["doc1", "doc2"]);
    }

    [Fact]
    public void FilterBuilder_And_FiltersHitsAgainstEngine()
    {
        Setup();

        var filter = ZVecFilterBuilder.Create()
            .Where("age", ZVecCompareOp.Gt, 18)
            .And(ZVecFilterBuilder.Create().Where("active", ZVecCompareOp.Eq, true))
            .ToString();

        var results = QueryWithFilter(filter);
        results.Select(d => d.Id).Should().BeEquivalentTo(["doc1"]);
    }

    [Fact]
    public void FilterBuilder_Or_FiltersHitsAgainstEngine()
    {
        Setup();

        var filter = ZVecFilterBuilder.Create()
            .Where("age", ZVecCompareOp.Lt, 18)
            .Or(ZVecFilterBuilder.Create().Where("age", ZVecCompareOp.Ge, 40))
            .ToString();

        var results = QueryWithFilter(filter);
        results.Select(d => d.Id).Should().BeEquivalentTo(["doc2", "doc3"]);
    }

    [Fact]
    public void FilterBuilder_Not_FiltersHitsAgainstEngine()
    {
        Setup();

        var filter = ZVecFilterBuilder.Create()
            .Not(ZVecFilterBuilder.Create().Where("active", ZVecCompareOp.Eq, true))
            .ToString();

        var results = QueryWithFilter(filter);
        results.Select(d => d.Id).Should().BeEquivalentTo(["doc2"]);
    }

    [Fact]
    public void FilterBuilder_In_FiltersHitsAgainstEngine()
    {
        Setup();

        var filter = ZVecFilterBuilder.Create()
            .In("status", "open", "pending")
            .ToString();

        var results = QueryWithFilter(filter);
        results.Select(d => d.Id).Should().BeEquivalentTo(["doc1", "doc3"]);
    }

    [Fact]
    public void FilterBuilder_Like_FiltersHitsAgainstEngine()
    {
        Setup();

        var filter = ZVecFilterBuilder.Create()
            .Like("name", "Al%")
            .ToString();

        var results = QueryWithFilter(filter);
        results.Select(d => d.Id).Should().BeEquivalentTo(["doc1"]);
    }

    [Fact]
    public void FilterBuilder_EscapedString_AcceptedByEngine()
    {
        Setup();

        var filter = ZVecFilterBuilder.Create()
            .Where("name", ZVecCompareOp.Eq, "O'Brien")
            .ToString();

        var results = QueryWithFilter(filter);
        results.Select(d => d.Id).Should().BeEquivalentTo(["doc2"]);
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
