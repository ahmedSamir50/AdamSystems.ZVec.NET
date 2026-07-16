using FluentAssertions;

namespace ZVec.NET.Tests.Integration;

public class CrudLifecycleIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private readonly ZVecCollectionSchema _schema;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public CrudLifecycleIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_crud_integration_{Guid.NewGuid():N}");
        _schema = new ZVecCollectionSchema
        {
            Name = "crud_integration",
            Fields =
            [
                new ZVecFieldSchema { Name = "name", DataType = ZVecDataType.String },
                new ZVecFieldSchema { Name = "age", DataType = ZVecDataType.Int32 }
            ],
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecHnswIndexParam()
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
    public void Test_Insert_Fetch_Update_Delete_Lifecycle()
    {
        Setup();
        _collection.Should().NotBeNull();

        // 1. Insert
        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var doc = ZVecDoc.Create("doc1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["name"] = "Alice", ["age"] = 30 });

        var insertStatus = _collection!.Insert(doc);
        insertStatus.IsSuccess.Should().BeTrue();

        // 2. Fetch
        var fetched = _collection.Fetch("doc1", includeVector: true);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be("doc1");
        fetched.Fields["name"].Should().Be("Alice");
        fetched.Fields["age"].Should().Be(30);
        fetched.DenseVectors["embedding"].ToArray().Should().BeEquivalentTo(vector);

        // 3. Update
        var updatedDoc = ZVecDoc.Create("doc1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["name"] = "Alice Cooper", ["age"] = 31 });

        var updateStatus = _collection.Update(updatedDoc);
        updateStatus.IsSuccess.Should().BeTrue();

        var refetched = _collection.Fetch("doc1", includeVector: false);
        refetched.Should().NotBeNull();
        refetched!.Fields["name"].Should().Be("Alice Cooper");
        refetched.Fields["age"].Should().Be(31);

        // 4. Delete
        var deleteStatus = _collection.Delete("doc1");
        deleteStatus.IsSuccess.Should().BeTrue();

        var missing = _collection.Fetch("doc1");
        missing.Should().BeNull();
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
