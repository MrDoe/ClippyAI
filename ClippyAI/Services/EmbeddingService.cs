using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ClippyAI.Services;

/// <summary>
/// What the text is used for. Some embedding models expect different instruction
/// prefixes for documents and queries.
/// </summary>
public enum EmbeddingPurpose
{
    Document,
    Query
}

/// <summary>
/// Generates embedding vectors with the configured AI provider.
///
/// Both supported providers expose an HTTP embedding endpoint, but with different
/// request/response shapes:
/// <list type="bullet">
/// <item>Ollama: <c>POST /api/embed</c> (falls back to <c>/api/embeddings</c> on older servers)</item>
/// <item>OpenAI: <c>POST /v1/embeddings</c></item>
/// </list>
/// The provider is the same <c>AIProvider</c> setting used for chat, so an
/// OpenAI-only setup never needs a local Ollama instance.
/// </summary>
public static class EmbeddingService
{
    /// <summary>
    /// Budget for one embedding request. The first request may have to load the
    /// model into memory, which can take a while on CPU.
    /// </summary>
    public const int EmbeddingTimeoutSeconds = 180;

    private static readonly HttpClientHandler Handler = new()
    {
        UseProxy = false
    };

    private static readonly HttpClient Client = new(Handler)
    {
        // Per-request timeouts are enforced with a linked CancellationTokenSource.
        Timeout = TimeSpan.FromSeconds(RequestLimits.MaxTimeoutSeconds)
    };

    /// <summary>Default Ollama embedding model.</summary>
    public const string DefaultOllamaModel = "nomic-embed-text";

    /// <summary>Default OpenAI embedding model.</summary>
    public const string DefaultOpenAIModel = "text-embedding-3-small";

    /// <summary>The AI provider used for embeddings (Ollama or OpenAI).</summary>
    public static string Provider =>
        ConfigurationService.GetConfigurationValue("AIProvider", "Ollama");

    /// <summary>
    /// Returns the embedding model configured for <paramref name="provider"/>.
    /// Falls back to the legacy single <c>EmbeddingModel</c> setting for Ollama,
    /// so configurations created before the per-provider split keep working.
    /// </summary>
    public static string GetModel(string? provider = null)
    {
        provider ??= Provider;

        if (IsOpenAI(provider))
        {
            string model = ConfigurationService.GetConfigurationValue("OpenAIEmbeddingModel", DefaultOpenAIModel);
            return string.IsNullOrWhiteSpace(model) ? DefaultOpenAIModel : model.Trim();
        }

        string legacy = ConfigurationService.GetConfigurationValue("EmbeddingModel", "");
        string defaultModel = string.IsNullOrWhiteSpace(legacy) ? DefaultOllamaModel : legacy.Trim();
        string ollamaModel = ConfigurationService.GetConfigurationValue("OllamaEmbeddingModel", defaultModel);

        return string.IsNullOrWhiteSpace(ollamaModel) ? defaultModel : ollamaModel.Trim();
    }

    /// <summary>
    /// Creates the embedding vector for <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="purpose">Whether the text is a stored document or a search query.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The embedding vector.</returns>
    public static async Task<float[]> EmbedAsync(string text, EmbeddingPurpose purpose,
                                                 CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Cannot embed empty text.", nameof(text));
        }

        string provider = Provider;
        string model = GetModel(provider);
        string input = ApplyPrefix(text, model, purpose);

        using CancellationTokenSource timeoutCts =
            RequestLimits.CreateTimeoutSource(token, EmbeddingTimeoutSeconds);

        float[] vector = IsOpenAI(provider)
            ? await EmbedWithOpenAIAsync(input, model, timeoutCts.Token).ConfigureAwait(false)
            : await EmbedWithOllamaAsync(input, model, timeoutCts.Token).ConfigureAwait(false);

        if (vector.Length == 0)
        {
            throw new InvalidOperationException(
                $"The embedding provider '{provider}' returned an empty vector for model '{model}'.");
        }

