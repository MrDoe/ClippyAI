using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ClippyAI.Services;

public static class ClipboardService
{
    public static string LastResponse { get; set; } = string.Empty;
    public static string LastInput { get; set; } = string.Empty;

    // Cache to avoid unnecessary clipboard API calls
    private static string[]? _lastFormats = null;
    private static DateTime _lastFormatsCheck = DateTime.MinValue;
    private const int FORMATS_CACHE_MS = 100; // Cache formats for 100ms to reduce API calls

    /// <summary>
    /// Gets the available clipboard formats with caching to reduce API calls.
    /// </summary>
    /// <returns>Array of available formats or null if unavailable.</returns>
    private static async Task<string[]?> GetFormatsWithCache()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } provider)
        {
            return null;
        }

        // Use cached formats if they're recent enough
        DateTime now = DateTime.UtcNow;
        if (_lastFormats != null && (now - _lastFormatsCheck).TotalMilliseconds < FORMATS_CACHE_MS)
        {
            return _lastFormats;
        }

        try
        {
            IAsyncDataTransfer? dataTransfer = await provider.TryGetDataAsync();
            _lastFormats = dataTransfer?.Formats.Select(f => f.Identifier).ToArray();
            _lastFormatsCheck = now;
            return _lastFormats;
        }
        catch
        {
            _lastFormats = null;
            return null;
        }
    }

    /// <summary>
    /// Sets the text content of the clipboard.
    /// </summary>
    /// <param name="text">The text to set.</param>
    public static async Task SetText(string? text)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } provider)
        {
            throw new NullReferenceException("Missing Clipboard instance.");
        }

        if (text != LastInput)
        {
            LastInput = text ?? string.Empty;
        }

        // Invalidate the formats cache so the next poll reads fresh data.
        _lastFormats = null;
        _lastFormatsCheck = DateTime.MinValue;

        if (text is null)
        {
            await provider.ClearAsync();
            return;
        }

        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateText(text));
        await provider.SetDataAsync(dataTransfer);
    }

    /// <summary>
    /// Gets the text content of the clipboard.
    /// </summary>
    /// <returns>The text content of the clipboard.</returns>
    public static async Task<string?> GetText()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } provider)
        {
            throw new NullReferenceException("Missing Clipboard instance.");
        }

        IAsyncDataTransfer? dataTransfer = await provider.TryGetDataAsync();
        if (dataTransfer is null)
        {
            return null;
        }

        return await dataTransfer.TryGetTextAsync();
    }

    /// <summary>
    /// Gets the image content of the clipboard.
    /// </summary>
    /// <returns>The image content of the clipboard.</returns>
    public static async Task<Bitmap?> GetImage()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } clipboard)
        {
            return null;
        }

        string[] formats = await GetFormatsWithCache() ?? [];
        if (!formats.Any(f => string.Equals(f, DataFormat.Bitmap.Identifier, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        IAsyncDataTransfer? dataTransfer = await clipboard.TryGetDataAsync();
        if (dataTransfer is null)
        {
            return null;
        }

        return await dataTransfer.TryGetBitmapAsync();
    }
}
