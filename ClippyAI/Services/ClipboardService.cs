using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace ClippyAI.Services;

/// <summary>
/// A clipboard image together with its PNG encoding - the PNG bytes are the
/// canonical comparison and history-storage form.
/// </summary>
public sealed record ClipboardImageSnapshot(Bitmap Bitmap, byte[] PngBytes);

public static class ClipboardService
{
    public static string LastResponse { get; set; } = string.Empty;
    public static string LastInput { get; set; } = string.Empty;

    // Cache to avoid unnecessary clipboard API calls
    private static string[]? _lastFormats = null;
    private static DateTime _lastFormatsCheck = DateTime.MinValue;
    private const int FORMATS_CACHE_MS = 100; // Cache formats for 100ms to reduce API calls

    // Windows hands out a brand-new Bitmap on every read even when the
    // clipboard did not change, and comparing pixels every second is wasteful.
    // The clipboard sequence number changes on every real clipboard write, so
    // the monitor only re-reads and re-hashes images when it differs.
    private static uint _imageSyncedSequence;

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    /// <summary>
    /// Gets the available clipboard formats with caching to reduce API calls.
    /// </summary>
    /// <returns>Array of available formats or null if unavailable.</returns>
    public static Task<string[]?> GetFormatsAsync() => GetFormatsWithCache();

    /// <summary>
    /// True when the clipboard bitmap payload may differ from what was last
    /// processed. Outside Windows there is no cheap change signal, so this
    /// always returns true and callers compare content hashes instead.
    /// </summary>
    public static bool ImagePayloadMayHaveChanged()
    {
        if (OperatingSystem.IsWindows())
        {
            return ClipboardSequence() != _imageSyncedSequence;
        }

        return true;
    }

    /// <summary>Marks the current image state as processed (resets the change gate).</summary>
    public static void MarkImageSynced()
    {
        if (OperatingSystem.IsWindows())
        {
            _imageSyncedSequence = ClipboardSequence();
        }
    }

    [SupportedOSPlatform("windows")]
    private static uint ClipboardSequence() => GetClipboardSequenceNumber();

    public static bool HasImage()
    {
        // Fast path only meaningful right after GetFormatsAsync() (cached 100 ms).
        return _lastFormats?.Any(f => string.Equals(f, DataFormat.Bitmap.Identifier, StringComparison.OrdinalIgnoreCase)) == true;
    }

    /// <summary>
    /// Gets the clipboard image with its PNG encoding, or null when the
    /// clipboard carries no bitmap or the payload cannot be read.
    /// </summary>
    public static async Task<ClipboardImageSnapshot?> GetImageSnapshotAsync()
    {
        Bitmap? bitmap = await GetImage();
        if (bitmap == null)
        {
            return null;
        }

        try
        {
            using MemoryStream ms = new();
            bitmap.Save(ms);
            return new ClipboardImageSnapshot(bitmap, ms.ToArray());
        }
        catch
        {
            bitmap.Dispose();
            return null;
        }
    }

    /// <summary>
    /// Puts an image file on the clipboard (used to restore images from the
    /// clipboard history). Implemented as a file transfer, which image-aware
    /// applications accept as picture content.
    /// </summary>
    public static async Task SetImageFile(string filePath)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.Clipboard is not { } provider)
        {
            throw new NullReferenceException("Missing Clipboard instance.");
        }

        IStorageFile? storageFile = await desktop.MainWindow!.StorageProvider.TryGetFileFromPathAsync(filePath);
        if (storageFile == null)
        {
            throw new InvalidOperationException($"Could not reference history image: {filePath}");
        }

        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.CreateFile(storageFile));
        await provider.SetDataAsync(dataTransfer);

        _lastFormats = null;
        _lastFormatsCheck = DateTime.MinValue;
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
}