        return vector;
    }

    private static bool IsOpenAI(string provider) =>
        provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// nomic-embed-text expects task prefixes; other models (for example OpenAI's
    /// text-embedding-3 family) must receive the raw text.
    /// </summary>
    private static string ApplyPrefix(string text, string model, EmbeddingPurpose purpose)
    {
        if (!model.Contains("nomic", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        string prefix = purpose == EmbeddingPurpose.Query ? "search_query: " : "search_document: ";
        return prefix + text;
    }

    private static async Task<float[]> EmbedWithOllamaAsync(string input, string model,
                                                            CancellationToken token)
    {
        string baseUrl = OllamaProvider.ResolveBaseUrl().TrimEnd('/');

        // Preferred endpoint (Ollama >= 0.2). Falls back below for older servers.
        HttpResponseMessage? response = null;
        try
        {
            string body = JsonSerializer.Serialize(new OllamaEmbedRequest
            {
                Model = model,
                Input = [input]
            });

            using StringContent content = new(body, Encoding.UTF8, "application/json");
            response = await Client.PostAsync($"{baseUrl}/embed", content, token).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                response.Dispose();
                response = null;
                return await EmbedWithLegacyOllamaAsync(baseUrl, input, model, token).ConfigureAwait(false);
            }

            return await ReadOllamaEmbeddingsAsync(response, model, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw RequestLimits.CreateTimeoutException(EmbeddingTimeoutSeconds);
        }
        finally
        {
            response?.Dispose();
        }
    }

    private static async Task<float[]> EmbedWithLegacyOllamaAsync(string baseUrl, string input,
                                                                  string model, CancellationToken token)
    {
        string body = JsonSerializer.Serialize(new OllamaLegacyEmbedRequest
        {
            Model = model,
            Prompt = input
        });

        using StringContent content = new(body, Encoding.UTF8, "application/json");
        using HttpResponseMessage response =
            await Client.PostAsync($"{baseUrl}/embeddings", content, token).ConfigureAwait(false);

        string payload = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Ollama rejected the embedding request ({(int)response.StatusCode}): {Truncate(payload)}");
        }

        OllamaLegacyEmbedResponse? parsed =
            JsonSerializer.Deserialize<OllamaLegacyEmbedResponse>(payload);

        if (parsed?.Embedding is null || parsed.Embedding.Length == 0)
        {
            throw new InvalidOperationException(
                $"Ollama returned no embedding for model '{model}'. Pull it with: ollama pull {model}");
        }

        return parsed.Embedding;
    }

    private static async Task<float[]> ReadOllamaEmbeddingsAsync(HttpResponseMessage response,
                                                                 string model, CancellationToken token)
    {
        string payload = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Ollama rejected the embedding request ({(int)response.StatusCode}): {Truncate(payload)}");
        }

        OllamaEmbedResponse? parsed = JsonSerializer.Deserialize<OllamaEmbedResponse>(payload);

        if (parsed?.Embeddings is not { Length: > 0 } embeddings ||
            embeddings[0] is not { Length: > 0 } embedding)
        {
            throw new InvalidOperationException(
                $"Ollama returned no embedding for model '{model}'. Pull it with: ollama pull {model}");
        }

        return embedding;
    }

    private static async Task<float[]> EmbedWithOpenAIAsync(string input, string model,
                                                            CancellationToken token)
    {
        string apiKey = ConfigurationService.GetConfigurationValue("OpenAIApiKey", "");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is not configured. Add it on the General tab or switch the AI provider to Ollama.");
        }

        string baseUrl = ConfigurationService
            .GetConfigurationValue("OpenAIBaseUrl", "https://api.openai.com/v1")
            .TrimEnd('/');

        string body = JsonSerializer.Serialize(new OpenAIEmbedRequest
        {
            Model = model,
            Input = input,
            EncodingFormat = "float"
        });

        using HttpRequestMessage request = new(HttpMethod.Post, $"{baseUrl}/embeddings");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        try
        {
            using HttpResponseMessage response = await Client.SendAsync(request, token).ConfigureAwait(false);
            string payload = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"OpenAI rejected the embedding request ({(int)response.StatusCode}): {Truncate(payload)}");
            }

            OpenAIEmbedResponse? parsed = JsonSerializer.Deserialize<OpenAIEmbedResponse>(payload);
            if (parsed?.Data is not { Length: > 0 } data ||
                data[0].Embedding is not { Length: > 0 } embedding)
            {
                throw new InvalidOperationException(
                    $"OpenAI returned no embedding for model '{model}'.");
            }

            return embedding;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw RequestLimits.CreateTimeoutException(EmbeddingTimeoutSeconds);
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300] + "...";

    private sealed class OllamaEmbedRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("input")]
        public string[] Input { get; set; } = [];
    }

    private sealed class OllamaEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public float[][]? Embeddings { get; set; }
    }

    private sealed class OllamaLegacyEmbedRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";
    }

    private sealed class OllamaLegacyEmbedResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }

    private sealed class OpenAIEmbedRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("input")]
        public string Input { get; set; } = "";

        [JsonPropertyName("encoding_format")]
        public string EncodingFormat { get; set; } = "float";
    }

    private sealed class OpenAIEmbedResponse
    {
        [JsonPropertyName("data")]
        public OpenAIEmbedDatum[]? Data { get; set; }
    }

    private sealed class OpenAIEmbedDatum
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
