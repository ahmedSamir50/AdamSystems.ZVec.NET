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

    public Task<IReadOnlyList<RagCitation>> RetrieveAsync(
        string question,
        int topK = SampleDefaults.DefaultTopK,
        CancellationToken ct = default)
        => _query.QueryAsync(question, topK, ct);

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

        var (system, user) = BuildPrompts(question, citations);

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

    /// <summary>Stream answer tokens after retrieve (caller should call <see cref="RetrieveAsync"/> first for citations).</summary>
    public IAsyncEnumerable<string> StreamAnswerAsync(
        string question,
        IReadOnlyList<RagCitation> citations,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(citations);
        var (system, user) = BuildPrompts(question, citations);
        return _chat.StreamAsync(system, user, ct);
    }

    /// <summary>Retrieve then stream (re-queries; prefer Retrieve + StreamAnswer for UI).</summary>
    public async IAsyncEnumerable<string> AskStreamAsync(
        string question,
        int topK = SampleDefaults.DefaultTopK,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var citations = await _query.QueryAsync(question, topK, ct).ConfigureAwait(false);
        if (citations.Count == 0)
        {
            yield return "No matching chunks were found. Ingest documents first.";
            yield break;
        }

        await foreach (var token in StreamAnswerAsync(question, citations, ct).ConfigureAwait(false))
            yield return token;
    }

    internal static (string System, string User) BuildPrompts(string question, IReadOnlyList<RagCitation> citations)
    {
        var context = string.Join(
            "\n\n",
            citations.Select((c, i) => $"[{i + 1}] ({c.Title} / {c.Source})\n{c.ContextText}"));

        var system = """
            You are a helpful assistant for an offline edge RAG demo using ZVec.NET.
            Answer using the provided context. Prefer a concise answer and cite sources like [1], [2] when you use them.
            Cite each distinct source at most once in the answer; do not repeat the same citation index on every bullet.
            Only say the context is insufficient if it truly does not contain relevant information for the question.
            """;

        var user = $"Context:\n{context}\n\nQuestion: {question}";
        return (system, user);
    }
}
