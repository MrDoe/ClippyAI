using System;

namespace ClippyAI.Models;

public class TaskConfiguration
{
    public int Id { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.8;
    public int MaxLength { get; set; } = 2048;
    public double TopP { get; set; } = 0.9;
    public int TopK { get; set; } = 40;
    public double RepeatPenalty { get; set; } = 1.1;
    public int NumCtx { get; set; } = 2048;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class JobConfiguration
{
    public int Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TaskConfigurationId { get; set; }
    public TaskConfiguration? TaskConfiguration { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}