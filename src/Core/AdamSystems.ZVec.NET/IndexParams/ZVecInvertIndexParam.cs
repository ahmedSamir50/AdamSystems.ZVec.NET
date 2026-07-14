namespace AdamSystems.ZVec.NET;

/// <summary>Inverted index parameters for scalar fields.</summary>
public sealed class ZVecInvertIndexParam : ZVecIndexParam
{
    public bool EnableRangeOptimization { get; init; } = false;
    public bool EnableExtendedWildcard { get; init; } = false;
}
