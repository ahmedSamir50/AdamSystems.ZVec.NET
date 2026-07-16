using FluentAssertions;

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
}
