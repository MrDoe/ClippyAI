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
        Timeout = TimeSpan.FromMinutes(5)
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
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured. Please set the OpenAIApiKey configuration value.");
        }

        UpdateConfig();

        // Load configuration options
        double temperature = taskConfig?.Temperature ?? 0.8;
        int maxTokens = taskConfig?.MaxLength ?? 2048;
        double topP = taskConfig?.TopP ?? 0.9;
        double frequencyPenalty = taskConfig?.RepeatPenalty ?? 0.0;
        double presencePenalty = 0.0; // OpenAI doesn't have direct equivalent to repeat_penalty
        string? systemPrompt = taskConfig?.SystemPrompt ?? system;
        string modelToUse = taskConfig?.Model ?? model;

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
            Content = $"# TEXT\n\n'''{input.Trim()}'''\n# TASK\n\n'{task.Trim()}'"
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

        using HttpResponseMessage response = await client.PostAsync($"{baseUrl}/chat/completions", content, token);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("OpenAI request successful.");

            string responseJson = await response.Content.ReadAsStringAsync(token);
            OpenAIResponse? openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);

            return openAIResponse?.Choices?[0]?.Message?.Content?.Trim('"').Trim('\'');
        }
        else
        {
            string errorContent = await response.Content.ReadAsStringAsync(token);
            throw new Exception($"OpenAI request failed with status: {response.StatusCode}. Response: {errorContent}");
        }
    }

    public async Task<ObservableCollection<string>> GetModelsAsync(CancellationToken token = default)
    {
        List<string> models = [];

        if (string.IsNullOrEmpty(apiKey))
        {
            return [];
        }

        try
        {
            UpdateConfig();

            using HttpResponseMessage response = await client.GetAsync($"{baseUrl}/models", token);

            if (response.IsSuccessStatusCode)
            {
                string responseJson = await response.Content.ReadAsStringAsync(token);
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

    public async Task<string> AnalyzeImage(byte[] image)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        // OpenAI vision requires GPT-4 Vision model
        string visionModel = ConfigurationService.GetConfigurationValue("OpenAIVisionModel", "gpt-4-vision-preview");
        string visionPrompt = ConfigurationService.GetConfigurationValue("VisionPrompt", "Describe the image.");

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

        using HttpResponseMessage response = await client.PostAsync($"{baseUrl}/chat/completions", content);

        if (response.IsSuccessStatusCode)
        {
            string responseJson = await response.Content.ReadAsStringAsync();
            OpenAIResponse? openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);

            return openAIResponse?.Choices?[0]?.Message?.Content ?? "Unable to analyze image.";
        }
        else
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"OpenAI vision request failed: {response.StatusCode}. Response: {errorContent}");
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