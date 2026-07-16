namespace ZVec.NET;

/// <summary>Single-field vector, FTS, or ID search query.</summary>
public sealed class ZVecQuery
{
    /// <summary>The name of the target schema field (vector or FTS column) to query against.</summary>
    public required string FieldName { get; init; }

    /// <summary>Optional dense vector query representation (zero-copy).</summary>
    public ReadOnlyMemory<float>? Vector { get; init; }

    /// <summary>Optional sparse vector query representation as {dimension_index: weight}.</summary>
    public IReadOnlyDictionary<int, float>? SparseVector { get; init; }

    /// <summary>Optional document ID to search for similar documents based on an existing document.</summary>
    public string? DocumentId { get; init; }

    /// <summary>Optional Full-Text Search (FTS) sub-query.</summary>
    public ZVecFtsQuery? Fts { get; init; }

    /// <summary>Optional index-specific search parameters (e.g. ef_search, nprobe). Prefer typed subclasses over dictionaries.</summary>
    public ZVecQueryParams? QueryParams { get; init; }
}
