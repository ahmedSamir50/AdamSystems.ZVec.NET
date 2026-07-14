namespace AdamSystems.ZVec.NET;

/// <summary>
/// Document DTO. Dense vectors use <see cref="ReadOnlyMemory{T}"/> for zero-copy pipelines.
/// Scalar <see cref="Fields"/> values are boxed (<c>object</c>) for Python/Node parity.
/// </summary>
public sealed class ZVecDoc
{
    public required string Id { get; init; }
    public IReadOnlyDictionary<string, ReadOnlyMemory<float>> DenseVectors { get; init; } =
        new Dictionary<string, ReadOnlyMemory<float>>();
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, float>> SparseVectors { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<int, float>>();

    /// <summary>
    /// Scalar fields. Values are boxed (<c>object</c>) for Python/Node parity.
    /// Zero-allocation goal applies to vector paths only.
    /// </summary>
    public IReadOnlyDictionary<string, object> Fields { get; init; } =
        new Dictionary<string, object>();

    public float Score { get; init; }

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
