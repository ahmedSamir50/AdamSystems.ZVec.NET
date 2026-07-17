using FluentAssertions;
using ZVec.NET.Tests.Integration;

namespace ZVec.NET.Tests.Memory;

public class VectorAllocationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;

    public VectorAllocationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_alloc_{Guid.NewGuid():N}");
    }

    private void Setup()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();
        var schema = new ZVecCollectionSchema
        {
            Name = "alloc_test",
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
    public void Query_WithReadOnlyMemory_DoesNotAllocateVectorArrayCopy()
    {
        Setup();
        _collection.Should().NotBeNull();

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var memory = new ReadOnlyMemory<float>(vector);

        _collection!.Query(new ZVecQuery { FieldName = "embedding", Vector = memory }, topk: 1);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        long before = GC.GetAllocatedBytesForCurrentThread();

        _ = _collection.Query(new ZVecQuery { FieldName = "embedding", Vector = memory }, topk: 1);

        long after = GC.GetAllocatedBytesForCurrentThread();
        long allocated = after - before;

        // Result DTOs may allocate; the query vector itself must not be copied as float[].
        allocated.Should().BeLessThan(4096, "Vector query path should avoid copying query vector arrays.");
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
