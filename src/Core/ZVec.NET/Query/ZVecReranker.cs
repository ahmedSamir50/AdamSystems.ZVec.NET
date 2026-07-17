namespace ZVec.NET;

/// <summary>Base type for multi-query fusion rerankers.</summary>
public abstract class ZVecReranker
{
}

/// <summary>Weighted score fusion across named query fields.</summary>
public sealed class ZVecWeightedReranker : ZVecReranker
{
    /// <summary>The final top-N count of merged results to return. Default is 0 (returns all merged).</summary>
    public int TopN { get; init; }

    /// <summary>
    /// The metric applied when fusing sub-query scores into the final ranked list.
    /// Each sub-query's raw score is scaled by its entry in <see cref="Weights"/> before aggregation.
    /// Default is <see cref="ZVecMetricType.Undefined"/> (native default fusion metric).
    /// </summary>
    public ZVecMetricType Metric { get; init; }

    /// <summary>Weight mapping for each named query, indicating its contribution to the final score.</summary>
    public IReadOnlyDictionary<string, float> Weights { get; init; } =
        new Dictionary<string, float>();

    /// <summary>
    /// Validates <paramref name="weights"/> for a multi-query weighted rerank.
    /// </summary>
    /// <exception cref="ArgumentNullException">When <paramref name="weights"/> is null.</exception>
    /// <exception cref="ArgumentException">When weights are empty or do not match <paramref name="subQueryCount"/>.</exception>
    public static void ValidateWeights(IReadOnlyDictionary<string, float> weights, int subQueryCount)
    {
        ArgumentNullException.ThrowIfNull(weights);
        if (weights.Count == 0 || weights.Count != subQueryCount)
            throw new ArgumentException(ZVecDefaults.Errors.RerankerWeightsInvalid, nameof(weights));
    }
}

/// <summary>Reciprocal Rank Fusion (RRF) reranker.</summary>
public sealed class ZVecRrfReranker : ZVecReranker
{
    /// <summary>The final top-N count of merged results to return. Default is 0 (returns all merged).</summary>
    public int TopN { get; init; }

    /// <summary>Native <c>rank_constant</c> (int) used in RRF scoring. Default is 60.</summary>
    public int RankConstant { get; init; } = ZVecDefaults.Rerank.RankConstant;
}
