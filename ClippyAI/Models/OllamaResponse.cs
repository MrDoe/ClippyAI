using System;
namespace ClippyAI.Models;

public class OllamaResponse
{
    public string? model { get; set; }
    public DateTime? created_at { get; set; }
    public string? response { get; set; }
    public bool? done { get; set; }
}