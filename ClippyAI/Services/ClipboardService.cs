using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
namespace ClippyAI.Services;

public static class ClipboardService
{
    /// <summary>
    /// Sets the text content of the clipboard.
    /// </summary>
    /// <param name="text">The text to set.</param>
    public static async Task SetText(string? text)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } provider)
            throw new NullReferenceException("Missing Clipboard instance.");

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
            throw new InvalidOperationException("Clipboard does not contain text.");

        return await provider.GetTextAsync();
    }
}
