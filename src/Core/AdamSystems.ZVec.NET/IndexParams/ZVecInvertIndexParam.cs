namespace AdamSystems.ZVec.NET;

/// <summary>Inverted index parameters for scalar fields.</summary>
public sealed class ZVecInvertIndexParam : ZVecIndexParam
{
    /// <summary>Whether to enable range queries optimization. Default is false.</summary>
    public bool EnableRangeOptimization { get; init; } = ZVecDefaults.Invert.EnableRangeOptimization;

    /// <summary>Whether to enable extended wildcard search. Default is false.</summary>
    public bool EnableExtendedWildcard { get; init; } = ZVecDefaults.Invert.EnableExtendedWildcard;
}
