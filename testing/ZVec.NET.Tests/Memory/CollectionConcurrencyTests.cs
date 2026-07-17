using FluentAssertions;
using ZVec.NET.Tests.Helpers;
using ZVec.NET.Tests.Integration;

namespace ZVec.NET.Tests.Memory;

public class CollectionConcurrencyTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public CollectionConcurrencyTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_concurrency_{Guid.NewGuid():N}");
    }

    private void Setup()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();
        var schema = new ZVecCollectionSchema
        {
            Name = "concurrency_test",
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
        _collection = _factory.CreateAndOpen(_testPath, schema);
    }

    [Fact]
    public async Task Concurrent_Insert_And_Query_Execute_Without_Exceptions()
    {
        Setup();
        _collection.Should().NotBeNull();

        const int taskCount = 10;
        const int iterationsPerTask = 50;
        var queryVector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };

        var testCt = TestContext.Current.CancellationToken;
        await ConcurrencyTestHelper.VerifyNoDeadlock(async ct =>
        {
            await ConcurrencyTestHelper.RunConcurrently(taskCount, async (workerId, token) =>
            {
                for (int j = 0; j < iterationsPerTask; j++)
                {
                    token.ThrowIfCancellationRequested();
                    if (workerId < taskCount / 2)
                    {
                        var docId = $"writer_{workerId}_doc_{j}";
                        var doc = ZVecDoc.Create(docId,
                            denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
                            {
                                ["embedding"] = queryVector
                            });
                        _collection!.Insert(doc).IsSuccess.Should().BeTrue();
                    }
                    else
                    {
                        var results = _collection!.Query(
                            new ZVecQuery { FieldName = "embedding", Vector = queryVector },
                            topk: 5);
                        results.Should().NotBeNull();
                    }
                    await Task.Yield();
                }
            }, ct);
        }, TimeSpan.FromSeconds(30), testCt);
    }

    public void Dispose()
    {
        _collection?.Dispose();
        _factory?.Dispose();
        if (Directory.Exists(_testPath))
        {
            try { Directory.Delete(_testPath, true); }
            catch { /* ignore cleanup failures */ }
        }
    }
}
