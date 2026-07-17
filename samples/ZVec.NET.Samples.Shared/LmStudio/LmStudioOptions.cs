namespace ZVec.NET.Samples.Shared.LmStudio;

/// <summary>Loopback LM Studio OpenAI-compatible settings (no secrets).</summary>
public sealed class LmStudioOptions
{
    public string BaseUrl { get; set; } = SampleDefaults.LmStudioBaseUrl;
    public string EmbeddingModel { get; set; } = SampleDefaults.EmbeddingModelId;
    public string ChatModel { get; set; } = SampleDefaults.ChatModelId;
    public int ExpectedEmbeddingDimensions { get; set; } = SampleDefaults.VectorDimensions;
}
