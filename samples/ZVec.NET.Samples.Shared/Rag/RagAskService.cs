using ZVec.NET.Samples.Shared.LmStudio;

namespace ZVec.NET.Samples.Shared.Rag;

public sealed class RagAskService
{
    private readonly RagQueryService _query;
    private readonly IChatClient _chat;
    private readonly LmStudioOptions _options;

    public RagAskService(RagQueryService query, IChatClient chat, LmStudioOptions options)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<RagAskResult> AskAsync(
        string question,
        int topK = SampleDefaults.DefaultTopK,
        CancellationToken ct = default)
    {
        var citations = await _query.QueryAsync(question, topK, ct).ConfigureAwait(false);
        if (citations.Count == 0)
        {
            return new RagAskResult(
                "No matching chunks were found. Ingest documents first.",
                citations,
                UsedChat: false);
        }

        var context = string.Join(
            "\n\n",
            citations.Select((c, i) => $"[{i + 1}] ({c.Title} / {c.Source})\n{c.Snippet}"));

        var system = """
            You are a helpful assistant for an offline edge RAG demo using ZVec.NET.
            Answer using only the provided context. If the context is insufficient, say so.
            Cite sources like [1], [2] when you use them.
            """;

        var user = $"Context:\n{context}\n\nQuestion: {question}";

        try
        {
            var answer = await _chat.CompleteAsync(system, user, ct).ConfigureAwait(false);
            return new RagAskResult(answer, citations, UsedChat: true);
        }
        catch (InvalidOperationException)
        {
            var fallback =
                $"Chat model unavailable (load '{_options.ChatModel}' in LM Studio alongside embeddings). " +
                "Showing retrieved citations only.\n\n" +
                string.Join("\n", citations.Select((c, i) => $"[{i + 1}] {c.Title}: {c.Snippet}"));
            return new RagAskResult(fallback, citations, UsedChat: false);
        }
    }
}
