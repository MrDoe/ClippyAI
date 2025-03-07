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

        // deny other content than text
        string[] formats = await provider.GetFormatsAsync();
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

        // Check if the clipboard contains image data
        var formats = await clipboard.GetFormatsAsync();
        if (!formats.Contains("image/png") && !formats.Contains("PNG"))
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
        if(data == null)
            return null;
        else if (data is byte[] imageData)
        {
            using var stream = new MemoryStream(imageData);
            return new Bitmap(stream);
        }

        return null;
    }
}
