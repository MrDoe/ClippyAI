using System;
using System.Threading;

namespace ClippyAI.Services;

/// <summary>
/// Hard limits applied to every model request. They are the safety net for bad
/// task configurations and runaway models: callers may ask for more, but the
/// request path never sends more tokens, never sends more text and never waits
/// longer than these values.
/// </summary>
public static class RequestLimits
{
    public const int MinOutputTokens = 1;
    public const int DefaultOutputTokens = 2048;
    public const int MaxOutputTokens = 8192;

    public const int MinInputCharacters = 1000;
    public const int DefaultInputCharacters = 24000;
    public const int MaxInputCharacters = 128000;

    public const int MinTimeoutSeconds = 15;
    public const int DefaultTimeoutSeconds = 600;
    public const int MaxTimeoutSeconds = 3600;

    /// <summary>Budget for short metadata calls such as fetching the model list.</summary>
    public const int MetadataTimeoutSeconds = 20;

    /// <summary>
    /// Clamps a task's configured maximum output length into the range the app
    /// is willing to wait for.
    /// </summary>
    public static int ClampOutputTokens(int? requested) =>
        Math.Clamp(requested ?? DefaultOutputTokens, MinOutputTokens, MaxOutputTokens);

    /// <summary>Maximum number of clipboard characters sent to the model.</summary>
    public static int InputCharacterLimit =>
        ReadInt("MaxInputCharacters", DefaultInputCharacters, MinInputCharacters, MaxInputCharacters);

    /// <summary>Wall-clock budget for one model request.</summary>
    public static int TimeoutSeconds =>
        ReadInt("RequestTimeoutSeconds", DefaultTimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds);

    /// <summary>Truncates oversized input so it cannot overflow the model context.</summary>
    public static string TruncateInput(string? input)
    {
        string value = input?.Trim() ?? string.Empty;
        int limit = InputCharacterLimit;

        return value.Length <= limit
            ? value
            : value[..limit] + "\n\n[... input truncated by ClippyAI ...]";
    }

    /// <summary>
    /// Creates a token source that cancels after <paramref name="timeoutSeconds"/>
    /// (or <see cref="TimeoutSeconds"/>) or when the caller's token is cancelled.
    /// </summary>
    public static CancellationTokenSource CreateTimeoutSource(CancellationToken userToken, int? timeoutSeconds = null)
    {
        CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(userToken);
        source.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds ?? TimeoutSeconds));
        return source;
    }

    /// <summary>
    /// Builds the error surfaced when a request exceeded its budget instead of
    /// being cancelled by the user.
    /// </summary>
    public static TimeoutException CreateTimeoutException(int timeoutSeconds) =>
        new($"The model did not answer within {timeoutSeconds} seconds and the request was cancelled. " +
            "Reduce Max Length, choose a faster model, or increase RequestTimeoutSeconds.");

    private static int ReadInt(string key, int defaultValue, int min, int max)
    {
        string raw = ConfigurationService.GetConfigurationValue(key, defaultValue.ToString());
        return int.TryParse(raw, out int value) ? Math.Clamp(value, min, max) : defaultValue;
    }
}
