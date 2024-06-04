using System;
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

namespace ClippyAI.Services;

public static class OllamaService
{
    private static readonly HttpClient client = new();
    private static readonly string? url = ConfigurationManager.AppSettings?.Get("OllamaUrl");
    private static readonly string? model = ConfigurationManager.AppSettings?.Get("Model");
    private static readonly string? system = ConfigurationManager.AppSettings?.Get("System");

    private static void SimulateTyping(string text)
    {
        foreach (var key in text)
        {
            if (key == '\n')
            {
                var process1 = Process.Start("xdotool", ["key", "Return"]);
                process1.WaitForExit();
            }
            else if (key == '\t')
            {
                var process2 = Process.Start("xdotool", ["key", "Tab"]);
                process2.WaitForExit();
            }
            else
            {
                var process3 = Process.Start("xdotool", ["type", key.ToString()]);
                process3.WaitForExit();
            }
        }

        // release all modifier keys
        var process4 = Process.Start("xdotool", ["keyup", "Alt_R", "Control_L", "Control_R", "Shift_L", "Shift_R"]);
        process4.WaitForExit();
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