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
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var chunks = _chunker.Chunk(text);
        progress?.Report($"Chunked into {chunks.Count} piece(s). Embedding…");

        var vectors = await _embeddings.EmbedBatchAsync(chunks, ct).ConfigureAwait(false);
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var docs = new List<SearchDocument>(chunks.Count);

        for (var i = 0; i < chunks.Count; i++)
        {
            docs.Add(new SearchDocument
            {
                Id = $"{Sanitize(source)}-{stamp}-{i}",
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
        var chars = value.Select(c => char.IsLetterOrDigit(c) ? c : '_').Take(40).ToArray();
        return new string(chars);
    }
}
