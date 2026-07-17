using FluentAssertions;

namespace ZVec.NET.Tests.Integration;

/// <summary>US-E18.7 — open → CRUD → close → reopen → verify data persists.</summary>
public class CollectionLifecycleIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private readonly ZVecCollectionSchema _schema;
    private IZvecFactory? _factory;

    public CollectionLifecycleIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_lifecycle_{Guid.NewGuid():N}");
        _schema = new ZVecCollectionSchema
        {
            Name = "lifecycle_integration",
            Fields =
            [
                new ZVecFieldSchema { Name = "title", DataType = ZVecDataType.String }
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

    [Fact]
    public void Open_Crud_Close_Reopen_PersistsData()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var doc = ZVecDoc.Create("persist1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["title"] = "persisted" });

        using (var col = _factory.CreateAndOpen(_testPath, _schema))
        {
            col.Insert(doc).IsSuccess.Should().BeTrue();
            col.Fetch("persist1").Should().NotBeNull();

            var updated = ZVecDoc.Create("persist1",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
                fields: new Dictionary<string, object> { ["title"] = "updated-title" });
            col.Update(updated).IsSuccess.Should().BeTrue();
        }

        using (var reopened = _factory.Open(_testPath))
        {
            var fetched = reopened.Fetch("persist1", includeVector: false);
            fetched.Should().NotBeNull();
            fetched!.Id.Should().Be("persist1");

            var hits = reopened.Query(
                new ZVecQuery { FieldName = "embedding", Vector = vector },
                topk: 1);
            hits.Should().ContainSingle();
            hits[0].Id.Should().Be("persist1");
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
        if (Directory.Exists(_testPath))
        {
            try { Directory.Delete(_testPath, true); }
            catch { /* ignore cleanup */ }
        }
    }
}
