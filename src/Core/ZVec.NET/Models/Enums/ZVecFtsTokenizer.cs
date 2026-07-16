namespace ZVec.NET;

/// <summary>
/// FTS tokenizer pipeline name. Maps to native string literals for
/// <c>zvec_index_params_set_fts_params</c> (<c>"standard"</c>, <c>"jieba"</c>, <c>"whitespace"</c>).
/// </summary>
public enum ZVecFtsTokenizer
{
    Standard = 0,
    Jieba = 1,
    Whitespace = 2
}
