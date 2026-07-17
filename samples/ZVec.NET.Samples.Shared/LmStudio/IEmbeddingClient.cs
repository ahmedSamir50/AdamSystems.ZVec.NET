namespace ZVec.NET.Samples.Shared.LmStudio;

public interface IEmbeddingClient
{
    Task<bool> CanConnectAsync(CancellationToken ct = default);
    Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
