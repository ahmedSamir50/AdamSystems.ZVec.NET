using ZVec.NET.Samples.Shared.Models;

namespace ZVec.NET.Samples.Shared.Rag;

public sealed record RagCitation(string Id, string Title, string Source, string Snippet, float Score);

public sealed record RagAskResult(string Answer, IReadOnlyList<RagCitation> Citations, bool UsedChat);

public sealed record IngestResult(int ChunkCount, string Source);
