using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using ClippyAI.Models;
using Desktop.Robot;
using Desktop.Robot.Extensions;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

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

    private static readonly string? url = ConfigurationManager.AppSettings?.Get("OllamaUrl");
    private static readonly string? system = ConfigurationManager.AppSettings?.Get("System");

    /// <summary>
    /// Simulates typing the given text.
    /// </summary>
    /// <param name="text">The text to type.</param>
    private static void SimulateTyping(string text)
    {
        var robot = new Robot();
        robot.Type(text, 80);
    }

    /// <summary>
    /// Sends a request to the Ollama API.
    /// </summary>
    /// <param name="clipboardData">The clipboard data to send.</param>
    /// <param name="task">The task to perform.</param>
    /// <param name="typeOutput">Whether to simulate typing the output.</param>
    /// <param name="token">The cancellation token.</param>
    public static async Task<string?> SendRequest(string clipboardData, string task,
                                                  string model, bool typeOutput = true,
                                                  CancellationToken token = default)
    {
        string? fullResponse = null;

        OllamaRequest body = new()
        {
            prompt = $"# TEXT\n\n'''{clipboardData}'''\n# TASK\n\n'{task}'",
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

        using var response = await client.PostAsync(
                             url + "/generate",
                             new StringContent(JsonSerializer.Serialize(body),
                             Encoding.UTF8,
                             "application/json"),
                             token).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Request successful.");

            using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(token)) != null &&
                    !token.IsCancellationRequested)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var responseObj = JsonSerializer.Deserialize<OllamaResponse>(line);
                    string? responseText = responseObj?.response;
                    fullResponse += responseText;

                    if (typeOutput)
                        SimulateTyping(responseText!);
                }
            }
        }
        else
        {
            throw new Exception($"Request failed with status: {response.StatusCode}.");
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
        catch(Exception e)
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
}