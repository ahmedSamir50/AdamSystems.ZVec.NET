using ZVec.NET.Mapping;

namespace ZVec.NET.Samples.Shared.Models;

/// <summary>Typed chunk for the dedicated semantic-search collection (separate from RAG).</summary>
[ZVecCollection("search_docs")]
public sealed class SearchDocument
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
