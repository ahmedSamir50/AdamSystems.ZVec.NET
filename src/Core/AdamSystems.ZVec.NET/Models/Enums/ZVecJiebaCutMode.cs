namespace AdamSystems.ZVec.NET;

/// <summary>
/// Jieba cut mode for FTS <c>extra_params.cut_mode</c>.
/// Native values: <c>"search"</c>, <c>"mix"</c>, <c>"full"</c>, <c>"hmm"</c>.
/// </summary>
public enum ZVecJiebaCutMode
{
    Search = 0,
    Mix = 1,
    Full = 2,
    Hmm = 3
}
