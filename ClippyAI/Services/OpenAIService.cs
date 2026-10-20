using ClippyAI.Interfaces;
using ClippyAI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace ClippyAI.Services;

/// <summary>
/// OpenAI API provider implementation
/// </summary>
public class OpenAIService : IAIProvider
{
    private static readonly HttpClientHandler handler = new()
    {
        UseProxy = false
    };
    private static readonly HttpClient client = new(handler)
    {
        // Timeouts are enforced per request with a linked CancellationTokenSource
        // so a timeout can be reported to the user and told apart from Cancel.
        // This value is only a last-resort backstop.
        Timeout = TimeSpan.FromSeconds(RequestLimits.MaxTimeoutSeconds)
    };

    private static string? apiKey = ConfigurationService.GetConfigurationValue("OpenAIApiKey");
    private static string? baseUrl = ConfigurationService.GetConfigurationValue("OpenAIBaseUrl", "https://api.openai.com/v1");
    private static string? system = ConfigurationService.GetConfigurationValue("System");

    static OpenAIService()
    {
        UpdateConfig();
    }

    private static void UpdateConfig()
    {
        apiKey = ConfigurationService.GetConfigurationValue("OpenAIApiKey");
        baseUrl = ConfigurationService.GetConfigurationValue("OpenAIBaseUrl", "https://api.openai.com/v1");
        system = ConfigurationService.GetConfigurationValue("System");

        // Set up authorization header
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }
    }

    public async Task<string?> SendRequestWithConfig(string input, string task, string model,
                                                    TaskConfiguration? taskConfig = null,
                                                    CancellationToken token = default)
    {
        // Refresh from the configuration store first: apiKey/baseUrl are static fields,
        // so they are stale when the key is configured after the app has started.
        UpdateConfig();

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured. Please set the OpenAIApiKey configuration value.");
        }

        // Load configuration options
        double temperature = taskConfig?.Temperature ?? 0.8;
        int maxTokens = RequestLimits.ClampOutputTokens(taskConfig?.MaxLength);
        double topP = taskConfig?.TopP ?? 0.9;
        double frequencyPenalty = taskConfig?.RepeatPenalty ?? 0.0;
        double presencePenalty = 0.0; // OpenAI doesn't have direct equivalent to repeat_penalty
        string? systemPrompt = taskConfig?.SystemPrompt ?? system;
        string modelToUse = taskConfig?.Model ?? model;
        string userInput = RequestLimits.TruncateInput(input);

        // Prepare messages
        List<OpenAIMessage> messages = [];

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new OpenAIMessage
            {
                Role = "system",
                Content = systemPrompt
            });
        }

        messages.Add(new OpenAIMessage
        {
            Role = "user",
            Content = $"# TEXT\n\n'''{userInput}'''\n# TASK\n\n'{task.Trim()}'"
        });

        OpenAIRequest request = new()
        {
            Model = modelToUse,
            Messages = [.. messages],
            Temperature = temperature,
            MaxTokens = maxTokens,
            TopP = topP,
            FrequencyPenalty = frequencyPenalty,
            PresencePenalty = presencePenalty,
            Stream = false // For simplicity, not implementing streaming initially
        };

        Console.WriteLine("Sending OpenAI request...");

        string json = JsonSerializer.Serialize(request);
        using StringContent content = new(json, Encoding.UTF8, "application/json");

        int timeoutSeconds = RequestLimits.TimeoutSeconds;
        using CancellationTokenSource timeoutCts = RequestLimits.CreateTimeoutSource(token, timeoutSeconds);

        Console.WriteLine($"OpenAI request: model={modelToUse}, max_tokens={maxTokens}, timeout={timeoutSeconds}s");

        try
        {
            using HttpResponseMessage response = await client.PostAsync($"{baseUrl}/chat/completions", content, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("OpenAI request successful.");

                string responseJson = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                OpenAIResponse? openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);

                return openAIResponse?.Choices?[0]?.Message?.Content?.Trim('"').Trim('\'');
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                throw new Exception($"OpenAI request failed with status: {response.StatusCode}. Response: {errorContent}");
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            // The linked token fired, but the caller did not cancel: this is our timeout.
            throw RequestLimits.CreateTimeoutException(timeoutSeconds);
        }
    }

    public async Task<ObservableCollection<string>> GetModelsAsync(CancellationToken token = default)
    {
        List<string> models = [];

        // Refresh from the configuration store before the apiKey check: the static
        // field is only populated at type load, so an API key configured while the
        // app is running would otherwise never be picked up.
        UpdateConfig();

        if (string.IsNullOrEmpty(apiKey))
        {
            return [];
        }

        try
        {
            using CancellationTokenSource timeoutCts =
                RequestLimits.CreateTimeoutSource(token, RequestLimits.MetadataTimeoutSeconds);
            using HttpResponseMessage response = await client.GetAsync($"{baseUrl}/models", timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                string responseJson = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                OpenAIModelsResponse? modelsResponse = JsonSerializer.Deserialize<OpenAIModelsResponse>(responseJson);

                if (modelsResponse?.Data != null)
                {
                    foreach (OpenAIModel model in modelsResponse.Data)
                    {
                        if (!string.IsNullOrEmpty(model.Id))
                        {
                            models.Add(model.Id);
                        }
                    }
                }
            }
            else
            {
                // Fallback to common models if API call fails
                return
                [];
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching OpenAI models: {ex.Message}");
            // Fallback to common models
            models.AddRange([]);
        }

        return new ObservableCollection<string>(models);
    }

    public ObservableCollection<string> GetModels(CancellationToken token = default)
    {
        return Task.Run(() => GetModelsAsync(token)).GetAwaiter().GetResult();
    }

    public async Task<string> AnalyzeImage(byte[] image, TaskConfiguration? taskConfig = null)
    {
        UpdateConfig();

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        // Use task-specific model/prompt if provided, otherwise fall back to configuration
        string visionModel = !string.IsNullOrEmpty(taskConfig?.Model)
            ? taskConfig!.Model
            : "";
        string visionPrompt = !string.IsNullOrEmpty(taskConfig?.SystemPrompt)
            ? taskConfig!.SystemPrompt
            : "Describe the image.";

        // Convert image to base64
        string base64Image = Convert.ToBase64String(image);
        _ = $"data:image/jpeg;base64,{base64Image}";

        OpenAIMessage[] messages = new[]
        {
            new OpenAIMessage
            {
                Role = "user",
                Content = visionPrompt
            }
        };

        OpenAIRequest request = new()
        {
            Model = visionModel,
            Messages = messages,
            MaxTokens = 1000
        };

        string json = JsonSerializer.Serialize(request);
        using StringContent content = new(json, Encoding.UTF8, "application/json");

        int timeoutSeconds = RequestLimits.TimeoutSeconds;
        using CancellationTokenSource timeoutCts =
            RequestLimits.CreateTimeoutSource(CancellationToken.None, timeoutSeconds);

        try
        {
            using HttpResponseMessage response = await client.PostAsync($"{baseUrl}/chat/completions", content, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                string responseJson = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                OpenAIResponse? openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);

                return openAIResponse?.Choices?[0]?.Message?.Content ?? "Unable to analyze image.";
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                throw new Exception($"OpenAI vision request failed: {response.StatusCode}. Response: {errorContent}");
            }
        }
        catch (OperationCanceledException)
        {
            throw RequestLimits.CreateTimeoutException(timeoutSeconds);
        }
    }

    public bool SupportsCapability(AIProviderCapability capability)
    {
        return capability switch
        {
            AIProviderCapability.Vision => true,
            AIProviderCapability.Embeddings => false, // Would need separate embeddings API
            AIProviderCapability.ModelPulling => false, // Not applicable for cloud API
            AIProviderCapability.ModelDeletion => false, // Not applicable for cloud API  
            AIProviderCapability.Streaming => true, // Can be implemented
            _ => false
        };
    }
}