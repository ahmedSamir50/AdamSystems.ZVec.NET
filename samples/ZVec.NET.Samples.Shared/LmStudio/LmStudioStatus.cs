namespace ZVec.NET.Samples.Shared.LmStudio;

public sealed record LmStudioStatus(bool Reachable, bool EmbeddingsOk, bool ChatOk, string Message);

/// <summary>Probes LM Studio for UI status bars (both models concurrently — no switching).</summary>
public sealed class LmStudioStatusProbe
{
    private readonly IEmbeddingClient _embeddings;
    private readonly IChatClient _chat;

    public LmStudioStatusProbe(IEmbeddingClient embeddings, IChatClient chat)
    {
        _embeddings = embeddings;
        _chat = chat;
    }

    public async Task<LmStudioStatus> ProbeAsync(CancellationToken ct = default)
    {
        var reachable = await _embeddings.CanConnectAsync(ct).ConfigureAwait(false);
        if (!reachable)
            return new LmStudioStatus(false, false, false, SampleDefaults.LmStudioDownMessage);

        var embedTask = ProbeEmbedAsync(ct);
        var chatTask = _chat.CanConnectAsync(ct);
        await Task.WhenAll(embedTask, chatTask).ConfigureAwait(false);

        var embedOk = await embedTask.ConfigureAwait(false);
        var chatOk = await chatTask.ConfigureAwait(false);

        var msg = embedOk && chatOk
            ? $"LM Studio ready (embed: {SampleDefaults.EmbeddingModelId} + chat: {SampleDefaults.ChatModelId})."
            : embedOk
                ? $"Embeddings OK. Load chat model '{SampleDefaults.ChatModelId}' (both can run together)."
                : chatOk
                    ? $"Chat OK. Load embedding model '{SampleDefaults.EmbeddingModelId}' (768-d)."
                    : "Connected, but probes failed. Load both models concurrently in LM Studio.";

        return new LmStudioStatus(true, embedOk, chatOk, msg);
    }

    private async Task<bool> ProbeEmbedAsync(CancellationToken ct)
    {
        try
        {
            var vec = await _embeddings.EmbedAsync("zvec connectivity probe", ct).ConfigureAwait(false);
            return vec.Length == SampleDefaults.VectorDimensions;
        }
        catch
        {
            return false;
        }
    }
}
