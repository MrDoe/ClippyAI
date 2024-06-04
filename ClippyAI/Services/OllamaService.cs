using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Specialized;
using ClippyAI.Models;
using Desktop.Robot;
using Desktop.Robot.Extensions;

namespace ClippyAI.Services;

public static class OllamaService
{
    private static readonly HttpClient client = new();
    private static readonly string? url = ConfigurationManager.AppSettings?.Get("OllamaUrl");
    private static readonly string? model = ConfigurationManager.AppSettings?.Get("Model");
    private static readonly string? system = ConfigurationManager.AppSettings?.Get("System"); 
    
    private static void SimulateTyping(string text)
    {
        var robot = new Robot();
        robot.Type(text);
    }
    
    public static async Task<string?> SendRequest(string clipboardData, string task, bool typeOutput = true, CancellationToken token = default)
    {
        string? fullResponse = null;

        OllamaRequest body = new()
        {
            prompt = $"{clipboardData}<br/>{task}",
            model = model,
            system = system,
            stream = true
        };
        Console.WriteLine("Sending request...");

        using var response = await client.PostAsync(
                             url,
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
            Console.WriteLine($"Request failed with status: {response.StatusCode}.");
        }
        return fullResponse;
    }
}