using FluentAssertions;

namespace ZVec.NET.Tests.Integration;

public class SchemaBuilderIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public SchemaBuilderIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_schema_builder_{Guid.NewGuid():N}");
    }

    private void Setup(ZVecCollectionSchema schema)
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();
        _collection = _factory.CreateAndOpen(_testPath, schema);
    }

    [Fact]
    public void SchemaBuilder_Build_CreateAndOpen_InsertQueryFetch_Succeeds()
    {
        var schema = new ZVecCollectionSchemaBuilder("schema_builder_integration")
            .AddField("title", ZVecDataType.String)
            .AddField("year", ZVecDataType.Int32, nullable: true,
                index: new ZVecInvertIndexParam { EnableRangeOptimization = true })
            .AddVector("embedding", ZVecDataType.VectorFp32, 4,
                new ZVecHnswIndexParam { MetricType = ZVecMetricType.Cosine })
            .WithMaxDocCountPerSegment(100_000)
            .Build();

        Setup(schema);
        _collection.Should().NotBeNull();

        schema.MaxDocCountPerSegment.Should().Be(100_000);
        schema.Fields.Should().HaveCount(2);
        schema.Vectors.Should().HaveCount(1);

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var doc = ZVecDoc.Create("doc1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["title"] = "Hello", ["year"] = 2024 });

        var insert = _collection!.Insert(doc);
        insert.IsSuccess.Should().BeTrue();

        var fetched = _collection.Fetch("doc1", includeVector: true);
        fetched.Should().NotBeNull();
        fetched!.Fields["title"].Should().Be("Hello");
        fetched.Fields["year"].Should().Be(2024);
        fetched.DenseVectors["embedding"].ToArray().Should().BeEquivalentTo(vector);

        var results = _collection.Query(
            new ZVecQuery { FieldName = "embedding", Vector = vector },
            topk: 5);
        results.Should().ContainSingle(r => r.Id == "doc1");
    }

    [Fact]
    public void SchemaBuilder_SchemaOverloads_CreateAndOpen_Succeeds()
    {
        var schema = new ZVecCollectionSchemaBuilder("schema_overloads")
            .AddField(new ZVecFieldSchema { Name = "tag", DataType = ZVecDataType.String })
            .AddVector(new ZVecVectorSchema
            {
                Name = "vec",
                DataType = ZVecDataType.VectorFp32,
                Dimension = 4,
                IndexParam = new ZVecFlatIndexParam()
            })
            .Build();

        Setup(schema);

        var vector = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
        var doc = ZVecDoc.Create("d1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["vec"] = vector },
            fields: new Dictionary<string, object> { ["tag"] = "x" });

        _collection!.Insert(doc).IsSuccess.Should().BeTrue();
        _collection.Fetch("d1")!.Fields["tag"].Should().Be("x");
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
