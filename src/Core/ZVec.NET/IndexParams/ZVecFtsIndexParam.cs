namespace ZVec.NET;

/// <summary>Full-text search index parameters.</summary>
public sealed class ZVecFtsIndexParam : ZVecIndexParam
{
    /// <summary>Tokenizer type (e.g. Standard, Jieba). Default is Standard.</summary>
    public ZVecFtsTokenizer Tokenizer { get; init; } = ZVecDefaults.Fts.Tokenizer;

    /// <summary>Token filters to apply (e.g. Lowercase). Default includes Lowercase.</summary>
    public IReadOnlyList<ZVecFtsTokenFilter> Filters { get; init; } = ZVecDefaults.Fts.Filters;

    /// <summary>Optional extra parameters (like custom directories or dictionary configurations).</summary>
    public ZVecFtsExtraParams? ExtraParams { get; init; }
}
