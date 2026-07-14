namespace AdamSystems.ZVec.NET;

/// <summary>Base type for multi-query fusion rerankers.</summary>
public abstract class ZVecReranker
{
}

/// <summary>Weighted score fusion across named query fields.</summary>
public sealed class ZVecWeightedReranker : ZVecReranker
{
    public int TopN { get; init; }
    public ZVecMetricType Metric { get; init; }
    public IReadOnlyDictionary<string, float> Weights { get; init; } =
        new Dictionary<string, float>();
}

/// <summary>Reciprocal Rank Fusion (RRF) reranker.</summary>
public sealed class ZVecRrfReranker : ZVecReranker
{
    public int TopN { get; init; }

    /// <summary>Native <c>rank_constant</c> (int). Default 60.</summary>
    public int RankConstant { get; init; } = ZVecDefaults.Rerank.RankConstant;
}
