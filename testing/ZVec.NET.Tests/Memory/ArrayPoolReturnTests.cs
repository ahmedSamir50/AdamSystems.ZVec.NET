using System.Buffers;
using FluentAssertions;
using ZVec.NET.Interop;

namespace ZVec.NET.Tests.Memory;

/// <summary>US-E19.3 — ArrayPool return verification for <see cref="VectorMarshaller"/> sparse buffers.</summary>
public class ArrayPoolReturnTests
{
    [Fact]
    public void VectorMarshaller_ReturnSparseArrays_RecyclesRentedBuffers()
    {
        var sparse = new Dictionary<int, float>
        {
            [2] = 0.2f,
            [0] = 1.0f,
            [1] = 0.5f
        };

        VectorMarshaller.SerializeSparseVector(sparse, out int[] firstIndices, out float[] firstValues, out int count);
        count.Should().Be(3);
        firstIndices.Take(count).Should().Equal(0, 1, 2);

        VectorMarshaller.ReturnSparseArrays(firstIndices, firstValues);

        int[] rentedIndices = ArrayPool<int>.Shared.Rent(3);
        float[] rentedValues = ArrayPool<float>.Shared.Rent(3);
        try
        {
            rentedIndices[0] = 99;
            rentedValues[0] = 99f;
            ArrayPool<int>.Shared.Return(rentedIndices);
            ArrayPool<float>.Shared.Return(rentedValues);
        }
        catch
        {
            ArrayPool<int>.Shared.Return(rentedIndices);
            ArrayPool<float>.Shared.Return(rentedValues);
            throw;
        }

        VectorMarshaller.SerializeSparseVector(sparse, out int[] secondIndices, out float[] secondValues, out count);
        try
        {
            count.Should().Be(3);
            secondIndices.Take(count).Should().Equal(0, 1, 2);
            secondValues.Take(count).Should().Equal(1.0f, 0.5f, 0.2f);
        }
        finally
        {
            VectorMarshaller.ReturnSparseArrays(secondIndices, secondValues);
        }
    }

    [Fact]
    public void VectorMarshaller_MultipleRentReturnCycles_DoNotLeak()
    {
        var sparse = new Dictionary<int, float> { [1] = 1.0f };

        for (int cycle = 0; cycle < 32; cycle++)
        {
            VectorMarshaller.SerializeSparseVector(sparse, out int[] indices, out float[] values, out int count);
            try
            {
                count.Should().Be(1);
                indices[0].Should().Be(1);
                values[0].Should().Be(1.0f);
            }
            finally
            {
                VectorMarshaller.ReturnSparseArrays(indices, values);
            }
        }
    }
}
