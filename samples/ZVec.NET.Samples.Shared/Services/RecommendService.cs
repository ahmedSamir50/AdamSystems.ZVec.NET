using ZVec.NET.Samples.Shared.LmStudio;
using ZVec.NET.Samples.Shared.Models;

namespace ZVec.NET.Samples.Shared.Services;

public sealed record RecommendHit(string Id, string Title, string Category, string Description, float Score);

/// <summary>Content-based similar-item recommendations via embeddings.</summary>
public sealed class RecommendService
{
    private readonly IZvecCollection<RecommendItem> _collection;
    private readonly IEmbeddingClient _embeddings;

    public RecommendService(IZvecCollection<RecommendItem> collection, IEmbeddingClient embeddings)
    {
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
    }

    public async Task<int> UpsertItemsAsync(
        IReadOnlyList<RecommendItem> itemsWithoutVectors,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(itemsWithoutVectors);
        if (itemsWithoutVectors.Count == 0)
            return 0;

        var texts = itemsWithoutVectors
            .Select(i => $"{i.Title}. {i.Category}. {i.Description}")
            .ToArray();

        progress?.Report($"Embedding {texts.Length} item(s)…");
        var vectors = await _embeddings.EmbedBatchAsync(texts, ct).ConfigureAwait(false);

        var docs = new List<RecommendItem>(itemsWithoutVectors.Count);
        for (var i = 0; i < itemsWithoutVectors.Count; i++)
        {
            var src = itemsWithoutVectors[i];
            docs.Add(new RecommendItem
            {
                Id = src.Id,
                Title = src.Title,
                Category = src.Category,
                Description = src.Description,
                Embedding = vectors[i]
            });
        }

        progress?.Report($"Upserting {docs.Count} item(s)…");
        foreach (var doc in docs)
            await _collection.UpsertAsync(doc, ct).ConfigureAwait(false);
        return docs.Count;
    }

    public async Task<IReadOnlyList<RecommendHit>> SimilarAsync(
        string queryText,
        int topK = SampleDefaults.DefaultTopK,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);
        var vector = await _embeddings.EmbedAsync(queryText, ct).ConfigureAwait(false);
        var hits = await _collection.QueryAsync(
            d => d.Embedding,
            vector,
            topK,
            filter: null,
            includeVector: false,
            ct).ConfigureAwait(false);

        return hits.Select(h => new RecommendHit(
            h.Record.Id,
            h.Record.Title,
            h.Record.Category,
            Truncate(h.Record.Description, 200),
            h.Score)).ToArray();
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";
}
