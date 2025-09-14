using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClippyAI.Interfaces;
using ClippyAI.Models;

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
        var temperature = taskConfig?.Temperature ?? 0.8;
        var maxTokens = taskConfig?.MaxLength ?? 2048;
        var topP = taskConfig?.TopP ?? 0.9;
        var frequencyPenalty = taskConfig?.RepeatPenalty ?? 0.0;
        var presencePenalty = 0.0; // OpenAI doesn't have direct equivalent to repeat_penalty
        var systemPrompt = taskConfig?.SystemPrompt ?? system;
        var modelToUse = taskConfig?.Model ?? model;

        // Prepare messages
        var messages = new List<OpenAIMessage>();
        
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

        var request = new OpenAIRequest
        {
            Model = modelToUse,
            Messages = messages.ToArray(),
            Temperature = temperature,
            MaxTokens = maxTokens,
            TopP = topP,
            FrequencyPenalty = frequencyPenalty,
            PresencePenalty = presencePenalty,
            Stream = false // For simplicity, not implementing streaming initially
        };

        Console.WriteLine("Sending OpenAI request...");

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync($"{baseUrl}/chat/completions", content, token);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("OpenAI request successful.");
            
            var responseJson = await response.Content.ReadAsStringAsync(token);
            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);
            
            return openAIResponse?.Choices?[0]?.Message?.Content?.Trim('"').Trim('\'');
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync(token);
            throw new Exception($"OpenAI request failed with status: {response.StatusCode}. Response: {errorContent}");
        }
    }

    public async Task<ObservableCollection<string>> GetModelsAsync(CancellationToken token = default)
    {
        var models = new List<string>();
        
        if (string.IsNullOrEmpty(apiKey))
        {
            // Return common OpenAI models if API key is not configured
            return new ObservableCollection<string>
            {
                "gpt-4",
                "gpt-4-turbo",
                "gpt-3.5-turbo",
                "gpt-3.5-turbo-16k"
            };
        }

        try
        {
            UpdateConfig();
            
            using var response = await client.GetAsync($"{baseUrl}/models", token);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(token);
                var modelsResponse = JsonSerializer.Deserialize<OpenAIModelsResponse>(responseJson);
                
                if (modelsResponse?.Data != null)
                {
                    foreach (var model in modelsResponse.Data)
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
                models.AddRange(new[]
                {
                    "gpt-4",
                    "gpt-4-turbo", 
                    "gpt-3.5-turbo",
                    "gpt-3.5-turbo-16k"
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching OpenAI models: {ex.Message}");
            // Fallback to common models
            models.AddRange(new[]
            {
                "gpt-4",
                "gpt-4-turbo",
                "gpt-3.5-turbo", 
                "gpt-3.5-turbo-16k"
            });
        }

        return new ObservableCollection<string>(models);
    }

    public ObservableCollection<string> GetModels(CancellationToken token = default)
    {
        return GetModelsAsync(token).GetAwaiter().GetResult();
    }

    public async Task<string> AnalyzeImage(byte[] image)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        // OpenAI vision requires GPT-4 Vision model
        var visionModel = ConfigurationService.GetConfigurationValue("OpenAIVisionModel", "gpt-4-vision-preview");
        var visionPrompt = ConfigurationService.GetConfigurationValue("VisionPrompt", "Describe the image.");

        // Convert image to base64
        var base64Image = Convert.ToBase64String(image);
        var imageUrl = $"data:image/jpeg;base64,{base64Image}";

        var messages = new[]
        {
            new OpenAIMessage
            {
                Role = "user",
                Content = visionPrompt
            }
        };

        var request = new OpenAIRequest
        {
            Model = visionModel,
            Messages = messages,
            MaxTokens = 1000
        };

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync($"{baseUrl}/chat/completions", content);

        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync();
            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);
            
            return openAIResponse?.Choices?[0]?.Message?.Content ?? "Unable to analyze image.";
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
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