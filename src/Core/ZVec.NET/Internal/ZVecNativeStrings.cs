namespace ZVec.NET.Internal;

/// <summary>
/// Maps managed enums to exact native C API string literals.
/// Not part of the public SDK surface — used by interop/marshalling only.
/// </summary>
internal static class ZVecNativeStrings
{
    internal static string ToNative(ZVecFtsTokenizer value) => value switch
    {
        ZVecFtsTokenizer.Standard => "standard",
        ZVecFtsTokenizer.Jieba => "jieba",
        ZVecFtsTokenizer.Whitespace => "whitespace",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    internal static string ToNative(ZVecFtsTokenFilter value) => value switch
    {
        ZVecFtsTokenFilter.Lowercase => "lowercase",
        ZVecFtsTokenFilter.AsciiFolding => "ascii_folding",
        ZVecFtsTokenFilter.Stemmer => "stemmer",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    internal static string ToNative(ZVecJiebaCutMode value) => value switch
    {
        ZVecJiebaCutMode.Search => "search",
        ZVecJiebaCutMode.Mix => "mix",
        ZVecJiebaCutMode.Full => "full",
        ZVecJiebaCutMode.Hmm => "hmm",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    internal static string ToNative(ZVecFtsDefaultOperator value) => value switch
    {
        ZVecFtsDefaultOperator.Or => "OR",
        ZVecFtsDefaultOperator.And => "AND",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}
