using System.Text.RegularExpressions;
using ZVec.NET.Samples.Shared.LmStudio;
using ZVec.NET.Samples.Shared.Models;

namespace ZVec.NET.Samples.Shared.Rag;

public sealed class RagQueryService
{
    private const int UiSnippetChars = 240;
    private const int LlmContextChars = 1500;
    private const int OverFetchFactor = 3;
    private const int MaxFetch = 48;
    private const int NearDupPrefixChars = 100;

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly IZvecCollection<RagDocument> _collection;
    private readonly IEmbeddingClient _embeddings;

    public RagQueryService(IZvecCollection<RagDocument> collection, IEmbeddingClient embeddings)
    {
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
    }

    public async Task<IReadOnlyList<RagCitation>> QueryAsync(
        string question,
        int topK = SampleDefaults.DefaultTopK,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        if (topK < 1)
            topK = SampleDefaults.DefaultTopK;

        var fetchK = Math.Clamp(topK * OverFetchFactor, topK, MaxFetch);
        var vector = await _embeddings.EmbedAsync(question, ct).ConfigureAwait(false);
        var hits = await _collection.QueryAsync(
            d => d.Embedding,
            vector,
            fetchK,
            filter: null,
            includeVector: false,
            ct).ConfigureAwait(false);

        // Hits are already score-descending (most relevant first).
        var mapped = hits.Select(h =>
        {
            var full = h.Record.ChunkText ?? "";
            return new RagCitation(
                h.Record.Id,
                h.Record.Title,
                h.Record.Source,
                Truncate(full, UiSnippetChars),
                Truncate(full, LlmContextChars),
                h.Score);
        });

        return DedupeNearIdentical(mapped, topK);
    }

    /// <summary>Keep score order; drop near-duplicate chunk text so chat does not cite clones as [1][2][3].</summary>
    internal static IReadOnlyList<RagCitation> DedupeNearIdentical(IEnumerable<RagCitation> ordered, int topK)
    {
        var kept = new List<RagCitation>(topK);
        var norms = new List<string>(topK);

        foreach (var c in ordered)
        {
            var norm = Normalize(c.ContextText);
            if (string.IsNullOrEmpty(norm))
                continue;

            if (norms.Any(existing => IsNearDuplicate(existing, norm)))
                continue;

            kept.Add(c);
            norms.Add(norm);
            if (kept.Count >= topK)
                break;
        }

        return kept;
    }

    private static bool IsNearDuplicate(string a, string b)
    {
        if (a == b)
            return true;

        var n = Math.Min(NearDupPrefixChars, Math.Min(a.Length, b.Length));
        if (n >= 40 && a.AsSpan(0, n).SequenceEqual(b.AsSpan(0, n)))
            return true;

        if (a.Length >= 60 && b.Length >= 60)
        {
            if (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        return Whitespace.Replace(text.Trim(), " ");
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";
}
