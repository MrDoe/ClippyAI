using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ClippyAI.Interfaces;
using ClippyAI.Models;
using ClippyAI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace ClippyAI.Services;

/// <summary>
/// Ollama API provider implementation
/// </summary>
public class OllamaProvider : IAIProvider
{
    private static readonly HttpClientHandler handler = new()
    {
        UseProxy = false
    };
    private static readonly HttpClient client = new(handler)
    {
        // Timeouts are enforced per request with a linked CancellationTokenSource
        // so a timeout can be reported instead of looking like a user cancel.
        // This value is only a last-resort backstop.
        Timeout = TimeSpan.FromSeconds(RequestLimits.MaxTimeoutSeconds)
    };

    private static string? url = ConfigurationService.GetConfigurationValue("OllamaUrl", "http://localhost:11434/api");
    private static string? system = ConfigurationService.GetConfigurationValue("System");

    public void UpdateConfig()
    {
        url = ResolveBaseUrl();
        system = ConfigurationService.GetConfigurationValue("System");
    }

    /// <summary>
    /// Resolves the Ollama API base URL, applying the SSH tunnel port when
    /// tunneling is enabled. Shared with <see cref="EmbeddingService"/> so chat
    /// and embedding traffic always reach the same endpoint.
    /// </summary>
    public static string ResolveBaseUrl()
    {
        string baseUrl = ConfigurationService.GetConfigurationValue("OllamaUrl", "http://localhost:11434/api");

        // Check if SSH is enabled and adjust URL to use tunnel port
        string useSSH = ConfigurationService.GetConfigurationValue("UseSSH", "False");
        if (useSSH.Equals("True", StringComparison.OrdinalIgnoreCase))
        {
            string sshLocalTunnel = ConfigurationService.GetConfigurationValue("SSHLocalTunnel", "");
            if (!string.IsNullOrWhiteSpace(sshLocalTunnel))
            {
                // Parse tunnel format: "localhost:localPort:remoteHost:remotePort"
                string[] tunnelParts = sshLocalTunnel.Split(':');
                if (tunnelParts.Length >= 2 && int.TryParse(tunnelParts[1], out int tunnelPort))
                {
                    // Replace the port in the base URL with the tunnel port
                    Uri uri = new(baseUrl);
                    UriBuilder tunnelUri = new(uri) { Port = tunnelPort };
                    return tunnelUri.ToString();
                }
            }
        }

        return baseUrl;
    }

    public async Task<string?> SendRequestWithConfig(string input, string task, string model,
                                                   TaskConfiguration? taskConfig = null,
                                                   CancellationToken token = default)
    {
        string? fullResponse = null;
        UpdateConfig();

        // Load advanced configuration options
        double temperature = taskConfig?.Temperature ?? 0.8;
        int maxLength = RequestLimits.ClampOutputTokens(taskConfig?.MaxLength);
        double topP = taskConfig?.TopP ?? 0.9;
        int topK = taskConfig?.TopK ?? 40;
        double repeatPenalty = taskConfig?.RepeatPenalty ?? 1.1;
        int numCtx = taskConfig?.NumCtx ?? 2048;
        string modelToUse = taskConfig?.Model ?? model;
        string? systemPrompt = taskConfig?.SystemPrompt ?? system;
        string userInput = RequestLimits.TruncateInput(input);

        OllamaRequest body = new()
        {
            prompt = $"# TEXT\n\n'''{userInput}'''\n# TASK\n\n'{task.Trim()}'",
            model = modelToUse,
            system = systemPrompt,
            stream = true,
            keep_alive = "60m",
            options = new OllamaModelOptions()
            {
                temperature = temperature,
                num_predict = maxLength,
                top_p = topP,
                top_k = topK,
                repeat_penalty = repeatPenalty,
                num_ctx = numCtx
            }
        };
        Console.WriteLine("Sending request...");

        int timeoutSeconds = RequestLimits.TimeoutSeconds;
        using CancellationTokenSource timeoutCts = RequestLimits.CreateTimeoutSource(token, timeoutSeconds);

        Console.WriteLine($"Ollama request: model={modelToUse}, num_predict={maxLength}, timeout={timeoutSeconds}s");

        try
        {
            // send the request
            using HttpResponseMessage response = await client.PostAsync(
                                 url + "/generate",
                                 new StringContent(JsonSerializer.Serialize(body),
                                 Encoding.UTF8,
                                 "application/json"),
                                 timeoutCts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Request successful.");

                using Stream responseStream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
                using StreamReader reader = new(responseStream);

                string? line;
                while ((line = await reader.ReadLineAsync(timeoutCts.Token)) != null &&
                        !timeoutCts.IsCancellationRequested)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        OllamaResponse? responseObj = JsonSerializer.Deserialize<OllamaResponse>(line);
                        string? responseText = responseObj?.response;
                        fullResponse += responseText;
                    }
                }
                Console.WriteLine("Response fully received.");
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                throw new Exception($"Ollama request failed with status: {response.StatusCode}. Response: {errorContent}");
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            // The linked token fired, but the caller did not cancel: this is our timeout.
            throw RequestLimits.CreateTimeoutException(timeoutSeconds);
        }

