using ZVec.NET.Samples.Shared.LmStudio;
using ZVec.NET.Samples.Shared.Models;
using ZVec.NET.Samples.Shared.Rag;

namespace ZVec.NET.Samples.Shared.Services;

/// <summary>Ingests text into the dedicated search collection.</summary>
public sealed class SearchIngestService
{
    private readonly IZvecCollection<SearchDocument> _collection;
    private readonly IEmbeddingClient _embeddings;
    private readonly TextChunker _chunker;

    public SearchIngestService(
        IZvecCollection<SearchDocument> collection,
        IEmbeddingClient embeddings,
        TextChunker? chunker = null)
    {
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _chunker = chunker ?? new TextChunker();
    }

    public long DocCount => _collection.Stats.DocCount;

    public async Task<IngestResult> IngestTextAsync(
        string title,
        string source,
        string text,
        string tags = "",
        IProgress<string>? progress = null,
        CancellationToken ct = default,
        string? stableId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var chunks = _chunker.Chunk(text);
        progress?.Report($"Chunked into {chunks.Count} piece(s). Embedding…");

        var embedTexts = chunks
            .Select(c => string.IsNullOrWhiteSpace(title) ? c : $"{title}\n{c}")
            .ToArray();
        var vectors = await _embeddings.EmbedBatchAsync(embedTexts, ct).ConfigureAwait(false);
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var docs = new List<SearchDocument>(chunks.Count);
        var idBase = string.IsNullOrWhiteSpace(stableId) ? null : Sanitize(stableId);

        for (var i = 0; i < chunks.Count; i++)
        {
            docs.Add(new SearchDocument
            {
                Id = idBase is null
                    ? $"{Sanitize(source)}-{stamp}-{i}"
                    : chunks.Count == 1
                        ? idBase
                        : $"{idBase}-{i}",
                Title = title,
                Source = source,
                ChunkText = chunks[i],
                Tags = tags,
                Embedding = vectors[i]
            });
        }

        progress?.Report($"Upserting {docs.Count} chunk(s) into search collection…");
        foreach (var doc in docs)
            await _collection.UpsertAsync(doc, ct).ConfigureAwait(false);
        return new IngestResult(docs.Count, source);
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) ? c : '_').Take(80).ToArray();
        return new string(chars);
    }
}
