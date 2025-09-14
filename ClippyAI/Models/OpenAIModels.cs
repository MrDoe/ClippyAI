using System.Text.Json.Serialization;

namespace ClippyAI.Models;

/// <summary>
/// OpenAI Chat Completion Request model
/// </summary>
public class OpenAIRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("messages")]
    public OpenAIMessage[]? Messages { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }

    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; set; }

    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }

    [JsonPropertyName("stop")]
    public string[]? Stop { get; set; }
}

/// <summary>
/// OpenAI Message model
/// </summary>
public class OpenAIMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

/// <summary>
/// OpenAI Chat Completion Response model
/// </summary>
public class OpenAIResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public OpenAIChoice[]? Choices { get; set; }

    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}

/// <summary>
/// OpenAI Choice model
/// </summary>
public class OpenAIChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public OpenAIMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    [JsonPropertyName("delta")]
    public OpenAIMessage? Delta { get; set; }
}

/// <summary>
/// OpenAI Usage model
/// </summary>
public class OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

/// <summary>
/// OpenAI Models list response
/// </summary>
public class OpenAIModelsResponse
{
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("data")]
    public OpenAIModel[]? Data { get; set; }
}

/// <summary>
/// OpenAI Model information
/// </summary>
public class OpenAIModel
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; set; }
}