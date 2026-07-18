using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZVec.NET.Samples.Shared.LmStudio;

/// <summary>OpenAI-compatible chat completions against local LM Studio (Gemma 4).</summary>
public sealed class LmStudioChatClient : IChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
        try
        {
            var sb = new StringBuilder();
            await foreach (var token in StreamAsync(systemPrompt, userPrompt, ct).ConfigureAwait(false))
                sb.Append(token);

            var content = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(content))
                return content;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            // Fall back to non-streaming.
        }

        return await CompleteNonStreamAsync(systemPrompt, userPrompt, ct).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new ChatRequest
        {
            Model = _options.ChatModel,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt }
            ],
            Temperature = 0.2,
            Stream = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload)
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(SampleDefaults.LmStudioDownMessage, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"LM Studio chat failed ({(int)response.StatusCode}). " +
                    $"Load chat model '{_options.ChatModel}'. Body: {Trim(body)}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null)
                    yield break;
                if (line.Length == 0)
                    continue;
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                    continue;

                var data = line["data:".Length..].Trim();
                if (data is "[DONE]")
                    yield break;

                StreamChunk? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<StreamChunk>(data, JsonOptions);
                }
                catch (JsonException)
                {
                    continue;
                }

                var delta = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                if (!string.IsNullOrEmpty(delta))
                    yield return delta;
            }
        }
    }

    private async Task<string> CompleteNonStreamAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var payload = new ChatRequest
        {
            Model = _options.ChatModel,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt }
            ],
            Temperature = 0.2,
            Stream = false
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

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"LM Studio chat failed ({(int)response.StatusCode}). " +
                    $"Load chat model '{_options.ChatModel}'. Body: {Trim(body)}");
            }

            var parsed = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct)
                .ConfigureAwait(false);
            var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("LM Studio returned an empty chat completion.");

            return content.Trim();
        }
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

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
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

    private sealed class StreamChunk
    {
        [JsonPropertyName("choices")]
        public List<StreamChoice>? Choices { get; set; }
    }

    private sealed class StreamChoice
    {
        [JsonPropertyName("delta")]
        public StreamDelta? Delta { get; set; }
    }

    private sealed class StreamDelta
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
