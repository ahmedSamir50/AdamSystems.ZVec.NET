using FluentAssertions;
using ZVec.NET.Tests.Helpers;
using ZVec.NET.Tests.Integration;

namespace ZVec.NET.Tests.Memory;

/// <summary>
/// US-E19.7 / US-E19.8 — collection concurrency with SemaphoreSlim gates (0 = unlimited).
/// Replaces obsolete WriteBlocksReads scenarios from the canceled E9 RW lock.
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
    public async Task ConcurrentReads_WithMaxConcurrentReads_CompleteWithoutDeadlock()
    {
        Setup(collectionOptions: new ZVecCollectionOptions { MaxConcurrentReads = 2 });
        _collection.Should().NotBeNull();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        _collection!.Insert(ZVecDoc.Create("seed",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector }));

        var testCt = TestContext.Current.CancellationToken;
        await ConcurrencyTestHelper.VerifyNoDeadlock(async ct =>
        {
            await ConcurrencyTestHelper.RunConcurrently(8, async (_, token) =>
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
        }, TimeSpan.FromSeconds(20), testCt);
    }

    [Fact]
    public async Task GlobalThrottle_MaxConcurrentNativeCalls_Completes()
    {
        Setup(options: new ZVecOptions { MaxConcurrentNativeCalls = 2 });
        _collection.Should().NotBeNull();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var testCt = TestContext.Current.CancellationToken;

        await ConcurrencyTestHelper.VerifyNoDeadlock(async ct =>
        {
            await ConcurrencyTestHelper.RunConcurrently(6, async (workerId, token) =>
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
        }, TimeSpan.FromSeconds(20), testCt);
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
