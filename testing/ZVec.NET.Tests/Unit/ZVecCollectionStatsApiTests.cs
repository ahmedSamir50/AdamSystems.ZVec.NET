using FluentAssertions;
using ZVec.NET.Query;

namespace ZVec.NET.Tests.Unit;

public class ZVecCollectionStatsApiTests : IDisposable
{
    private readonly string _testDir;
    private readonly List<IZvecCollection> _collectionsToCleanup = [];
    private IZvecFactory? _factory;

    public ZVecCollectionStatsApiTests()
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

    private ZVecCollection CreateCollection(ZVecCollectionOptions? options = null)
    {
        var schema = new ZVecCollectionSchema
        {
            Name = "stats_test",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "vec",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 2,
                    IndexParam = new ZVecFlatIndexParam()
                }
            ]
        };
        var col = (ZVecCollection)_factory!.CreateAndOpen(
            Path.Combine(_testDir, $"coll-{Guid.NewGuid()}"),
            schema,
            options);
        _collectionsToCleanup.Add(col);
        return col;
    }

    public void Dispose()
    {
        foreach (var col in _collectionsToCleanup) { try { col.Destroy(); } catch { } }
        try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public void Options_ReturnsSuppliedOpenOptions()
    {
        if (_factory is null || !_factory.IsInitialized) return;

        var options = new ZVecCollectionOptions { ReadOnly = false, EnableMmap = false, MaxConcurrentReads = 4 };
        var col = CreateCollection(options);

        col.Options.ReadOnly.Should().BeFalse();
        col.Options.EnableMmap.Should().BeFalse();
        col.Options.MaxConcurrentReads.Should().Be(4);
    }

    [Fact]
    public void GetStats_AfterInsert_ReportsDocCount()
    {
        if (_factory is null || !_factory.IsInitialized) return;

        var col = CreateCollection();
        col.Insert(ZVecDoc.Create("s1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["vec"] = new float[] { 1f, 0f } }))
            .IsSuccess.Should().BeTrue();

        var stats = col.GetStats();
        stats.DocCount.Should().BeGreaterThanOrEqualTo(1);
        col.Stats.DocCount.Should().Be(stats.DocCount);
    }

    [Fact]
    public void GetStats_WhenDisposed_ThrowsObjectDisposedException()
    {
        if (_factory is null || !_factory.IsInitialized) return;

        var col = CreateCollection();
        col.Dispose();
        var act = () => col.GetStats();
        act.Should().Throw<ObjectDisposedException>();
    }
}
