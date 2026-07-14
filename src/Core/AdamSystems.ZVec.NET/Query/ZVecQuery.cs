namespace AdamSystems.ZVec.NET;

/// <summary>Single-field vector / FTS / ID query.</summary>
public sealed class ZVecQuery
{
    public required string FieldName { get; init; }
    public ReadOnlyMemory<float>? Vector { get; init; }
    public IReadOnlyDictionary<int, float>? SparseVector { get; init; }
    public string? DocumentId { get; init; }
    public ZVecFtsQuery? Fts { get; init; }

    /// <summary>Prefer typed <see cref="QueryParams"/> over opaque dictionaries.</summary>
    public ZVecQueryParams? QueryParams { get; init; }
}
