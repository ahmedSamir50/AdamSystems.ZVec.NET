using FluentAssertions;
using ZVec.NET.Interop;

namespace ZVec.NET.Tests.Unit.Interop;

public class VectorMarshallerTests
{
    [Fact]
    public void SerializeSparseVector_SortsIndices_And_ReturnSparseArrays_DoesNotThrow()
    {
        var sparse = new Dictionary<int, float>
        {
            [5] = 0.5f,
            [1] = 1.0f,
            [3] = 0.3f
        };

        VectorMarshaller.SerializeSparseVector(sparse, out int[] indices, out float[] values, out int count);
        try
        {
            count.Should().Be(3);
            indices.Take(count).Should().Equal(1, 3, 5);
            values.Take(count).Should().Equal(1.0f, 0.3f, 0.5f);
        }
        finally
        {
            VectorMarshaller.ReturnSparseArrays(indices, values);
        }

        // Second rent/return cycle verifies pool recycle path.
        VectorMarshaller.SerializeSparseVector(sparse, out indices, out values, out count);
        try
        {
            count.Should().Be(3);
        }
        finally
        {
            VectorMarshaller.ReturnSparseArrays(indices, values);
        }
    }

    [Fact]
    public void PinVector_ReturnsPointer_ForReadOnlyMemory()
    {
        var memory = new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f, 0.4f]);
        var (ptr, pin) = VectorMarshaller.PinVector(memory);
        try
        {
            ptr.Should().NotBe(IntPtr.Zero);
        }
        finally
        {
            pin.Dispose();
        }
    }
}
