using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ZVec.NET.Samples.Shared.LmStudio;

/// <summary>OpenAI-compatible chat completions against local LM Studio (Gemma 4).</summary>
public sealed class LmStudioChatClient : IChatClient
{
    private readonly HttpClient _http;
    private readonly LmStudioOptions _options;

    public LmStudioChatClient(HttpClient http, LmStudioOptions options)
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

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var payload = new ChatRequest
        {
            Model = _options.ChatModel,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt }
            ],
            Temperature = 0.2
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("chat/completions", payload, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(SampleDefaults.LmStudioDownMessage, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"LM Studio chat failed ({(int)response.StatusCode}). " +
                $"Load chat model '{_options.ChatModel}' (Gemma 4). Body: {Trim(body)}");
        }

        var parsed = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct)
            .ConfigureAwait(false);
        var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("LM Studio returned an empty chat completion.");

        return content.Trim();
    }

    private static string Trim(string s) => s.Length <= 300 ? s : s[..300] + "…";

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("messages")]
        public ChatMessage[] Messages { get; set; } = [];

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }
}
