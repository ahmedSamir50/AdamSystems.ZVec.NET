using FluentAssertions;

namespace ZVec.NET.Tests.Unit;

public class ZVecCollectionDdlTests
{
    private readonly string _testDir;
    private readonly List<IZvecCollection> _collectionsToCleanup = [];
    private IZvecFactory? _factory;
    private ZVecCollectionSchema? _schema;
    private IZvecCollection? _collection;

    public ZVecCollectionDdlTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"zvec-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        try
        {
            var factory = new ZVecFactory();
            factory.Initialize();
            _factory = factory;
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException)
        {
            // Already initialized or native library not available
        }
    }

    private IZvecCollection CreateTestCollection()
    {
        _schema = new ZVecCollectionSchema
        {
            Name = "fts_integration",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "content",
                    DataType = ZVecDataType.String,
                    Dimension = 0,
                    IndexParam = new ZVecFtsIndexParam
                    {
                        Tokenizer = ZVecFtsTokenizer.Standard,
                        Filters = [ZVecFtsTokenFilter.Lowercase]
                    }
                }
            ]
        };

        var collectionPath = Path.Combine(_testDir, $"coll-{Guid.NewGuid()}");
        var col = _factory!.CreateAndOpen(collectionPath, _schema);
        _collectionsToCleanup.Add(col);
        _collection = col;
        return col;
    }

    [Fact]
    public void AddColumn_WithNullField_ThrowsArgumentNullException()
    {
        if (_factory is null || !_factory.IsInitialized) return; // Native library not available
        var col = CreateTestCollection();
        var act = () => col.AddColumn(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddColumn_WhenDisposed_ThrowsObjectDisposedException()
    {
        if (_factory is null || !_factory.IsInitialized) return; // Native library not available
        var col = CreateTestCollection();
        try { col.Dispose(); } catch { }
        var act = () => col.AddColumn(new ZVecFieldSchema { Name = "title", DataType = ZVecDataType.String });
        act.Should().Throw<ObjectDisposedException>();
    }




    public void Dispose()
    {
        foreach (var col in _collectionsToCleanup)
        {
            try { col.Destroy(); } catch { }
        }

        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }
        catch { }
    }
}
