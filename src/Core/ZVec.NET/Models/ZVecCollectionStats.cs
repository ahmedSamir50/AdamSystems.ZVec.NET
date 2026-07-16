namespace ZVec.NET;

/// <summary>
/// Collection statistics: document count and per-index build completeness (0.0–1.0).
/// Maps to Python/Node <c>collection.stats</c> (<c>doc_count</c>, <c>index_completeness</c>).
/// </summary>
public sealed class ZVecCollectionStats
{
    /// <summary>Total document count in the collection.</summary>
    public long DocCount { get; init; }

    /// <summary>Index construction completeness percentage (from 0.0 to 1.0) mapped by index name.</summary>
    public IReadOnlyDictionary<string, float> IndexCompleteness { get; init; } =
        new Dictionary<string, float>();
}
