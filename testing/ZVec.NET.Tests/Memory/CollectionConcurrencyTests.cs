using FluentAssertions;

namespace ZVec.NET.Tests.Memory;

public class CollectionConcurrencyTests : IDisposable
{
    private readonly string _testPath;
    private readonly ZVecCollectionSchema _schema;
    private readonly IZvecFactory _factory;
    private readonly IZvecCollection _collection;

    public CollectionConcurrencyTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_concurrency_{Guid.NewGuid():N}");
        _schema = new ZVecCollectionSchema
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

        _factory = new ZVecFactory();
        _factory.Initialize();
        _collection = _factory.CreateAndOpen(_testPath, _schema);
    }

    [Fact]
    public async Task Concurrent_Insert_And_Query_Execute_Without_Exceptions()
    {
        if (_collection is null) return; // Native library not available

        const int taskCount = 10;
        const int iterationsPerTask = 50;

        var tasks = new List<Task>();

        // 1. Spawn concurrent writers
        for (int i = 0; i < taskCount / 2; i++)
        {
            int writerId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < iterationsPerTask; j++)
                {
                    var docId = $"writer_{writerId}_doc_{j}";
                    var doc = ZVecDoc.Create(docId,
                        denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
                        {
                            ["embedding"] = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
                        });
                    var status = _collection.Insert(doc);
                    _ = status.IsSuccess.Should().BeTrue();
                }
            }, TestContext.Current.CancellationToken));
        }

        // 2. Spawn concurrent readers
        for (int i = 0; i < taskCount / 2; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var queryVector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
                var query = new ZVecQuery { FieldName = "embedding", Vector = queryVector };

                for (int j = 0; j < iterationsPerTask; j++)
                {
                    // Queries should execute successfully without crash or access violation
                    var results = _collection.Query(query, topk: 5);
                    results.Should().NotBeNull();
                }
            } , TestContext.Current.CancellationToken) );
        }

        // Wait for all concurrent operations to finish
        var testTimeout = Task.Delay(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        var whenAll = Task.WhenAll(tasks);

        var completedTask = await Task.WhenAny(whenAll, testTimeout);
        if (completedTask == testTimeout)
        {
            throw new TimeoutException("Concurrent inserts and queries deadlocked or timed out.");
        }

        await whenAll; // Propagate any assertion failures
    }

    public void Dispose()
    {
        _collection.Dispose();
        _factory.Dispose();
        if (Directory.Exists(_testPath))
        {
            try
            {
                Directory.Delete(_testPath, true);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }
    }
}
