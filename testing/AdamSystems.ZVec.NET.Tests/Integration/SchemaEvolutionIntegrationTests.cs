using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Integration;

public class SchemaEvolutionIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private readonly ZVecCollectionSchema _schema;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public SchemaEvolutionIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_schema_evolution_{Guid.NewGuid():N}");
        _schema = new ZVecCollectionSchema
        {
            Name = "evolution_integration",
            Vectors = new[]
            {
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecHnswIndexParam()
                }
            }
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
    public void Test_Schema_Evolution_Add_Alter_Drop_Column()
    {
        Setup();
        _collection.Should().NotBeNull();

        // 1. Add Column
        var ratingField = new ZVecFieldSchema { Name = "rating", DataType = ZVecDataType.Float, Nullable = true };
        _collection!.AddColumn(ratingField, "0.0");

        // 2. Insert with rating
        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var doc = ZVecDoc.Create("doc1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["rating"] = 4.5f });

        var insertResult = _collection.Insert(doc);
        insertResult.IsSuccess.Should().BeTrue();

        // 3. Fetch and verify rating
        var fetched = _collection.Fetch("doc1", includeVector: false);
        fetched.Should().NotBeNull();
        fetched!.Fields.Should().ContainKey("rating");
        Convert.ToSingle(fetched.Fields["rating"]).Should().Be(4.5f);

        // 4. Alter column (rename rating to stars)
        _collection.AlterColumn("rating", newName: "stars");

        // 5. Fetch and verify renamed column
        var fetchedStars = _collection.Fetch("doc1", includeVector: false);
        fetchedStars.Should().NotBeNull();
        fetchedStars!.Fields.Should().ContainKey("stars");
        fetchedStars.Fields.Should().NotContainKey("rating");
        Convert.ToSingle(fetchedStars.Fields["stars"]).Should().Be(4.5f);

        // 6. Drop column stars
        _collection.DropColumn("stars");

        // 7. Fetch and verify dropped column is gone
        var fetchedAfterDrop = _collection.Fetch("doc1", includeVector: false);
        fetchedAfterDrop.Should().NotBeNull();
        fetchedAfterDrop!.Fields.Should().NotContainKey("stars");
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
