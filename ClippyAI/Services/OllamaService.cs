using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClippyAI.Models;
using Desktop.Robot;
using Desktop.Robot.Extensions;
namespace ClippyAI.Services;

public static class OllamaService
{
    private static readonly HttpClientHandler handler = new()
    {
        UseProxy = false
    };
    private static readonly HttpClient client = new(handler);
    private static readonly string url = "http://localhost:11434/api/generate";
    private static readonly string model = "tinyllama";
    private static readonly string system = "You are writing answers to the tasks given.";

    private static void SimulateTyping(string text)
    {
        var robot = new Robot();
        robot.Type(text);
    }

    public static async Task<string?> SendRequest(string clipboardData, string task, bool typeOutput = true)
    {
        string? responseText = null;
        
        OllamaRequest body = new()
        {
            prompt = $"{clipboardData}<br/>{task}",
            model = model,
            system = system,
            stream = true
        };
        Console.WriteLine("Sending request...");

        var response = await client.PostAsync(
                             url,
                             new StringContent(JsonSerializer.Serialize(body),
                             Encoding.UTF8,
                             "application/json"));

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Request successful.");

            var responseBody = await response.Content.ReadAsStringAsync();
            var lines = responseBody.Split('\n');

            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var responseObj = JsonSerializer.Deserialize<OllamaResponse>(line);
                    string currentText = responseObj?.response ?? string.Empty;
                    responseText += currentText;

                    if (typeOutput)
                        SimulateTyping(currentText);
                }
            }
        }
        else
        {
            Console.WriteLine($"Request failed with status: {response.StatusCode}.");
        }       
        return responseText;
    }
}