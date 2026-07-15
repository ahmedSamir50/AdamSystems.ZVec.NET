using AdamSystems.ZVec.NET.Interop;
using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit;

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

        // Initialize ZVec if needed
        try
        {
            _factory = new ZVecFactory();
            _factory.Initialize();
        }
        catch (InvalidOperationException)
        {
            // Already initialized
        }
    }

    private IZvecCollection CreateTestCollection()
    {
        _schema = new ZVecCollectionSchema
        {
            Name = "fts_integration",
            Vectors = new[]
            {
                new ZVecVectorSchema
                {
                    Name = "content",
                    DataType = ZVecDataType.String,
                    Dimension = 0,
                    IndexParam = new ZVecFtsIndexParam
                    {
                        Tokenizer = ZVecFtsTokenizer.Standard,
                        Filters = new[] { ZVecFtsTokenFilter.Lowercase }
                    }
                }
            }
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
        var col = CreateTestCollection();
        var act = () => col.AddColumn(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddColumn_WhenDisposed_ThrowsObjectDisposedException()
    {
        var col = CreateTestCollection();
        try { col.Dispose(); } catch { }
        var act = () => col.AddColumn(new ZVecFieldSchema { Name = "title", DataType = ZVecDataType.String });
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AddColumn_AttemptsNativeCall()
    {
        var col = CreateTestCollection();
        var field = new ZVecFieldSchema { Name = "title", DataType = ZVecDataType.String };
        var act = () => col.AddColumn(field);
        if (NativeLibraryHelper.IsAvailable())
        {
            act.Should().NotThrow();
        }

        act.Should().Throw<Exception>().Where(ex => ex is DllNotFoundException || ex is EntryPointNotFoundException);
    }

    [Fact]
    public void CreateIndex_AttemptsNativeCall()
    {
        var col = CreateTestCollection();
        var param = new ZVecFlatIndexParam { MetricType = ZVecMetricType.Cosine };
        var act = () => col.CreateIndex("vec", param);
        act.Should().Throw<Exception>().Where(ex => ex is DllNotFoundException || ex is EntryPointNotFoundException);
    }

    [Fact]
    public void Optimize_AttemptsNativeCall()
    {
        var col = CreateTestCollection();
        var act = () => col.Optimize();
        act.Should().Throw<Exception>().Where(ex => ex is DllNotFoundException || ex is EntryPointNotFoundException);
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

internal static class NativeLibraryHelper
{
    public static bool IsAvailable()
    {
        try
        {
            NativeMethods.zvec_get_version();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }
}
