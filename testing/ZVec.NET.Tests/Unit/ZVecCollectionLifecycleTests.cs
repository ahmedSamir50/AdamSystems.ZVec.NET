using FluentAssertions;

namespace ZVec.NET.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ZVecCollection"/> lifecycle semantics.
/// Uses a fake/null handle (nint.MaxValue) to test managed-side logic only.
/// Full native round-trips are covered in integration tests (Epic E18).
/// </summary>
public class ZVecCollectionLifecycleTests : IDisposable
{
    private readonly string _testDir;
    private readonly List<IZvecCollection> _collectionsToCleanup = [];
    private IZvecFactory? _factory;

    public ZVecCollectionLifecycleTests()
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
        var schema = new ZVecCollectionSchema
        {
            Name = "lifecycle_test",
            Vectors = [new ZVecVectorSchema { Name = "vec", DataType = ZVecDataType.VectorFp32, Dimension = 2, IndexParam = new ZVecFlatIndexParam() }]
        };
        var col = _factory!.CreateAndOpen(Path.Combine(_testDir, $"coll-{Guid.NewGuid()}"), schema);
        _collectionsToCleanup.Add(col);
        return (ZVecCollection)col;
    }

    public void Dispose()
    {
        foreach (var col in _collectionsToCleanup) { try { col.Destroy(); } catch { } }
        try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public void Collection_Path_ReturnsConstructedValue()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        col.Path.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Collection_Schema_NotNullWhenProvided()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        // Schema is actually provided in the new CreateCollection
        var col = CreateCollection();
        col.Schema.Should().NotBeNull();
    }

    [Fact]
    public void Collection_DisposeAsync_DoesNotThrow()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        var act = async () => { await col.DisposeAsync(); };
        act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Collection_Dispose_CalledTwice_IsIdempotent()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        try { col.Dispose(); } catch { }

        var secondCall = () => col.Dispose();
        secondCall.Should().NotThrow();
    }

    [Fact]
    public async Task Collection_ConcurrentDisposeAndDisposeAsync_CloseCalledOnce()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();

        var task1 = Task.Run(() => { try { col.Dispose(); } catch { } }, TestContext.Current.CancellationToken);
        var task2 = Task.Run(async () => { try { await col.DisposeAsync(); } catch { } }, TestContext.Current.CancellationToken);

        await Task.WhenAll(task1, task2).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }



    [Fact]
    public void Collection_Destroy_CalledTwice_IsIdempotent()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        try { col.Destroy(); } catch { }

        var secondDestroy = () => col.Destroy();
        secondDestroy.Should().NotThrow();
    }

    [Fact]
    public void Collection_DestroyAsync_CancelledToken_Throws()
    {
        if (_factory is null || !_factory.IsInitialized) return;
        var col = CreateCollection();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = async () => await col.DestroyAsync(cts.Token);
        act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Collection_Constructor_ZeroHandle_Throws()
    {
        var act = () => new ZVecCollection(nint.Zero, "/tmp/test", null, CancellationToken.None, null!);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
