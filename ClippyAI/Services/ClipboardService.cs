using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using System.IO;

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
            return null;

        // Use cached formats if they're recent enough
        var now = DateTime.UtcNow;
        if (_lastFormats != null && (now - _lastFormatsCheck).TotalMilliseconds < FORMATS_CACHE_MS)
        {
            return _lastFormats;
        }

        try
        {
            _lastFormats = await provider.GetFormatsAsync();
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
            throw new NullReferenceException("Missing Clipboard instance.");

        if (text != LastInput)
            LastInput = text ?? string.Empty;

        await provider.SetTextAsync(text);
    }

    /// <summary>
    /// Gets the text content of the clipboard.
    /// </summary>
    /// <returns>The text content of the clipboard.</returns>
    public static async Task<string?> GetText()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } provider)
            throw new NullReferenceException("Missing Clipboard instance.");

        // Check formats with caching to reduce API calls
        string[] formats = await GetFormatsWithCache() ?? [];
        if (!formats.Contains("Text"))
        {
            return null;
        }
        return await provider.GetTextAsync();
    }

    /// <summary>
    /// Gets the image content of the clipboard.
    /// </summary>
    /// <returns>The image content of the clipboard.</returns>
    public static async Task<Bitmap?> GetImage()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } clipboard)
            return null;

        // Check formats with caching to reduce API calls
        var formats = await GetFormatsWithCache() ?? [];
        if (!formats.Contains("image") && (!formats.Contains("PNG") || !formats.Contains("image/png")))
            return null;

        // Get the image data from the clipboard
        var data = await clipboard.GetDataAsync("image/png");
        data ??= await clipboard.GetDataAsync("PNG");
        data ??= await clipboard.GetDataAsync("image/bmp");
        data ??= await clipboard.GetDataAsync("image/x-bmp");
        data ??= await clipboard.GetDataAsync("image/x-MS-bmp");
        data ??= await clipboard.GetDataAsync("image/x-win-bitmap");
        data ??= await clipboard.GetDataAsync("image/jpeg");
        data ??= await clipboard.GetDataAsync("image/tiff");
        data ??= await clipboard.GetDataAsync("image/webp");
        data ??= await clipboard.GetDataAsync("image/ico");
        data ??= await clipboard.GetDataAsync("image/icon");
        if (data == null)
            return null;
        else if (data is byte[] imageData)
        {
            using var stream = new MemoryStream(imageData);
            return new Bitmap(stream);
        }

        return null;
    }
}
