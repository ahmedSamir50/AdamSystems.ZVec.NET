using ZVec.NET.Mapping;

namespace ZVec.NET.Samples.Shared.Models;

/// <summary>Typed item for content-based recommendation demos (movies / products).</summary>
[ZVecCollection("recommend_items")]
public sealed class RecommendItem
{
    [ZVecId]
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    public string Category { get; set; } = "";

    public string Description { get; set; } = "";

    [ZVecVector(SampleDefaults.VectorDimensions, Metric = ZVecMetricType.Cosine, M = 16, EfConstruction = 128)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
