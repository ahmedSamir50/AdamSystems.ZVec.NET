using FluentAssertions;
using ZVec.NET.Query;

namespace ZVec.NET.Tests.Unit;

public class ZVecCollectionQueryTests : IDisposable
{
    private readonly string _testDir;
    private readonly List<IZvecCollection> _collectionsToCleanup = [];
    private IZvecFactory? _factory;
    private ZVecCollectionSchema? _schema;

    public ZVecCollectionQueryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"zvec-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        try
        {
            var factory = new ZVecFactory();
            factory.Initialize();
            _factory = factory;
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException) { }
    }

    private ZVecCollection CreateCollection()
    {
        _schema = new ZVecCollectionSchema
        {
            Name = "query_test",
            Vectors = [new ZVecVectorSchema { Name = "vec", DataType = ZVecDataType.VectorFp32, Dimension = 2, IndexParam = new ZVecFlatIndexParam() }]
        };
        var col = _factory!.CreateAndOpen(Path.Combine(_testDir, $"coll-{Guid.NewGuid()}"), _schema);
        _collectionsToCleanup.Add(col);
        return (ZVecCollection)col;
    }

    public void Dispose()
    {
        foreach (var col in _collectionsToCleanup) { try { col.Destroy(); } catch { } }
        try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public void Query_WithNullQuery_ThrowsArgumentNullException()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        var act = () => col.Query((ZVecQuery)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Query_WhenDisposed_ThrowsObjectDisposedException()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        try { col.Dispose(); } catch { }
        var act = () => col.Query(new ZVecQuery { FieldName = "vec" });
        act.Should().Throw<ObjectDisposedException>();
    }


    [Fact]
    public async Task QueryAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var q = new ZVecQuery { FieldName = "vec" };
        var act = async () => await col.QueryAsync(q, ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Query_WithFilterBuilder_NullFilter_ThrowsArgumentNullException()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        var act = () => col.Query(new ZVecQuery { FieldName = "vec", Vector = new float[] { 1f, 0f } }, 1, (ZVecFilterBuilder)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Query_WithDocumentId_NotFound_ThrowsKeyNotFoundException()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        var q = new ZVecQuery { FieldName = "vec", DocumentId = "missing" };
        var act = () => col.Query(q);
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Query_WithDocumentId_ResolvesVectorAndReturnsHits()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        col.Insert(ZVecDoc.Create(
            "doc1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["vec"] = new float[] { 1f, 0f } }));

        var results = col.Query(new ZVecQuery { FieldName = "vec", DocumentId = "doc1" }, topk: 1);
        results.Should().NotBeEmpty();
        results[0].Id.Should().Be("doc1");
    }

    [Fact]
    public void Query_IncludeVectorFalse_OmitsDenseVectors()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        col.Insert(ZVecDoc.Create(
            "doc1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["vec"] = new float[] { 1f, 0f } }));

        var withVectors = col.Query(
            new ZVecQuery { FieldName = "vec", Vector = new float[] { 1f, 0f } },
            topk: 1,
            includeVector: true);
        withVectors.Should().NotBeEmpty();
        withVectors[0].DenseVectors.Should().ContainKey("vec");

        var without = col.Query(
            new ZVecQuery { FieldName = "vec", Vector = new float[] { 1f, 0f } },
            topk: 1,
            includeVector: false);
        without.Should().NotBeEmpty();
        without[0].DenseVectors.Should().BeEmpty();
    }

    [Fact]
#pragma warning disable CS0618
    public void QueryGroupBy_ThrowsNotSupportedException()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        var gq = new ZVecGroupByQuery
        {
            Query = new ZVecQuery { FieldName = "vec", Vector = new float[] { 1f, 0f } },
            GroupByField = "category"
        };
        var act = () => col.QueryGroupBy(gq);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*zvec_collection_group_by*");
    }

    [Fact]
    public void QueryGroupBy_WithNull_ThrowsArgumentNullException()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        var act = () => col.QueryGroupBy(null!);
        act.Should().Throw<ArgumentNullException>();
    }
#pragma warning restore CS0618

    [Fact]
    public async Task QueryAsync_MultiQuery_CancelledToken_ThrowsOperationCanceledException()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var queries = new List<ZVecQuery> { new() { FieldName = "vec", Vector = new float[] { 1f, 0f } } };
        var act = async () => await col.QueryAsync(queries, ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
