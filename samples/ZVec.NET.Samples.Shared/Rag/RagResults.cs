namespace ZVec.NET.Samples.Shared.Rag;

/// <param name="Snippet">Short preview for UI (~240 chars).</param>
/// <param name="ContextText">Full chunk text for LLM context (may be capped).</param>
public sealed record RagCitation(
    string Id,
    string Title,
    string Source,
    string Snippet,
    string ContextText,
    float Score);

public sealed record RagAskResult(string Answer, IReadOnlyList<RagCitation> Citations, bool UsedChat);

public sealed record IngestResult(int ChunkCount, string Source);
