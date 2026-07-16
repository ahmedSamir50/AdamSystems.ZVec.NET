using ZVec.NET.Interop;
using FluentAssertions;

namespace ZVec.NET.Tests.Unit.Interop;

public class VectorMarshallerTests
{
    [Fact]
    public void PinVector_ReturnsValidPointerAndPin()
    {
        // US-E7.1: Pin dynamic vectors without copying
        var vec = new ReadOnlyMemory<float>([1.5f, 2.5f, 3.5f]);
        var (ptr, pin) = VectorMarshaller.PinVector(vec);

        ptr.Should().NotBe(IntPtr.Zero);
        pin.Dispose(); // must not throw
    }

    [Fact]
    public void SerializeSparseVector_SortsByIndex()
    {
        // US-E7.2: Serialize sparse vectors sorted by indices
        var sparse = new Dictionary<int, float> { [5] = 0.5f, [1] = 0.1f, [3] = 0.3f };
        
        VectorMarshaller.SerializeSparseVector(sparse, out var indices, out var values, out var count);
        
        count.Should().Be(3);
        indices[0].Should().Be(1);
        indices[1].Should().Be(3);
        indices[2].Should().Be(5);

        values[0].Should().Be(0.1f);
        values[1].Should().Be(0.3f);
        values[2].Should().Be(0.5f);

        VectorMarshaller.ReturnSparseArrays(indices, values);
    }


}
