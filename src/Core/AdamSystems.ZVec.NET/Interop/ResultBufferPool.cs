using System.Buffers;

namespace AdamSystems.ZVec.NET.Interop;

/// <summary>
/// Provides pooled intermediate arrays for native result retrieval.
/// </summary>
internal static class ResultBufferPool
{
    /// <summary>
    /// Rents intermediate buffers from shared ArrayPool for document IDs and distance scores.
    /// </summary>
    internal static (int[] ids, float[] scores, int bufferSize) RentResultBuffer(int topk)
    {
        var ids = ArrayPool<int>.Shared.Rent(topk);
        var scores = ArrayPool<float>.Shared.Rent(topk);
        return (ids, scores, topk);
    }

    /// <summary>
    /// Returns rented result arrays back to the shared ArrayPool.
    /// </summary>
    internal static void ReturnResultBuffer(int[] ids, float[] scores)
    {
        ArrayPool<int>.Shared.Return(ids, clearArray: true);
        ArrayPool<float>.Shared.Return(scores, clearArray: true);
    }
}
