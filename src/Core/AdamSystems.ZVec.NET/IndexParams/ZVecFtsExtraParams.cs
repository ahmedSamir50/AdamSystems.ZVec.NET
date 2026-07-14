using System.Globalization;
using System.Text;
using AdamSystems.ZVec.NET.Internal;

namespace AdamSystems.ZVec.NET;

/// <summary>
/// Optional FTS tokenizer/filter knobs. Serialized to the native <c>extra_params</c> JSON object.
/// Only set properties that apply to the chosen <see cref="ZVecFtsTokenizer"/> / filters;
/// irrelevant keys would be ignored by native if sent.
/// </summary>
public sealed class ZVecFtsExtraParams
{
    /// <summary>standard only. Positive int; native default 255, range [1, 1048576].</summary>
    public int? MaxTokenLength { get; init; }

    /// <summary>jieba only. Directory containing jieba.dict.utf8 and hmm_model.utf8.</summary>
    public string? JiebaDictDir { get; init; }

    /// <summary>jieba only. Optional user dictionary path.</summary>
    public string? UserDictPath { get; init; }

    /// <summary>jieba only. Native default is search.</summary>
    public ZVecJiebaCutMode? CutMode { get; init; }

    /// <summary>
    /// stemmer filter only. Snowball language/algorithm (native default "english";
    /// e.g. "porter" for ES-like behaviour). Kept as string — Snowball set is large/open.
    /// </summary>
    public string? StemmerLang { get; init; }

    /// <summary>
    /// Builds the native JSON object string for <c>zvec_index_params_set_fts_params</c>,
    /// or <c>null</c> when no properties are set.
    /// </summary>
    internal string? ToNativeJson()
    {
        var parts = new List<string>(5);

        if (MaxTokenLength is { } maxTokenLength)
        {
            parts.Add(FormattableString.Invariant(
                $"\"max_token_length\":{maxTokenLength.ToString(CultureInfo.InvariantCulture)}"));
        }

        if (JiebaDictDir is not null)
        {
            parts.Add($"\"jieba_dict_dir\":{QuoteJsonString(JiebaDictDir)}");
        }

        if (UserDictPath is not null)
        {
            parts.Add($"\"user_dict_path\":{QuoteJsonString(UserDictPath)}");
        }

        if (CutMode is { } cutMode)
        {
            parts.Add($"\"cut_mode\":{QuoteJsonString(ZVecNativeStrings.ToNative(cutMode))}");
        }

        if (StemmerLang is not null)
        {
            parts.Add($"\"stemmer_lang\":{QuoteJsonString(StemmerLang)}");
        }

        if (parts.Count == 0)
        {
            return null;
        }

        return "{" + string.Join(",", parts) + "}";
    }

    private static string QuoteJsonString(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (ch < ' ')
                    {
                        sb.Append("\\u");
                        sb.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }
}
