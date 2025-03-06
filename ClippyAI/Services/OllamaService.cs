using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using ClippyAI.Models;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ClippyAI.Views;
using Npgsql;

namespace ClippyAI.Services;

public static class OllamaService
{
    private static readonly HttpClientHandler handler = new()
    {
        UseProxy = false
    };
    private static readonly HttpClient client = new(handler)
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    private static string? url = ConfigurationManager.AppSettings?.Get("OllamaUrl");
    private static string? system = ConfigurationManager.AppSettings?.Get("System");
    private static string? connectionString = ConfigurationManager.AppSettings?.Get("PostgreSqlConnection");
    private static string? pgOllamaUrl = ConfigurationManager.AppSettings?.Get("PostgresOllamaUrl");
    private static string? videoDevice = ConfigurationManager.AppSettings?.Get("VideoDevice");
    private static string? visionModel = ConfigurationManager.AppSettings?.Get("VisionModel");
    private static string? visionPrompt = ConfigurationManager.AppSettings?.Get("VisionPrompt");

    private static void UpdateConfig()
    {
        url = ConfigurationManager.AppSettings?.Get("OllamaUrl");
        system = ConfigurationManager.AppSettings?.Get("System");
        connectionString = ConfigurationManager.AppSettings?.Get("PostgreSqlConnection");
        pgOllamaUrl = ConfigurationManager.AppSettings?.Get("PostgresOllamaUrl");
        videoDevice = ConfigurationManager.AppSettings?.Get("VideoDevice");
        visionModel = ConfigurationManager.AppSettings?.Get("VisionModel");
        visionPrompt = ConfigurationManager.AppSettings?.Get("VisionPrompt");
    }

