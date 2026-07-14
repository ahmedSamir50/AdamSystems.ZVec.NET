namespace AdamSystems.ZVec.NET;

/// <summary>Group-by query wrapping a base <see cref="ZVecQuery"/>.</summary>
public sealed class ZVecGroupByQuery
{
    public required ZVecQuery Query { get; init; }
    public required string GroupByField { get; init; }
    public int GroupSize { get; init; } = ZVecDefaults.Query.GroupSize;
    public int Topk { get; init; } = ZVecDefaults.Query.Topk;
    public string? Filter { get; init; }
}
