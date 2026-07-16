namespace ZVec.NET;

/// <summary>Group-by query wrapping a base <see cref="ZVecQuery"/>.</summary>
public sealed class ZVecGroupByQuery
{
    /// <summary>The underlying vector, FTS, or ID search query.</summary>
    public required ZVecQuery Query { get; init; }

    /// <summary>The scalar field name to group results by.</summary>
    public required string GroupByField { get; init; }

    /// <summary>Maximum number of documents to return per group. Default is 1.</summary>
    public int GroupSize { get; init; } = ZVecDefaults.Query.GroupSize;

    /// <summary>Number of groups to return (Top-K groups). Default is 10.</summary>
    public int Topk { get; init; } = ZVecDefaults.Query.Topk;

    /// <summary>Optional scalar filter expression applied before grouping.</summary>
    public string? Filter { get; init; }
}
