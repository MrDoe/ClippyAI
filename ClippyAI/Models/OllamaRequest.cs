namespace ClippyAI.Models;

public class OllamaModelOptions
{
    public int? num_keep { get; set; }
    public int? seed { get; set; }
    public int? num_predict { get; set; }
    public int? top_k { get; set; }
    public double? top_p { get; set; }
    public double? min_p { get; set; }
    public double? tfs_z { get; set; }
    public double? typical_p { get; set; }
    public int? repeat_last_n { get; set; }
    public double? temperature { get; set; }
    public double? repeat_penalty { get; set; }
    public double? presence_penalty { get; set; }
    public double? frequency_penalty { get; set; }
    public int? mirostat { get; set; }
    public double? mirostat_tau { get; set; }
    public double? mirostat_eta { get; set; }
    public bool? penalize_newline { get; set; }
    public string[]? stop { get; set; }
    public bool? numa { get; set; }
    public int? num_ctx { get; set; }
    public int? num_batch { get; set; }
    public int? num_gpu { get; set; }
    public int? main_gpu { get; set; }
    public bool? low_vram { get; set; }
    public bool? f16_kv { get; set; }
    public bool? vocab_only { get; set; }
    public bool? use_mmap { get; set; }
    public bool? use_mlock { get; set; }
    public int? num_thread { get; set; }
}

public class OllamaRequest
{
    public string? prompt { get; set; }
    public string? model { get; set; }
    public string? system { get; set; }
    public bool? stream { get; set; }
    public string? keep_alive { get; set; }
    public string[]? images { get; set; }
    public OllamaModelOptions? options { get; set; }
}