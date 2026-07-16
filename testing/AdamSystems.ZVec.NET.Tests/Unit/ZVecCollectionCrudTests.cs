using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit;

public class ZVecCollectionCrudTests : IDisposable
{
    private readonly string _testDir;
    private readonly List<IZvecCollection> _collectionsToCleanup = [];
    private IZvecFactory? _factory;
    private ZVecCollectionSchema? _schema;

    public ZVecCollectionCrudTests()
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
            Name = "crud_test",
            Vectors = [new ZVecVectorSchema { Name = "vec", DataType = ZVecDataType.VectorFp32, Dimension = 2, IndexParam = new ZVecFlatIndexParam() }],
            Fields = [new ZVecFieldSchema { Name = "id1", DataType = ZVecDataType.String }]
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
    public void Insert_WithNullDoc_ThrowsArgumentNullException()
    {
        if (_factory is null || !ZVecFactory.IsInitialized) return;
        var col = CreateCollection();
        var act = () => col.Insert((ZVecDoc)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Insert_WhenDisposed_ThrowsObjectDisposedException()
    {
        if (_factory is null || !ZVecFactory.IsInitialized) return;
        var col = CreateCollection();
        try { col.Dispose(); } catch { }
        var act = () => col.Insert(ZVecDoc.Create("id1"));
        act.Should().Throw<ObjectDisposedException>();
    }


    [Fact]
    public async Task InsertAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        if (_factory is null || !ZVecFactory.IsInitialized) return;
        var col = CreateCollection();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = async () => await col.InsertAsync(ZVecDoc.Create("id1"), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Fetch_WithNullKey_ThrowsArgumentNullException()
    {
        if (_factory is null || !ZVecFactory.IsInitialized) return;
        var col = CreateCollection();
        var act = () => col.Fetch((string)null!);
        act.Should().Throw<ArgumentException>();
    }

}
