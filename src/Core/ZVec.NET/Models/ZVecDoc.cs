namespace ZVec.NET;

/// <summary>
/// Document DTO. Dense vectors use <see cref="ReadOnlyMemory{T}"/> for zero-copy pipelines.
/// Scalar <see cref="Fields"/> values are boxed (<c>object</c>) for Python/Node parity.
/// </summary>
public sealed class ZVecDoc
{
    /// <summary>Unique identifier of the document.</summary>
    public required string Id { get; init; }

    /// <summary>Dense vectors associated with the document, mapped by field name.</summary>
    public IReadOnlyDictionary<string, ReadOnlyMemory<float>> DenseVectors { get; init; } =
        new Dictionary<string, ReadOnlyMemory<float>>();

    /// <summary>Sparse vectors associated with the document, mapped by field name. Mapped as {dimension_index: weight}.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, float>> SparseVectors { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<int, float>>();

    /// <summary>
    /// Scalar fields. Values are boxed (<c>object</c>) for Python/Node parity.
    /// Zero-allocation goal applies to vector paths only.
    /// </summary>
    public IReadOnlyDictionary<string, object> Fields { get; init; } =
        new Dictionary<string, object>();

    /// <summary>Score assigned to the document in search results.</summary>
    public float Score { get; init; }

    /// <summary>Creates a new document instance with validation.</summary>
    /// <param name="id">The document ID.</param>
    /// <param name="denseVectors">Optional dense vectors.</param>
    /// <param name="sparseVectors">Optional sparse vectors.</param>
    /// <param name="fields">Optional scalar fields.</param>
    /// <returns>A validated ZVecDoc instance.</returns>
    public static ZVecDoc Create(
        string id,
        IReadOnlyDictionary<string, ReadOnlyMemory<float>>? denseVectors = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, float>>? sparseVectors = null,
        IReadOnlyDictionary<string, object>? fields = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return new ZVecDoc
        {
            Id = id,
            DenseVectors = denseVectors ?? new Dictionary<string, ReadOnlyMemory<float>>(),
            SparseVectors = sparseVectors ?? new Dictionary<string, IReadOnlyDictionary<int, float>>(),
            Fields = fields ?? new Dictionary<string, object>()
        };
    }
}
