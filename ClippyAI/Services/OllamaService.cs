using System;
using System.Configuration;
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
    private static readonly HttpClient client = new();
    private static readonly string? url = ConfigurationManager.AppSettings?.Get("OllamaUrl");
    private static readonly string? model = ConfigurationManager.AppSettings?.Get("Model");
    private static readonly string? system = ConfigurationManager.AppSettings?.Get("System"); 
    
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
                    responseText += responseObj?.response;

                    if (typeOutput)
                        SimulateTyping(responseText!);
                }
            }

            if (typeOutput)
            {
                var process4 = Process.Start("xdotool", ["keyup", "Alt_R", "Control_L", "Control_R", "Shift_L", "Shift_R"]);
                process4.WaitForExit();
            }
        }
        else
        {
            Console.WriteLine($"Request failed with status: {response.StatusCode}.");
        }       
        return responseText;
    }
}