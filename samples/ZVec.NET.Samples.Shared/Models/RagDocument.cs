using ZVec.NET.Mapping;

namespace ZVec.NET.Samples.Shared.Models;

/// <summary>Typed chunk stored for RAG / semantic search demos.</summary>
[ZVecCollection("rag_docs")]
public sealed class RagDocument
{
    [ZVecId]
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    public string Source { get; set; } = "";

    public string ChunkText { get; set; } = "";

    public string Tags { get; set; } = "";

    [ZVecVector(SampleDefaults.VectorDimensions, Metric = ZVecMetricType.Cosine, M = 16, EfConstruction = 128)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
