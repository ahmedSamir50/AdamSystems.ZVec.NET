using FluentAssertions;
using ZVec.NET.Tests.Helpers;
using ZVec.NET.Tests.Integration;

namespace ZVec.NET.Tests.Memory;

/// <summary>
/// US-E19.7 / US-E19.8 — collection concurrency with SemaphoreSlim gates (0 = unlimited).
/// </summary>
public class ConcurrencyGateTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public ConcurrencyGateTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_conc_gate_{Guid.NewGuid():N}");
    }

    private void Setup(ZVecOptions? options = null, ZVecCollectionOptions? collectionOptions = null)
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize(options);

        var schema = new ZVecCollectionSchema
        {
            Name = "concurrency_gate",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecFlatIndexParam()
                }
            ]
        };

        _collection = _factory.CreateAndOpen(_testPath, schema, collectionOptions);
    }

    [Fact]
    public async Task ConcurrentReads_DontBlock()
    {
        Setup();
        _collection.Should().NotBeNull();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        _collection!.Insert(ZVecDoc.Create("seed",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector }));

        var testCt = TestContext.Current.CancellationToken;
        await ConcurrencyTestHelper.VerifyNoDeadlock(async ct =>
        {
            await ConcurrencyTestHelper.RunConcurrently(10, async (_, token) =>
            {
                for (int i = 0; i < 5; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var results = await _collection.QueryAsync(
                        new ZVecQuery { FieldName = "embedding", Vector = vector },
                        topk: ZVecDefaults.Query.Topk,
                        ct: token);
                    results.Should().NotBeNull();
                    await Task.Yield();
                }
            }, ct);
        }, TimeSpan.FromSeconds(20), testCt);
    }

    [Fact]
    public async Task WriteBlocksReads_SerializedThroughNativeGate()
    {
        Setup(options: new ZVecOptions { MaxConcurrentNativeCalls = 1 });
        _collection.Should().NotBeNull();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        _collection!.Insert(ZVecDoc.Create("seed",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector }));

        var testCt = TestContext.Current.CancellationToken;
        await ConcurrencyTestHelper.VerifyNoDeadlock(async ct =>
        {
            await ConcurrencyTestHelper.RunConcurrently(6, async (workerId, token) =>
            {
                for (int i = 0; i < 10; i++)
                {
                    token.ThrowIfCancellationRequested();
                    if (workerId == 0)
                    {
                        var doc = ZVecDoc.Create($"writer_{i}",
                            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector });
                        _collection.Insert(doc).IsSuccess.Should().BeTrue();
                    }
                    else
                    {
                        _ = _collection.Query(
                            new ZVecQuery { FieldName = "embedding", Vector = vector },
                            topk: ZVecDefaults.Query.Topk);
                    }
                    await Task.Yield();
                }
            }, ct);
        }, TimeSpan.FromSeconds(30), testCt);
    }

    [Fact]
    public async Task MixedReadWrite_NoCorruption()
    {
        Setup();
        _collection.Should().NotBeNull();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var testCt = TestContext.Current.CancellationToken;

        await ConcurrencyTestHelper.VerifyNoDeadlock(async ct =>
        {
            await ConcurrencyTestHelper.RunConcurrently(7, async (workerId, token) =>
            {
                for (int i = 0; i < 20; i++)
                {
                    token.ThrowIfCancellationRequested();
                    if (workerId < 2)
                    {
                        var doc = ZVecDoc.Create($"mw_{workerId}_{i}",
                            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector });
                        _collection!.Insert(doc).IsSuccess.Should().BeTrue();
                    }
                    else
                    {
                        _ = _collection!.Query(
                            new ZVecQuery { FieldName = "embedding", Vector = vector },
                            topk: 3);
                    }
                    await Task.Yield();
                }
            }, ct);
        }, TimeSpan.FromSeconds(25), testCt);
    }

    [Fact]
    public async Task Cancellation_DuringQuery_ThrowsWhenAlreadyCancelled()
    {
        Setup(collectionOptions: new ZVecCollectionOptions { MaxConcurrentReads = 1 });
        _collection.Should().NotBeNull();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        _collection!.Insert(ZVecDoc.Create("seed",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector }));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await _collection.QueryAsync(
            new ZVecQuery { FieldName = "embedding", Vector = vector },
            topk: ZVecDefaults.Query.Topk,
            ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DisposeDuringActiveRead_CompletesWithoutDeadlock()
    {
        Setup();
        _collection.Should().NotBeNull();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        _collection!.Insert(ZVecDoc.Create("seed",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector }));

        var testCt = TestContext.Current.CancellationToken;
        await ConcurrencyTestHelper.VerifyNoDeadlock(async ct =>
        {
            var readTask = Task.Run(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        _ = _collection.Query(
                            new ZVecQuery { FieldName = "embedding", Vector = vector },
                            topk: ZVecDefaults.Query.Topk);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            }, ct);

            await Task.Yield();
            _collection.Dispose();
            await readTask;
        }, TimeSpan.FromSeconds(15), testCt);
    }

    [Fact]
    public async Task DestroyDuringActiveRead_CompletesWithoutDeadlock()
    {
        Setup();
        _collection.Should().NotBeNull();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        _collection!.Insert(ZVecDoc.Create("seed",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector }));

        var testCt = TestContext.Current.CancellationToken;
        await ConcurrencyTestHelper.VerifyNoDeadlock(async ct =>
        {
            var readTask = Task.Run(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        _ = _collection.Query(
                            new ZVecQuery { FieldName = "embedding", Vector = vector },
                            topk: ZVecDefaults.Query.Topk);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            }, ct);

            await Task.Yield();
            _collection.Destroy();
            await readTask;
        }, TimeSpan.FromSeconds(15), testCt);
    }

    [Fact]
    public async Task MaxConcurrentReads_Throttles()
    {
        Setup(collectionOptions: new ZVecCollectionOptions { MaxConcurrentReads = 4 });
        _collection.Should().NotBeNull();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        _collection!.Insert(ZVecDoc.Create("seed",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector }));

        var testCt = TestContext.Current.CancellationToken;
        await ConcurrencyTestHelper.VerifyNoDeadlock(async ct =>
        {
            await ConcurrencyTestHelper.RunConcurrently(12, async (_, token) =>
            {
                for (int i = 0; i < 10; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var results = _collection.Query(
                        new ZVecQuery { FieldName = "embedding", Vector = vector },
                        topk: 1);
                    results.Should().NotBeNull();
                    await Task.Yield();
                }
            }, ct);
        }, TimeSpan.FromSeconds(25), testCt);
    }

    [Fact]
    public async Task GlobalThrottle_LimitsNativeCalls()
    {
        Setup(options: new ZVecOptions { MaxConcurrentNativeCalls = 8 });
        _collection.Should().NotBeNull();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var testCt = TestContext.Current.CancellationToken;

        await ConcurrencyTestHelper.VerifyNoDeadlock(async ct =>
        {
            await ConcurrencyTestHelper.RunConcurrently(16, async (workerId, token) =>
            {
                for (int i = 0; i < 5; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var doc = ZVecDoc.Create($"w{workerId}_{i}",
                        denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector });
                    _collection!.Insert(doc).IsSuccess.Should().BeTrue();
                    await Task.Yield();
                }
            }, ct);
        }, TimeSpan.FromSeconds(30), testCt);
    }

    [Fact]
    public async Task InsertAsync_CancelWhileWaitingOnSaturatedGate_ThrowsPromptly()
    {
        Setup(options: new ZVecOptions { MaxConcurrentNativeCalls = 1 });
        _collection.Should().NotBeNull();
        var factory = (ZVecFactory)_factory!;

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var doc = ZVecDoc.Create("cancel_wait",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector });

        // Hold the single native-call slot so InsertAsync must WaitAsync.
        factory.EnterNativeCall();
        try
        {
            using var cts = new CancellationTokenSource();
            var insertTask = _collection!.InsertAsync(doc, cts.Token).AsTask();

            // Allow the async path to reach WaitAsync on the saturated gate.
            await Task.Delay(100, TestContext.Current.CancellationToken);
            insertTask.IsCompleted.Should().BeFalse("InsertAsync should be waiting on the saturated gate");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            cts.Cancel();

            var act = async () => await insertTask.WaitAsync(TestContext.Current.CancellationToken);
            await act.Should().ThrowAsync<OperationCanceledException>();
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
                "cancel while waiting on WaitAsync should complete promptly");
        }
        finally
        {
            factory.ExitNativeCall();
        }
    }

    public void Dispose()
    {
        _collection?.Dispose();
        _factory?.Dispose();
        if (Directory.Exists(_testPath))
        {
            try { Directory.Delete(_testPath, true); }
            catch { /* ignore */ }
        }
    }
}