        // remove double quotes from the beginning and end of the response
        if (fullResponse != null)
        {
            fullResponse = fullResponse.Trim('"').Trim('\'');
        }

        return fullResponse;
    }

    public async Task<ObservableCollection<string>> GetModelsAsync(CancellationToken token = default)
    {
        List<string> models = [];
        HttpResponseMessage? response = null;
        using CancellationTokenSource timeoutCts =
            RequestLimits.CreateTimeoutSource(token, RequestLimits.MetadataTimeoutSeconds);
        try
        {
            UpdateConfig();
            response = await client.GetAsync(
                                 $"{url}/tags",
                                 timeoutCts.Token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error retrieving models list: " + e.Message);
        }
        if (response != null && response.IsSuccessStatusCode)
        {
            using Stream stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            using StreamReader reader = new(stream);
            string? output = await reader.ReadToEndAsync();

            if (output != null)
            {
                // only collect model names
                List<OllamaModels>? deserializedModels = JsonSerializer.Deserialize<OllamaModelRequest>(output)?.models;

                if (deserializedModels != null)
                {
                    foreach (OllamaModels model in deserializedModels)
                    {
                        models.Add(model!.name!);
                    }
                }
            }
        }
        // else
        // {
        //     Console.WriteLine($"Request failed with status: {response?.StatusCode}.");
        // }

        return new ObservableCollection<string>(models);
    }

    public ObservableCollection<string> GetModels(CancellationToken token = default)
    {
        return Task.Run(() => GetModelsAsync(token)).GetAwaiter().GetResult();
    }

    public async Task<string> AnalyzeImage(byte[] image, TaskConfiguration? taskConfig = null)
    {
        UpdateConfig();

        string base64Image = Convert.ToBase64String(image);
        string modelToUse = taskConfig?.Model ?? "";
        string promptToUse = taskConfig?.SystemPrompt ?? "Describe the image.";

        OllamaRequest body = new()
        {
            model = modelToUse,
            prompt = promptToUse,
            images = [base64Image],
            stream = false
        };

        int timeoutSeconds = RequestLimits.TimeoutSeconds;
        using CancellationTokenSource timeoutCts =
            RequestLimits.CreateTimeoutSource(CancellationToken.None, timeoutSeconds);

        try
        {
            using HttpResponseMessage response = await client.PostAsync(
                                 url + "/generate",
                                 new StringContent(JsonSerializer.Serialize(body),
                                 Encoding.UTF8,
                                 "application/json"),
                                 timeoutCts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                OllamaResponse? responseObj = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
                return responseObj?.response ?? "Unable to analyze image.";
            }
            else
            {
                throw new Exception($"Image analysis failed with status: {response.StatusCode}.");
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
            AIProviderCapability.Embeddings => true,
            AIProviderCapability.ModelPulling => true,
            AIProviderCapability.ModelDeletion => true,
            AIProviderCapability.Streaming => true,
            _ => false
        };
    }

    /// <summary>
    /// Pulls a new model from the Ollama API.
    /// </summary>
    /// <param name="modelName">The name of the model to pull.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The models.</returns>
    public async Task PullModelAsync(string modelName, CancellationToken token = default)
    {
        HttpResponseMessage? response;
        try
        {
            var requestBody = new
            {
                name = modelName,
                insecure = false,
                stream = true
            };

            OllamaService.ShowNotification($"Pulling model {modelName}...", true, false);

            response = await client.PostAsync(
                                             $"{url}/pull",
                                             new StringContent(JsonSerializer.Serialize(requestBody),
                                             Encoding.UTF8,
                                             "application/json"),
                                             token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using Stream streamContent = await response.Content.ReadAsStreamAsync(token);
                using StreamReader reader = new(streamContent);

                string? line;
                DateTime lastNotificationTime = DateTime.MinValue;
                while ((line = await reader.ReadLineAsync(token)) != null && !token.IsCancellationRequested)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        Dictionary<string, object>? statusUpdate = JsonSerializer.Deserialize<Dictionary<string, object>?>(line);
                        if (statusUpdate == null)
                        {
                            continue;
                        }

                        string statusMessage = statusUpdate["status"]?.ToString() ?? "Model pull in progress";

                        if (statusUpdate.TryGetValue("completed", out object? completed) &&
                           statusUpdate.TryGetValue("total", out object? total))
                        {
                            statusMessage += $" ({completed}/{total})";
                        }

                        // Check if 5 seconds have passed since the last notification
                        if ((DateTime.Now - lastNotificationTime).TotalSeconds >= 5)
                        {
                            OllamaService.ShowNotification(statusMessage, true, false);
                            lastNotificationTime = DateTime.Now;
                        }

                        if (statusUpdate?.ContainsKey("completed") == true)
                        {
                            Console.WriteLine("Model pull completed.");
                            break;
                        }
                    }
                }

                OllamaService.ShowNotification($"Model {modelName} pulled successfully", false, false);
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync(token);
                throw new Exception($"Model pull failed with status: {response.StatusCode}. Response: {errorContent}");
            }
        }
        catch (Exception e)
        {
            OllamaService.ShowNotification($"Error pulling model: {e.Message}", false, true);
            throw;
        }
    }

    /// <summary>
    /// Deletes a model from Ollama.
    /// </summary>
    /// <param name="modelName">The name of the model to delete.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task DeleteModelAsync(string modelName, CancellationToken token = default)
    {
        try
        {
            var requestBody = new
            {
                name = modelName
            };

            HttpRequestMessage request = new(HttpMethod.Delete, $"{url}/delete")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            using HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                OllamaService.ShowNotification($"Model {modelName} deleted successfully", false, false);
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync(token);
                throw new Exception($"Model deletion failed with status: {response.StatusCode}. Response: {errorContent}");
            }
        }
        catch (Exception e)
        {
            OllamaService.ShowNotification($"Error deleting model: {e.Message}", false, true);
            throw;
        }
    }
}

