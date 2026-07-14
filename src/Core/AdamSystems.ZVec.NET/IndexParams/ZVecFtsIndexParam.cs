namespace AdamSystems.ZVec.NET;

/// <summary>Full-text search index parameters.</summary>
public sealed class ZVecFtsIndexParam : ZVecIndexParam
{
    public ZVecFtsTokenizer Tokenizer { get; init; } = ZVecDefaults.Fts.Tokenizer;
    public IReadOnlyList<ZVecFtsTokenFilter> Filters { get; init; } = ZVecDefaults.Fts.Filters;
    public ZVecFtsExtraParams? ExtraParams { get; init; }
}
