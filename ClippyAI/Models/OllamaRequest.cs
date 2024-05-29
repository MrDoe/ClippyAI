namespace ClippyAI.Models;

public class OllamaRequest
{
    public string? prompt { get; set; }
    public string? model { get; set; }
    public string? system { get; set; }
    public bool? stream { get; set; }
}