public static class OllamaService
{
    /// <summary>
    /// Cosine distance below which a new template counts as a duplicate of an
    /// existing one. The previous pgvector implementation used an L2 distance of
    /// 1, which is the same threshold for normalized embeddings.
    /// </summary>
    private const float DuplicateCosineDistance = 0.5f;

    // Provider instances
    private static readonly OllamaProvider ollamaProvider = new();
    private static readonly OpenAIService openAIProvider = new();

    /// <summary>
    /// Gets the current AI provider based on configuration.
    /// </summary>
    /// <returns>The configured AI provider.</returns>
    public static IAIProvider GetCurrentProvider()
    {
        string provider = ConfigurationService.GetConfigurationValue("AIProvider", "Ollama");
        return provider switch
        {
            "OpenAI" => openAIProvider,
            _ => ollamaProvider
        };
    }

    /// <summary>
    /// Gets task-specific configuration from the database if available, otherwise returns null.
    /// </summary>
    /// <param name="taskName">The name of the task to look up configuration for.</param>
    /// <returns>TaskConfiguration object if found, null otherwise.</returns>
    public static TaskConfiguration? GetTaskConfiguration(string taskName)
    {
        try
        {
            return ConfigurationService.GetTaskConfiguration(taskName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting task configuration: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sends a request to the AI API using task-specific configuration if available.
    /// </summary>
    /// <param name="input">The clipboard data to send.</param>
    /// <param name="task">The task to perform.</param>
    /// <param name="model">The model to use.</param>
    /// <param name="token">The cancellation token.</param>
    public static async Task<string?> SendRequestForTask(string input, string task,
                                                         string model, CancellationToken token = default)
    {
        // Try to find a task configuration that matches
        TaskConfiguration? taskConfig = GetTaskConfiguration(task);
        return await SendRequestWithConfig(input, task, model, taskConfig, token);
    }

    /// <summary>
    /// Sends a request to the AI API.
    /// </summary>
    /// <param name="input">The clipboard data to send.</param>
    /// <param name="task">The task to perform.</param>
    /// <param name="model">The model to use.</param>
    /// <param name="token">The cancellation token.</param>
    public static async Task<string?> SendRequest(string input, string task,
                                                  string model, CancellationToken token = default)
    {
        return await SendRequestWithConfig(input, task, model, null, token);
    }

    /// <summary>
    /// Sends a request to the AI API with enhanced configuration options.
    /// </summary>
    /// <param name="input">The clipboard data to send.</param>
    /// <param name="task">The task to perform.</param>
    /// <param name="model">The model to use.</param>
    /// <param name="taskConfig">Optional task-specific configuration. If null, uses default config.</param>
    /// <param name="token">The cancellation token.</param>
    public static async Task<string?> SendRequestWithConfig(string input, string task,
                                                           string model, TaskConfiguration? taskConfig = null,
                                                           CancellationToken token = default)
    {
        IAIProvider provider = GetCurrentProvider();
        return await provider.SendRequestWithConfig(input, task, model, taskConfig, token);
    }

    /// <summary>
    /// Get models from the AI API.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The models.</returns>
    public static ObservableCollection<string> GetModels(CancellationToken token = default)
    {
        return Task.Run(() => GetModelsAsync(token)).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Get models from the AI API.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The models.</returns>
    public static async Task<ObservableCollection<string>> GetModelsAsync(CancellationToken token = default)
    {
        IAIProvider provider = GetCurrentProvider();
        return await provider.GetModelsAsync(token);
    }

    /// <summary>
    /// Pulls a new model from the Ollama API (only supported by Ollama).
    /// </summary>
    /// <param name="modelName">The name of the model to pull.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The models.</returns>
    public static async Task PullModelAsync(string modelName, CancellationToken token = default)
    {
        IAIProvider provider = GetCurrentProvider();
        if (provider is OllamaProvider ollamaProvider)
        {
            await ollamaProvider.PullModelAsync(modelName, token);
        }
        else
        {
            throw new NotSupportedException("Model pulling is only supported by Ollama provider.");
        }
    }

    /// <summary>
    /// Deletes a model from Ollama (only supported by Ollama).
    /// </summary>
    /// <param name="modelName">The name of the model to delete.</param>
    /// <param name="token">The cancellation token.</param>
    public static async Task DeleteModelAsync(string modelName, CancellationToken token = default)
    {
        IAIProvider provider = GetCurrentProvider();
        if (provider is OllamaProvider ollamaProvider)
        {
            await ollamaProvider.DeleteModelAsync(modelName, token);
        }
        else
        {
            throw new NotSupportedException("Model deletion is only supported by Ollama provider.");
        }
    }

    public static void ShowNotification(string message, bool isBusy, bool isError)
    {
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow mainWindow = (MainWindow)desktop.MainWindow!;
            mainWindow.HideLastNotification();
            mainWindow.ShowNotification("ClippyAI", message, isBusy, isError);
        }
    }

    /// <summary>
    /// Stores a clipboard/answer pair in the local vector database.
    /// Embeddings are generated with the configured AI provider (Ollama or OpenAI).
    /// </summary>
    /// <param name="task">The task that was executed.</param>
    /// <param name="clipboard_data">The clipboard text that produced the answer.</param>
    /// <param name="answer">The answer to store.</param>
    /// <returns><c>true</c> when a new row was stored, <c>false</c> for a duplicate.</returns>
    public static async Task<bool> StoreEmbedding(string task, string clipboard_data, string answer)
    {
        if (!EmbeddingsEnabled())
        {
            System.Diagnostics.Debug.WriteLine("Embeddings are disabled. Skipping storage.");
            return false;
        }

        clipboard_data = clipboard_data.Trim('\n').Trim();
        if (string.IsNullOrWhiteSpace(task) || string.IsNullOrWhiteSpace(clipboard_data))
        {
            return false;
        }

        (string tableName, float[] vector) = await EmbedForStorageAsync(clipboard_data);

        bool stored = await EmbeddingStore.StoreAsync(
            tableName, task, clipboard_data, answer, vector, DuplicateCosineDistance);

        if (!stored)
        {
            Console.WriteLine("Similar embedding already exists.");
        }

        return stored;
    }

    /// <summary>
    /// Retrieves the cached answers that are closest to <paramref name="clipboard_data"/>.
    /// </summary>
    /// <param name="task">Only answers stored for this task are considered.</param>
    /// <param name="clipboard_data">The clipboard text to search for.</param>
    /// <param name="threshold">Maximum cosine distance (lower is more similar).</param>
    /// <returns>The matching answers, closest first.</returns>
    public static async Task<List<Embedding>> RetrieveAnswersForTask(string task, string clipboard_data, float threshold)
    {
        if (!EmbeddingsEnabled())
        {
            System.Diagnostics.Debug.WriteLine("Embeddings are disabled. Returning empty list.");
            return [];
        }

        string provider = EmbeddingService.Provider;
        string model = EmbeddingService.GetModel(provider);
        string tableName = EmbeddingStore.BuildTableName(provider, model);

        float[] vector = await EmbeddingService.EmbedAsync(clipboard_data, EmbeddingPurpose.Query);
        return await EmbeddingStore.SearchAsync(tableName, task, vector, threshold);
    }

    /// <summary>
    /// Verifies the local vector database and creates it on first use.
    /// </summary>
    public static void InitializeEmbeddings()
    {
        if (!EmbeddingsEnabled())
        {
            System.Diagnostics.Debug.WriteLine("Embeddings are disabled. Skipping initialization.");
            return;
        }

        EmbeddingStore.EnsureInitialized();
    }

    /// <summary>
    /// Deletes all cached answers from the local vector database.
    /// </summary>
    public static async Task ClearEmbeddings()
    {
        if (!EmbeddingsEnabled())
        {
            System.Diagnostics.Debug.WriteLine("Embeddings are disabled. Skipping clear operation.");
            return;
        }

        await EmbeddingStore.ClearAsync();
    }

    /// <summary>
    /// Gets the number of cached answers, or -1 when the store cannot be read.
    /// </summary>
    public static async Task<int> GetEmbeddingsCount()
    {
        if (!EmbeddingsEnabled())
        {
            return 0;
        }

        try
        {
            long count = await EmbeddingStore.CountAsync();
            return count > int.MaxValue ? int.MaxValue : (int)count;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>
    /// Deletes one cached answer from the local vector database.
    /// </summary>
    /// <param name="id">The id of the embedding to delete.</param>
    public static async Task DeleteEmbedding(long id)
    {
        if (!EmbeddingsEnabled())
        {
            System.Diagnostics.Debug.WriteLine("Embeddings are disabled. Skipping delete operation.");
            return;
        }

        _ = await EmbeddingStore.DeleteAsync(id);
    }

    private static bool EmbeddingsEnabled() =>
        ConfigurationService.GetConfigurationValue("UseEmbeddings") == "True";

    private static async Task<(string TableName, float[] Vector)> EmbedForStorageAsync(string clipboardData)
    {
        string provider = EmbeddingService.Provider;
        string model = EmbeddingService.GetModel(provider);
        float[] vector = await EmbeddingService.EmbedAsync(clipboardData, EmbeddingPurpose.Document);
        return (EmbeddingStore.BuildTableName(provider, model), vector);
    }

    /// <summary>
    /// Analyze an image using the AI API.
    /// </summary>
    /// <param name="image">The image to analyze.</param>
    /// <param name="taskConfig">Optional task-specific configuration (model and prompt override).</param>
    /// <returns>The analysis result.</returns>
    public static async Task<string> AnalyzeImage(byte[] image, TaskConfiguration? taskConfig = null)
    {
        IAIProvider provider = GetCurrentProvider();
        return await provider.AnalyzeImage(image, taskConfig);
    }
}