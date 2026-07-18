namespace ZVec.NET.Samples.Shared.LmStudio;

public sealed record LmStudioStatus(
    bool Reachable,
    bool EmbeddingsOk,
    bool ChatOk,
    string Message,
    string EmbeddingModel,
    string ChatModel);

/// <summary>Probes LM Studio for UI status bars (both models concurrently — no switching).</summary>
public sealed class LmStudioStatusProbe
{
    private readonly IEmbeddingClient _embeddings;
    private readonly IChatClient _chat;
    private readonly LmStudioOptions _options;

    public LmStudioStatusProbe(IEmbeddingClient embeddings, IChatClient chat, LmStudioOptions options)
    {
        _embeddings = embeddings;
        _chat = chat;
        _options = options;
    }

    public async Task<LmStudioStatus> ProbeAsync(CancellationToken ct = default)
    {
        var reachable = await _embeddings.CanConnectAsync(ct).ConfigureAwait(false);
        if (!reachable)
        {
            return new LmStudioStatus(
                false, false, false, SampleDefaults.LmStudioDownMessage,
                _options.EmbeddingModel, _options.ChatModel);
        }

        var embedTask = ProbeEmbedAsync(ct);
        var chatTask = _chat.CanConnectAsync(ct);
        await Task.WhenAll(embedTask, chatTask).ConfigureAwait(false);

        var embedOk = await embedTask.ConfigureAwait(false);
        var chatOk = await chatTask.ConfigureAwait(false);

        var msg = embedOk && chatOk
            ? $"LM Studio ready (embed: {_options.EmbeddingModel} + chat: {_options.ChatModel})."
            : embedOk
                ? $"Embeddings OK. Load/select chat model '{_options.ChatModel}' (both can run together)."
                : chatOk
                    ? $"Chat OK. Load/select embedding model '{_options.EmbeddingModel}' (768-d)."
                    : "Connected, but probes failed. Load both models in LM Studio and select them.";

        return new LmStudioStatus(true, embedOk, chatOk, msg, _options.EmbeddingModel, _options.ChatModel);
    }

    private async Task<bool> ProbeEmbedAsync(CancellationToken ct)
    {
        try
        {
            var vec = await _embeddings.EmbedAsync("zvec connectivity probe", ct).ConfigureAwait(false);
            return vec.Length == _options.ExpectedEmbeddingDimensions;
        }
        catch
        {
            return false;
        }
    }
}
