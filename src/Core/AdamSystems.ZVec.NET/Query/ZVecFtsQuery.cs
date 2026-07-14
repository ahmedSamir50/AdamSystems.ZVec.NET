namespace AdamSystems.ZVec.NET;

/// <summary>Full-text search query payload.</summary>
public sealed class ZVecFtsQuery
{
    /// <summary>The FTS match clause string (e.g. matching terms).</summary>
    public string? MatchString { get; init; }

    /// <summary>The FTS query expression string.</summary>
    public string? QueryString { get; init; }

    /// <summary>Default logical operator between query terms. Default is Or.</summary>
    public ZVecFtsDefaultOperator DefaultOperator { get; init; } = ZVecDefaults.Query.DefaultOperator;
}
