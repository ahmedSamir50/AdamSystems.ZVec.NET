using FluentAssertions;

namespace ZVec.NET.Tests.Integration;

/// <summary>
/// Integration coverage for opt-in gate WaitAsync: cancel while waiting must complete promptly
/// for write and read async paths against the real native library.
/// </summary>
public class AsyncGateIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public AsyncGateIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_async_gate_{Guid.NewGuid():N}");
    }

    private static ZVecCollectionSchema CreateSchema() => new()
    {
        Name = "async_gate_integration",
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

    private void Setup(ZVecOptions? options = null)
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize(options);
        _collection = _factory.CreateAndOpen(_testPath, CreateSchema());
    }

    [Fact]
    public async Task InsertAsync_CancelWhileWaiting_OnSaturatedNativeGate()
    {
        Setup(options: new ZVecOptions { MaxConcurrentNativeCalls = 1 });
        _collection.Should().NotBeNull();
        var factory = (ZVecFactory)_factory!;

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var doc = ZVecDoc.Create("cancel_insert",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector });

        factory.EnterNativeCall(TestContext.Current.CancellationToken);
        try
        {
            using var cts = new CancellationTokenSource();
            var insertTask = _collection!.InsertAsync(doc, cts.Token).AsTask();

            await Task.Delay(100, TestContext.Current.CancellationToken);
            insertTask.IsCompleted.Should().BeFalse("InsertAsync should be waiting on the saturated native gate");

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

    [Fact]
    public async Task QueryAsync_CancelWhileWaiting_OnSaturatedNativeGate()
    {
        Setup(options: new ZVecOptions { MaxConcurrentNativeCalls = 1 });
        _collection.Should().NotBeNull();
        var factory = (ZVecFactory)_factory!;

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        _collection!.Insert(ZVecDoc.Create("seed",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector }));

        factory.EnterNativeCall(TestContext.Current.CancellationToken);
        try
        {
            using var cts = new CancellationTokenSource();
            var queryTask = _collection.QueryAsync(
                new ZVecQuery { FieldName = "embedding", Vector = vector },
                topk: 1,
                includeVector: false,
                ct: cts.Token).AsTask();

            await Task.Delay(100, TestContext.Current.CancellationToken);
            queryTask.IsCompleted.Should().BeFalse("QueryAsync should be waiting on the saturated native gate");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            cts.Cancel();

            var act = async () => await queryTask.WaitAsync(TestContext.Current.CancellationToken);
            await act.Should().ThrowAsync<OperationCanceledException>();
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
                "cancel while waiting on WaitAsync should complete promptly");
        }
        finally
        {
            factory.ExitNativeCall();
        }
    }

    [Fact]
    public async Task FetchAsync_CancelWhileWaiting_OnSaturatedNativeGate()
    {
        Setup(options: new ZVecOptions { MaxConcurrentNativeCalls = 1 });
        _collection.Should().NotBeNull();
        var factory = (ZVecFactory)_factory!;

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        _collection!.Insert(ZVecDoc.Create("seed",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector }));

        factory.EnterNativeCall(TestContext.Current.CancellationToken);
        try
        {
            using var cts = new CancellationTokenSource();
            var fetchTask = _collection.FetchAsync("seed", includeVector: false, ct: cts.Token).AsTask();

            await Task.Delay(100, TestContext.Current.CancellationToken);
            fetchTask.IsCompleted.Should().BeFalse("FetchAsync should be waiting on the saturated native gate");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            cts.Cancel();

            var act = async () => await fetchTask.WaitAsync(TestContext.Current.CancellationToken);
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
            catch { /* ignore cleanup */ }
        }
    }
}
