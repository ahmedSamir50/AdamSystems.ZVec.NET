using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ZVec.NET.Samples.Shared.LmStudio;

/// <summary>OpenAI-compatible embeddings against local LM Studio.</summary>
public sealed class LmStudioEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _http;
    private readonly LmStudioOptions _options;

    public LmStudioEmbeddingClient(HttpClient http, LmStudioOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (_http.BaseAddress is null && Uri.TryCreate(_options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
            _http.BaseAddress = baseUri;
    }

    public async Task<bool> CanConnectAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("models", ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken ct = default)
    {
        var batch = await EmbedBatchAsync([text], ct).ConfigureAwait(false);
        return batch[0];
    }

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count == 0)
            return [];

        var payload = new EmbeddingRequest
        {
            Model = _options.EmbeddingModel,
            Input = texts.ToArray()
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("embeddings", payload, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(SampleDefaults.LmStudioDownMessage, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"LM Studio embeddings failed ({(int)response.StatusCode}). " +
                $"Load embedding model '{_options.EmbeddingModel}'. Body: {Trim(body)}");
        }

        var parsed = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct)
            .ConfigureAwait(false);
        if (parsed?.Data is null || parsed.Data.Count == 0)
            throw new InvalidOperationException("LM Studio returned no embedding data.");

        var ordered = parsed.Data.OrderBy(d => d.Index).ToArray();
        var result = new ReadOnlyMemory<float>[ordered.Length];
        for (var i = 0; i < ordered.Length; i++)
        {
            var vector = ordered[i].Embedding ?? throw new InvalidOperationException("Null embedding vector.");
            var memory = new ReadOnlyMemory<float>(vector);
            EmbeddingDimension.Ensure(memory, _options.ExpectedEmbeddingDimensions);
            result[i] = memory;
        }

        return result;
    }

    private static string Trim(string s) => s.Length <= 300 ? s : s[..300] + "…";

    private sealed class EmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("input")]
        public string[] Input { get; set; } = [];
    }

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData>? Data { get; set; }
    }

    private sealed class EmbeddingData
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
