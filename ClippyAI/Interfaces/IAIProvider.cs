using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ClippyAI.Models;

namespace ClippyAI.Interfaces;

/// <summary>
/// Interface for AI providers (Ollama, OpenAI, etc.)
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Sends a request to the AI provider with configuration options.
    /// </summary>
    /// <param name="input">The input text to process.</param>
    /// <param name="task">The task to perform.</param>
    /// <param name="model">The model to use.</param>
    /// <param name="taskConfig">Optional task-specific configuration.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The AI response text.</returns>
    Task<string?> SendRequestWithConfig(string input, string task, string model, 
                                       TaskConfiguration? taskConfig = null, 
                                       CancellationToken token = default);

    /// <summary>
    /// Gets available models from the AI provider.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>Collection of available model names.</returns>
    Task<ObservableCollection<string>> GetModelsAsync(CancellationToken token = default);

    /// <summary>
    /// Gets available models from the AI provider (synchronous).
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>Collection of available model names.</returns>
    ObservableCollection<string> GetModels(CancellationToken token = default);

    /// <summary>
    /// Analyzes an image if the provider supports vision capabilities.
    /// </summary>
    /// <param name="image">The image bytes to analyze.</param>
    /// <returns>The analysis result.</returns>
    Task<string> AnalyzeImage(byte[] image);

    /// <summary>
    /// Checks if the provider supports the specified capability.
    /// </summary>
    /// <param name="capability">The capability to check.</param>
    /// <returns>True if supported, false otherwise.</returns>
    bool SupportsCapability(AIProviderCapability capability);
}

/// <summary>
/// Capabilities that AI providers may support.
/// </summary>
public enum AIProviderCapability
{
    Vision,
    Embeddings,
    ModelPulling,
    ModelDeletion,
    Streaming
}