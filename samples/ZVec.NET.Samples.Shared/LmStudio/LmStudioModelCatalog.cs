using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ZVec.NET.Samples.Shared.LmStudio;

/// <summary>Lists models from LM Studio OpenAI-compatible GET /models.</summary>
public sealed class LmStudioModelCatalog
{
    private readonly HttpClient _http;
    private readonly LmStudioOptions _options;
    private readonly IEmbeddingClient _embeddings;

    public LmStudioModelCatalog(HttpClient http, LmStudioOptions options, IEmbeddingClient embeddings)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        if (_http.BaseAddress is null && Uri.TryCreate(_options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
            _http.BaseAddress = baseUri;
    }

    public async Task<IReadOnlyList<string>> ListModelIdsAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("models", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            var parsed = await response.Content.ReadFromJsonAsync<ModelsResponse>(cancellationToken: ct)
                .ConfigureAwait(false);
            return parsed?.Data?
                       .Select(d => d.Id)
                       .Where(id => !string.IsNullOrWhiteSpace(id))
                       .Distinct(StringComparer.Ordinal)
                       .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                       .ToArray()
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<ModelSelectionResult> ApplySelectionAsync(
        string? embeddingModel,
        string? chatModel,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(embeddingModel))
            _options.EmbeddingModel = embeddingModel.Trim();
        if (!string.IsNullOrWhiteSpace(chatModel))
            _options.ChatModel = chatModel.Trim();

        var embedOk = false;
        var embedDim = 0;
        string? embedError = null;
        try
        {
            var vec = await _embeddings.EmbedAsync("zvec model selection probe", ct).ConfigureAwait(false);
            embedDim = vec.Length;
            embedOk = embedDim == _options.ExpectedEmbeddingDimensions;
            if (!embedOk)
            {
                embedError = string.Format(
                    SampleDefaults.DimMismatchFormat,
                    embedDim,
                    _options.ExpectedEmbeddingDimensions);
            }
        }
        catch (Exception ex)
        {
            embedError = ex.Message;
        }

        return new ModelSelectionResult(
            _options.EmbeddingModel,
            _options.ChatModel,
            embedOk,
            embedDim,
            embedError,
            embedOk
                ? null
                : "If the collection already has documents, re-seed after switching embedding models.");
    }

    public static bool LooksLikeEmbeddingModel(string id)
        => id.Contains("embed", StringComparison.OrdinalIgnoreCase);

    private sealed class ModelsResponse
    {
        [JsonPropertyName("data")]
        public List<ModelEntry>? Data { get; set; }
    }

    private sealed class ModelEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
    }
}

public sealed record ModelSelectionResult(
    string EmbeddingModel,
    string ChatModel,
    bool EmbeddingOk,
    int EmbeddingDimensions,
    string? EmbeddingError,
    string? Warning);
