using System.Collections.Generic;
namespace ClippyAI.Models;

public class OllamaModelRequest
{
    public List<OllamaModels> models { get; set; } = new();
}

public class OllamaModelDetails
{
    public string? format { get; set; }
    public string? family { get; set; }
    public string[]? families { get; set; }
    public string? parameter_size { get; set; }
    public string? quantization_level { get; set; }
}

public class OllamaModels
{
    public string? name { get; set; }
    public string? modified_at { get; set; }
    public long size { get; set; }
    public string? digest { get; set; }
    public OllamaModelDetails details { get; set; } = new OllamaModelDetails();
}
