namespace AdamSystems.ZVec.NET;

/// <summary>
/// Collection statistics: document count and per-index build completeness (0.0–1.0).
/// Maps to Python/Node <c>collection.stats</c> (<c>doc_count</c>, <c>index_completeness</c>).
/// </summary>
public sealed class ZVecCollectionStats
{
    public long DocCount { get; init; }
    public IReadOnlyDictionary<string, float> IndexCompleteness { get; init; } =
        new Dictionary<string, float>();
}
