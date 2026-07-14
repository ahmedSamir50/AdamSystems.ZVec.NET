namespace AdamSystems.ZVec.NET;

/// <summary>Base type for multi-query fusion rerankers.</summary>
public abstract class ZVecReranker
{
}

/// <summary>Weighted score fusion across named query fields.</summary>
public sealed class ZVecWeightedReranker : ZVecReranker
{
    /// <summary>The final top-N count of merged results to return. Default is 0 (returns all merged).</summary>
    public int TopN { get; init; }

    /// <summary>The metric type to apply during scoring. Default is Undefined.</summary>
    public ZVecMetricType Metric { get; init; }

    /// <summary>Weight mapping for each named query, indicating its contribution to the final score.</summary>
    public IReadOnlyDictionary<string, float> Weights { get; init; } =
        new Dictionary<string, float>();
}

/// <summary>Reciprocal Rank Fusion (RRF) reranker.</summary>
public sealed class ZVecRrfReranker : ZVecReranker
{
    /// <summary>The final top-N count of merged results to return. Default is 0 (returns all merged).</summary>
    public int TopN { get; init; }

    /// <summary>Native <c>rank_constant</c> (int) used in RRF scoring. Default is 60.</summary>
    public int RankConstant { get; init; } = ZVecDefaults.Rerank.RankConstant;
}
