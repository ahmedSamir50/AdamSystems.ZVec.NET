namespace AdamSystems.ZVec.NET;

/// <summary>Full-text search query payload.</summary>
public sealed class ZVecFtsQuery
{
    public string? MatchString { get; init; }
    public string? QueryString { get; init; }
    public ZVecFtsDefaultOperator DefaultOperator { get; init; } = ZVecFtsDefaultOperator.Or;
}
