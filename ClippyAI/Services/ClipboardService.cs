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
#pragma warning disable CS0618
            _lastFormats = await provider.GetFormatsAsync();
#pragma warning restore CS0618
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

        // Invalidate the formats cache so the next poll reads fresh data.
        _lastFormats = null;
        _lastFormatsCheck = DateTime.MinValue;

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

        // Directly call GetTextAsync() — it returns null when no text is present.
        // Avoid GetFormatsAsync() here because it can fail silently when the window
        // is hidden (e.g. minimised to taskbar), which would suppress all updates.
#pragma warning disable CS0618
        return await provider.GetTextAsync();
#pragma warning restore CS0618
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
#pragma warning disable CS0618
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
#pragma warning restore CS0618
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
