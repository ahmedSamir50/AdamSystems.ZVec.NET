using ZVec.NET.Samples.Shared.LmStudio;
using ZVec.NET.Samples.Shared.Models;
using ZVec.NET.Samples.Shared.Rag;

namespace ZVec.NET.Samples.Shared.Services;

/// <summary>Semantic search against the dedicated search collection.</summary>
public sealed class SearchQueryService
{
    private const int UiSnippetChars = 240;
    private const int LlmContextChars = 1500;

    private readonly IZvecCollection<SearchDocument> _collection;
    private readonly IEmbeddingClient _embeddings;

    public SearchQueryService(IZvecCollection<SearchDocument> collection, IEmbeddingClient embeddings)
    {
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
    }

    public long DocCount => _collection.Stats.DocCount;
    public string Path => _collection.Path;

    public async Task<IReadOnlyList<RagCitation>> QueryAsync(
        string question,
        int topK = SampleDefaults.DefaultTopK,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        var vector = await _embeddings.EmbedAsync(question, ct).ConfigureAwait(false);
        var hits = await _collection.QueryAsync(
            d => d.Embedding,
            vector,
            topK,
            filter: null,
            includeVector: false,
            ct).ConfigureAwait(false);

        return hits.Select(h =>
        {
            var full = h.Record.ChunkText ?? "";
            return new RagCitation(
                h.Record.Id,
                h.Record.Title,
                h.Record.Source,
                Truncate(full, UiSnippetChars),
                Truncate(full, LlmContextChars),
                h.Score);
        }).ToArray();
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";
}