    /// <summary>
    /// Sends a request to the Ollama API.
    /// </summary>
    /// <param name="input">The clipboard data to send.</param>
    /// <param name="task">The task to perform.</param>
    /// <param name="typeOutput">Whether to simulate typing the output.</param>
    /// <param name="token">The cancellation token.</param>
    public static async Task<string?> SendRequest(string input, string task,
                                                  string model, CancellationToken token = default)
    {
        string? fullResponse = null;
        OllamaRequest body = new()
        {
            prompt = $"# TEXT\n\n'''{input.Trim()}'''\n# TASK\n\n'{task.Trim()}'",
            model = model,
            system = system,
            stream = true,
            keep_alive = "60m",
            options = new OllamaModelOptions()
            {
                num_ctx = 512
            }
        };
        Console.WriteLine("Sending request...");

        // send the request
        using var response = await client.PostAsync(
                             url + "/generate",
                             new StringContent(JsonSerializer.Serialize(body),
                             Encoding.UTF8,
                             "application/json"),
                             token).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Request successful.");

            using var responseStream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(responseStream);

            string? line;
            while ((line = await reader.ReadLineAsync(token)) != null &&
                    !token.IsCancellationRequested)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var responseObj = JsonSerializer.Deserialize<OllamaResponse>(line);
                    string? responseText = responseObj?.response;
                    fullResponse += responseText;
                }
            }
        }
        else
        {
            throw new Exception($"Request failed with status: {response.StatusCode}.");
        }

        // remove double quotes from the beginning and end of the response
        if (fullResponse != null)
        {
            fullResponse = fullResponse.Trim('"').Trim('\'');
        }

        return fullResponse;
    }

    /// <summary>
    /// Get models from the Ollama API.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The models.</returns>
    public static ObservableCollection<string> GetModels(CancellationToken token = default)
    {
        return GetModelsAsync(token).GetAwaiter().GetResult();
    }
    /// <summary>
    /// Get models from the Ollama API.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The models.</returns>
    public static async Task<ObservableCollection<string>> GetModelsAsync(CancellationToken token = default)
    {
        List<string> models = [];
        HttpResponseMessage? response = null;
        try
        {
            response = await client.GetAsync(
                                 $"{url}/tags",
                                 token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        if (response != null && response.IsSuccessStatusCode)
        {
            using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream);
            string? output = await reader.ReadToEndAsync();

            if (output != null)
            {
                // only collect model names
                var deserializedModels = JsonSerializer.Deserialize<OllamaModelRequest>(output)?.models;

                if (deserializedModels != null)
                {
                    foreach (var model in deserializedModels)
                    {
                        models.Add(model!.name!);
                    }
                }
            }
        }
        else
        {
            Console.WriteLine($"Request failed with status: {response?.StatusCode}.");
        }

        // convert list to observable collection
        var oc = new ObservableCollection<string>();
        foreach (var item in models)
            oc.Add(item);

        return oc;
    }

    /// <summary>
    /// Pulls a new model from the Ollama API.
    /// </summary>
    /// <param name="modelName">The name of the model to pull.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The models.</returns>
    public static async Task PullModelAsync(string modelName, CancellationToken token = default)
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

            ShowNotification($"Pulling model {modelName}", true, false);

            response = await client.PostAsync(
                                             $"{url}/pull",
                                             new StringContent(JsonSerializer.Serialize(requestBody),
                                             Encoding.UTF8,
                                             "application/json"),
                                             token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using var streamContent = await response.Content.ReadAsStreamAsync(token);
                using var reader = new StreamReader(streamContent);

                string? line;
                DateTime lastNotificationTime = DateTime.MinValue;
                while ((line = await reader.ReadLineAsync(token)) != null && !token.IsCancellationRequested)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var statusUpdate = JsonSerializer.Deserialize<Dictionary<string, object>?>(line);
                        if (statusUpdate == null)
                            continue;

                        string statusMessage = statusUpdate["status"]?.ToString() ?? "Model pull in progress";

                        if (statusUpdate.TryGetValue("completed", out object? completed) &&
                           statusUpdate.TryGetValue("total", out object? total))
                        {
                            statusMessage += $" ({completed}/{total})";
                        }

                        // Check if 5 seconds have passed since the last notification
                        if ((DateTime.Now - lastNotificationTime).TotalSeconds >= 5)
                        {
                            ShowNotification(statusMessage, true, false);
                            lastNotificationTime = DateTime.Now;
                        }

                        if (statusUpdate?.ContainsKey("completed") == true)
                        {
                            if (statusUpdate["completed"]?.ToString() == statusUpdate["total"]?.ToString())
                            {
                                ShowNotification("Model pull completed.", false, false);
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                ShowNotification("Model pull failed with status: " + response.StatusCode, false, true);
            }
        }
        catch (Exception)
        {
            ShowNotification("Model pull failed.", false, true);
        }
    }
    
    /// <summary>
    /// Deletes a model from Ollama.
    /// </summary>
    /// <param name="modelName">The name of the model to delete.</param>
    /// <param name="token">The cancellation token.</param>
    public static async Task DeleteModelAsync(string modelName, CancellationToken token = default)
    {
        HttpResponseMessage? response;
        try
        {
            var requestBody = new
            {
                name = modelName
            };
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{url}/delete")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            response = await client.SendAsync(request, token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                ShowNotification("Model deleted successfully.", false, false);
            }
            else
            {
                ShowNotification("Model deletion failed with status: " + response.StatusCode, false, true);
            }
        }
        catch (Exception)
        {
            ShowNotification("Model deletion failed.", false, true);
        }
    }

    public static void ShowNotification(string message, bool isBusy, bool isError)
    {
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = (MainWindow)desktop.MainWindow!;
            mainWindow.HideLastNotification();
            mainWindow.ShowNotification("ClippyAI", message, isBusy, isError);
        }
    }

    /// <summary>
    /// Stores the clipboard text and generates the embedding in the PostgreSql vector database.
    /// </summary>
    /// <param name="task">The task that was executed.</param>
    /// <param name="clipboard_data">The clipboard_data/task to store.</param>
    /// <param name="answer">The answer to store.</param>
    public static async Task StoreSqlEmbedding(string task, string clipboard_data, string answer)
    {
        UpdateConfig();
        
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        
        clipboard_data = clipboard_data.Trim('\n').Trim();

        // check if no similar embeddings exist
        var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*)
            FROM clippy
            WHERE task = @task
            AND embedding_clipboard_data <-> ai.ollama_embed('nomic-embed-text', @clipboard_data) <= 1",
        conn);

        cmd.Parameters.AddWithValue("task", task);
        cmd.Parameters.AddWithValue("clipboard_data", clipboard_data);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        if (count > 0)
        {
            Console.WriteLine("Similar embedding already exists.");
            return;
        }

        // Insert the clipboard text and generate the embedding in a single command
        cmd = new NpgsqlCommand(@"
            INSERT INTO clippy (task, clipboard_data, answer, embedding_clipboard_data)
            VALUES (@task,
                   @clipboard_data,
                   @answer,
                   ai.ollama_embed('nomic-embed-text', @clipboard_data))", conn);

        cmd.Parameters.AddWithValue("task", task);
        cmd.Parameters.AddWithValue("clipboard_data", "search_document: " + clipboard_data);
        cmd.Parameters.AddWithValue("answer", answer);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Retrieves the most similar answers by using embeddings from the PostgreSql vector database.
    /// </summary>
    /// <param name="clipboard_data">The clipboard_data to use for retrieval.</param>
    /// <param name="threshold">The threshold for similarity.</param>
    /// <returns>The most similar answers as list of embeddings</returns>
    public static async Task<List<Embedding>> RetrieveAnswersForTask(string task, string clipboard_data, float threshold)
    {
        UpdateConfig();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(@"
        SELECT 
            id, 
            answer, 
            embedding_clipboard_data <=> 
                ai.ollama_embed('nomic-embed-text', @clipboard_data) as distance
        FROM clippy
        WHERE 
            task = @task 
        AND embedding_clipboard_data <=> 
            ai.ollama_embed('nomic-embed-text', @clipboard_data) <= @threshold
        ORDER BY 3
        LIMIT 10", conn);
        
        cmd.Parameters.AddWithValue("task", task);
        cmd.Parameters.AddWithValue("clipboard_data", "search_query: " + clipboard_data);
        cmd.Parameters.AddWithValue("threshold", threshold);

        var result = await cmd.ExecuteReaderAsync();
        
        // generate a list of answers
        List<Embedding> answers = [];
        while (await result.ReadAsync())
        {
            answers.Add(new Embedding
            {
                Id = result.GetInt32(0),
                Answer = result.GetString(1),
                Distance = result.GetFloat(2)
            });
        }
        return answers;
    }

    /// <summary>
    /// Initializes the PostgreSql vector database.
    /// </summary>
    /// <returns>The task.</returns>
    public static void InitializeEmbeddings()
    {
        UpdateConfig();

        if(pgOllamaUrl == null)
        {
            throw new Exception("PostgresOllamaUrl is not set.");
        }
        
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();

        // Install the pgai extension if it doesn't exist
        var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS ai CASCADE", conn);
        cmd.ExecuteNonQuery();

        // drop table if it exists (for debugging)
        // cmd = new NpgsqlCommand("DROP TABLE IF EXISTS clippy", conn);
        // cmd.ExecuteNonQuery();

        // Create the table if it doesn't exist
        cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS clippy (
                id int not null primary key generated by default as identity,
                task TEXT NOT NULL,
                clipboard_data TEXT NOT NULL,
                answer TEXT NOT NULL,
                embedding_clipboard_data vector(768)
            )", conn);
        cmd.ExecuteNonQuery();

        // Create the index if it doesn't exist
        cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_clippy_embedding_clipboard_data
            ON clippy
            USING ivfflat(embedding_clipboard_data vector_cosine_ops)
            WITH (lists = 20)", conn);
        cmd.ExecuteNonQuery();

        // set docker host for the ai extension
        cmd = new NpgsqlCommand("SELECT set_config('ai.ollama_host', @host, false)", conn);
        cmd.Parameters.AddWithValue("host", pgOllamaUrl);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes all embeddings from the PostgreSql vector database.
    /// </summary>
    /// <returns>The task.</returns>
    public static async Task ClearEmbeddings()
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("DELETE FROM clippy", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Get total number of embeddings in the PostgreSql vector database.
    /// </summary>
    /// <returns>The number of embeddings or -1 if there was an error.</returns>
    public static async Task<int> GetEmbeddingsCount()
    {
        int count = 0;
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM clippy", conn);
            count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        catch(Exception)
        {
            return -1;
        }

        return count;
    }

    /// <summary>
    /// Delete an embedding from the PostgreSql vector database.
    /// </summary>
    /// <param name="id">The id of the embedding to delete.</param>
    /// <returns>The task.</returns>
    public static async Task DeleteEmbedding(int id)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("DELETE FROM clippy WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Analyze an image using the Ollama API.
    /// </summary>
    /// <param name="image">The image to analyze.</param>
    /// <returns>The analysis result.</returns>
    public static async Task<string> AnalyzeImage(byte[] image)
    {
        UpdateConfig();

        OllamaRequest body = new()
        {
            images = [Convert.ToBase64String(image)],
            model = visionModel ?? "llama3.2-vision",
            prompt = visionPrompt ?? "Detect what you can find in the image. Use markdown to format the text.",
            stream = false,
            keep_alive = "5m",
        };

        // send the request
        using var response = await client.PostAsync(
                             url + "/generate",
                             new StringContent(JsonSerializer.Serialize(body),
                             Encoding.UTF8,
                             "application/json")).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(responseStream);

            string? line;
            string fullResponse = "";
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var responseObj = JsonSerializer.Deserialize<OllamaResponse>(line);
                    string? responseText = responseObj?.response;
                    fullResponse += responseText;
                }
            }
            return fullResponse;
        }
        else
        {
            throw new Exception($"Request failed with status: {response.StatusCode}.");
        }
    }
}
