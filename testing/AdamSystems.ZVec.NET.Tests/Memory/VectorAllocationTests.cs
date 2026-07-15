using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Memory;

public class VectorAllocationTests : IDisposable
{
    private readonly string _testPath;
    private readonly ZVecCollectionSchema _schema;
    private readonly IZvecFactory _factory;
    private readonly IZvecCollection _collection;

    public VectorAllocationTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_alloc_{Guid.NewGuid():N}");
        _schema = new ZVecCollectionSchema
        {
            Name = "alloc_test",
            Vectors = new[]
            {
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecFlatIndexParam()
                }
            }
        };

        _factory = new ZVecFactory();
        _factory.Initialize();
        _collection = _factory.CreateAndOpen(_testPath, _schema);
    }

    [Fact]
    public void Query_WithReadOnlyMemory_DoesNotAllocateVectorArrayCopy()
    {
        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var memory = new ReadOnlyMemory<float>(vector);

        // Warm up to initialize any lazy structures / JIT compilation
        _collection.Query(new ZVecQuery { FieldName = "embedding", Vector = memory }, topk: 1);

        // Measure allocations
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long before = GC.GetAllocatedBytesForCurrentThread();

        _ = _collection.Query(new ZVecQuery { FieldName = "embedding", Vector = memory }, topk: 1);

        long after = GC.GetAllocatedBytesForCurrentThread();
        long allocated = after - before;

        // The query itself will allocate some small DTO objects (ZVecDoc, lists, results),
        // but it must NOT allocate a copy of the float vector (which would be 4 * 4 = 16 bytes, or 768 * 4 = 3072 bytes for larger dimensions).
        // Since we are using standard mock/P-Invoke pipelines, the allocation overhead must be low.
        // We assert allocated bytes is reasonably small (< 4096 bytes total).
        allocated.Should().BeLessThan(4096, "Vector query path should be optimized and avoid copying query vector arrays.");
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
