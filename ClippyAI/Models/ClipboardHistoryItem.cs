using Avalonia.Media.Imaging;
using System;
using System.IO;

namespace ClippyAI.Models;

/// <summary>
/// One recorded clipboard entry. Images are stored as PNG files next to the
/// configuration database; the database keeps only the file name.
/// </summary>
public sealed class ClipboardHistoryItem
{
    public long Id { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>"Text" or "Image".</summary>
    public string Kind { get; init; } = "Text";

    /// <summary>Text payload for Kind=Text.</summary>
    public string? Content { get; init; }

    /// <summary>Absolute path of the PNG file for Kind=Image.</summary>
    public string? ImagePath { get; init; }

    /// <summary>SHA-256 of the text or PNG bytes - also the file name of images.</summary>
    public string Hash { get; init; } = "";

    /// <summary>
    /// For image entries: best-effort capture of the foreground window at the
    /// moment of recording ("Paint — 'Bild - Paint' — mspaint.exe 11.2403").
    /// Doubles as a hint for the manual generator's app name.
    /// </summary>
    public string? SourceContext { get; init; }

    public bool IsImage => Kind == "Image";

    public string Preview
    {
        get
        {
            if (IsImage)
            {
                return System.IO.Path.GetFileName(ImagePath ?? "");
            }

            string text = (Content ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            const int max = 160;
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
    }

    private Bitmap? _thumbnail;
    private bool _thumbnailLoaded;

    /// <summary>Lazy 400px preview bitmap for image entries; null for text or on decode errors.</summary>
    public Bitmap? Thumbnail
    {
        get
        {
            if (!_thumbnailLoaded)
            {
                _thumbnailLoaded = true;
                try
                {
                    if (IsImage && !string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath))
                    {
                        using FileStream stream = File.OpenRead(ImagePath);
                        _thumbnail = Bitmap.DecodeToWidth(stream, 400);
                    }
                }
                catch
                {
                    _thumbnail = null;
                }
            }

            return _thumbnail;
        }
    }

    public string CreatedAtDisplay => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
}
