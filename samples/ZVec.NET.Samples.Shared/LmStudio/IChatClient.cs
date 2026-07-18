namespace ZVec.NET.Samples.Shared.LmStudio;

public interface IChatClient
{
    Task<bool> CanConnectAsync(CancellationToken ct = default);
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);

    /// <summary>OpenAI-compatible SSE streaming of chat completion deltas.</summary>
    IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default);
}
