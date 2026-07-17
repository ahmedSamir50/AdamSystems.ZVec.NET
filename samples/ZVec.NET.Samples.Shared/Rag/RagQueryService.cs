using ZVec.NET.Samples.Shared.LmStudio;
using ZVec.NET.Samples.Shared.Models;

namespace ZVec.NET.Samples.Shared.Rag;

public sealed class RagQueryService
{
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
        var vector = await _embeddings.EmbedAsync(question, ct).ConfigureAwait(false);
        var hits = await _collection.QueryAsync(
            d => d.Embedding,
            vector,
            topK,
            filter: null,
            includeVector: false,
            ct).ConfigureAwait(false);

        return hits.Select(h => new RagCitation(
            h.Record.Id,
            h.Record.Title,
            h.Record.Source,
            Truncate(h.Record.ChunkText, 240),
            h.Score)).ToArray();
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "…";
}
