using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClippyAI.Models;

namespace ClippyAI.Services;

public static class OllamaService
{
    private static readonly HttpClient client = new();
    private static readonly string url = "http://127.0.0.1:11434/api/generate";
    private static readonly string model = "llama3";
    private static readonly string system = "Du schreibst freundliche Antworten auf E-Mails in Deutsch.";

    public static async Task SendRequest(string clipboardData, string task)
    {
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

                    for (int i = 0; i < responseObj?.response?.Length; ++i)
                    {
                        var key = responseObj?.response[i];
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
                            var process3 = Process.Start("xdotool", ["type", key?.ToString() ?? ""]);
                            process3.WaitForExit();
                        }
                        //Console.Write(key);
                    }
                    var process4 = Process.Start("xdotool", ["keyup", "Alt_R", "Control_L", "Control_R", "Shift_L", "Shift_R"]);
                    process4.WaitForExit();
                }
            }
        }
        else
        {
            Console.WriteLine($"Request failed with status: {response.StatusCode}.");
        }
    }
}