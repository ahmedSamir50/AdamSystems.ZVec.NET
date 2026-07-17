namespace ZVec.NET.Samples.Shared;

/// <summary>Fail-fast validation that embeddings match the mapped vector dimension.</summary>
public static class EmbeddingDimension
{
    public static void Ensure(ReadOnlyMemory<float> embedding, int expected = SampleDefaults.VectorDimensions)
    {
        if (embedding.Length != expected)
        {
            throw new InvalidOperationException(
                string.Format(SampleDefaults.DimMismatchFormat, embedding.Length, expected));
        }
    }
}
