using FluentAssertions;

namespace ZVec.NET.Tests.Integration;

/// <summary>US-E18.10 — Async CRUD + query integration.</summary>
public class AsyncPathIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private readonly ZVecCollectionSchema _schema;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public AsyncPathIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_async_{Guid.NewGuid():N}");
        _schema = new ZVecCollectionSchema
        {
            Name = "async_integration",
            Fields =
            [
                new ZVecFieldSchema { Name = "name", DataType = ZVecDataType.String }
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
    public async Task Async_Insert_Fetch_Query_Delete_Succeeds()
    {
        _fixture.SkipIfNotAvailable();
        var ct = TestContext.Current.CancellationToken;
        _factory = new ZVecFactory();
        await _factory.InitializeAsync(ct: ct);
        _collection = await _factory.CreateAndOpenAsync(_testPath, _schema, ct: ct);

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var doc = ZVecDoc.Create("async1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["name"] = "async" });

        var insert = await _collection.InsertAsync(doc, ct);
        insert.IsSuccess.Should().BeTrue();

        var fetched = await _collection.FetchAsync("async1", includeVector: true, ct: ct);
        fetched.Should().NotBeNull();
        fetched!.Fields["name"].Should().Be("async");

        var results = await _collection.QueryAsync(
            new ZVecQuery { FieldName = "embedding", Vector = vector },
            topk: 1,
            ct: ct);
        results.Should().NotBeEmpty();
        results[0].Id.Should().Be("async1");

        var delete = await _collection.DeleteAsync("async1", ct);
        delete.IsSuccess.Should().BeTrue();
        (await _collection.FetchAsync("async1", ct: ct)).Should().BeNull();
    }

    [Fact]
    public async Task Async_Update_Upsert_BatchFetch_Succeeds()
    {
        _fixture.SkipIfNotAvailable();
        var ct = TestContext.Current.CancellationToken;
        _factory = new ZVecFactory();
        await _factory.InitializeAsync(ct: ct);
        _collection = await _factory.CreateAndOpenAsync(_testPath + "_crud", _schema, ct: ct);

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var doc = ZVecDoc.Create("batch1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["name"] = "batch" });

        (await _collection.InsertAsync(doc, ct)).IsSuccess.Should().BeTrue();

        var updated = ZVecDoc.Create("batch1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["name"] = "updated-batch" });
        (await _collection.UpdateAsync(updated, ct)).IsSuccess.Should().BeTrue();

        var upserted = ZVecDoc.Create("batch2",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["name"] = "upserted" });
        (await _collection.UpsertAsync(upserted, ct)).IsSuccess.Should().BeTrue();

        var fetched = await _collection.FetchAsync(new[] { "batch1", "batch2" }, includeVector: true, ct: ct);
        fetched.Should().HaveCount(2);
        fetched.Should().Contain(d => d.Id == "batch1" && (string)d.Fields["name"]! == "updated-batch");
        fetched.Should().Contain(d => d.Id == "batch2" && (string)d.Fields["name"]! == "upserted");

        _collection.Stats.DocCount.Should().BeGreaterThanOrEqualTo(1);

        (await _collection.DeleteAsync(new[] { "batch1" }, ct)).IsSuccess.Should().BeTrue();
    }

    public void Dispose()
    {
        _collection?.Dispose();
        _factory?.Dispose();
        if (Directory.Exists(_testPath))
        {
            try { Directory.Delete(_testPath, true); }
            catch { /* ignore cleanup */ }
        }
    }
}
