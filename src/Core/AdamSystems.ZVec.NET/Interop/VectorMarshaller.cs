using System.Buffers;

namespace AdamSystems.ZVec.NET.Interop;

/// <summary>
/// Provides zero-copy vector pinning and sparse vector serialization using rented buffers.
/// </summary>
internal static class VectorMarshaller
{
    /// <summary>
    /// Pins a ReadOnlyMemory vector in memory and returns a native pointer and its handle.
    /// The caller MUST dispose the MemoryHandle to unpin the memory after P/Invoke call.
    /// </summary>
    internal static unsafe (IntPtr ptr, MemoryHandle pin) PinVector(ReadOnlyMemory<float> vector)
    {
        var pin = vector.Pin();
        return ((IntPtr)pin.Pointer, pin);
    }

    /// <summary>
    /// Rents buffers from ArrayPool and serializes a sparse vector sorted by index.
    /// The caller MUST call ReturnSparseArrays when done.
    /// </summary>
    internal static void SerializeSparseVector(
        IReadOnlyDictionary<int, float> sparse,
        out int[] indices,
        out float[] values,
        out int count)
    {
        count = sparse.Count;
        indices = ArrayPool<int>.Shared.Rent(count);
        values = ArrayPool<float>.Shared.Rent(count);

        int i = 0;
        // Native sparse vector requires sorted indices
        foreach (var kvp in sparse.OrderBy(kv => kv.Key))
        {
            indices[i] = kvp.Key;
            values[i] = kvp.Value;
            i++;
        }
    }

    /// <summary>
    /// Returns rented arrays back to the shared ArrayPool.
    /// </summary>
    internal static void ReturnSparseArrays(int[] indices, float[] values)
    {
        ArrayPool<int>.Shared.Return(indices);
        ArrayPool<float>.Shared.Return(values);
    }
}
