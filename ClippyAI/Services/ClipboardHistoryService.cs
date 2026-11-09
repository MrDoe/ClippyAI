using ClippyAI.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ClippyAI.Services;

/// <summary>
/// Clipboard history stored in the shared configuration database; images are
/// PNG files in %APPDATA%\ClippyAI\History named by their SHA-256 hash.
/// Consecutive duplicates (re-copies of the same payload) only refresh the
/// entry timestamp. Configuration keys:
///   ClipboardHistoryEnabled (default True)
///   ClipboardHistoryLimit   (default 200 entries, clamped 10..5000)
/// </summary>
public static class ClipboardHistoryService
{
    private const int MaxStoredTextCharacters = 100_000;

    private static readonly string ConnectionString = GetConnectionString();

    public static string HistoryDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClippyAI", "History");

    private static string GetConnectionString()
    {
        string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                     "ClippyAI", "clippyai_config.db");
        string? directory = Path.GetDirectoryName(dbPath);
        if (!Directory.Exists(directory))
        {
            _ = Directory.CreateDirectory(directory!);
        }

        return $"Data Source={dbPath}";
    }

    public static bool Enabled =>
        ConfigurationService.GetConfigurationValue("ClipboardHistoryEnabled", "True").Equals("True", StringComparison.OrdinalIgnoreCase);

    public static int Limit
    {
        get
        {
            string raw = ConfigurationService.GetConfigurationValue("ClipboardHistoryLimit", "200");
            return int.TryParse(raw, out int n) ? Math.Clamp(n, 10, 5000) : 200;
        }
    }

    public static void Initialize()
    {
        try
        {
            if (!Directory.Exists(HistoryDirectory))
            {
                Directory.CreateDirectory(HistoryDirectory);
            }

            using SqliteConnection connection = new(ConnectionString);
            connection.Open();

            const string createHistoryTable = @"
                CREATE TABLE IF NOT EXISTS ClipboardHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CreatedAt TEXT NOT NULL,
                    Kind TEXT NOT NULL,
                    Content TEXT NULL,
                    ImageFile TEXT NULL,
                    Hash TEXT NOT NULL,
                    Context TEXT NULL
                )";

            using (SqliteCommand command = new(createHistoryTable, connection))
            {
                _ = command.ExecuteNonQuery();
            }

            MigrateAddContextColumn(connection);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ClipboardHistory init failed: {ex.Message}");
        }
    }

    private static void MigrateAddContextColumn(SqliteConnection connection)
    {
        bool hasContext = false;
        using (SqliteCommand pragma = new("PRAGMA table_info(ClipboardHistory)", connection))
        using (SqliteDataReader reader = pragma.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "Context", StringComparison.OrdinalIgnoreCase))
                {
                    hasContext = true;
                    break;
                }
            }
        }

        if (!hasContext)
        {
            using SqliteCommand alter = new("ALTER TABLE ClipboardHistory ADD COLUMN Context TEXT NULL", connection);
            _ = alter.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Best-effort description of the foreground window at the moment an image
    /// is recorded: "ProductName — 'window title' — process.exe fileVersion".
    /// Used to pre-fill the manual generator's application name.
    /// </summary>
    private static string? CaptureForegroundContext()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            System.Text.StringBuilder title = new(512);
            _ = GetWindowText(hwnd, title, title.Capacity);

            _ = GetWindowThreadProcessId(hwnd, out uint pid);
            string processName = "";
            string fileVersion = "";
            string product = "";

            if (pid != 0)
            {
                try
                {
                    using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById((int)pid);
                    processName = process.ProcessName;
                    if (process.MainModule != null)
                    {
                        System.Diagnostics.FileVersionInfo fvi = process.MainModule.FileVersionInfo;
                        fileVersion = fvi.FileVersion ?? "";
                        product = fvi.ProductName ?? "";
                    }
                }
                catch
                {
                    // 32/64-bit or access-restricted processes: title only
                }
            }

            string windowTitle = title.ToString().Trim();
            if (windowTitle.Length == 0 && processName.Length == 0)
            {
                return null;
            }

            // Prefer the product name (e.g. "Microsoft Visual Studio Code") over the
            // process name; strip common noise from the title for the app hint.
            string appHint = product.Length > 0 ? product : processName;
            return $"{appHint} \u2014 '{windowTitle}' \u2014 {processName}.exe {fileVersion}".Trim();
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>Records a text payload. Hash-deduplicated against the newest entry.</summary>
    public static void RecordText(string? text)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string content = text.Length > MaxStoredTextCharacters
            ? text.Substring(0, MaxStoredTextCharacters) + "\n… truncated"
            : text;

        Record("Text", content, null, Sha256Hex(Encoding.UTF8.GetBytes(content)));
    }

    /// <summary>Records a clipboard image from its PNG encoding. Hash-deduplicated.</summary>
    public static void RecordImage(byte[]? pngBytes)
    {
        if (!Enabled || pngBytes == null || pngBytes.Length == 0)
        {
            return;
        }

        string hash = Sha256Hex(pngBytes);
        Record("Image", null, hash + ".png", hash, pngBytes, CaptureForegroundContext());
    }

    private static void Record(string kind, string? content, string? imageFile, string hash, byte[]? pngBytes = null, string? context = null)
    {
        try
        {
            using SqliteConnection connection = new(ConnectionString);
            connection.Open();

            // Consecutive identical payload: refresh timestamp, keep one row.
            using (SqliteCommand check = new("SELECT Hash, Kind FROM ClipboardHistory ORDER BY Id DESC LIMIT 1", connection))
            using (SqliteDataReader reader = check.ExecuteReader())
            {
                if (reader.Read() && reader.GetString(0) == hash && reader.GetString(1) == kind)
                {
                    reader.Close();
                    using SqliteCommand touch = new("UPDATE ClipboardHistory SET CreatedAt = @timestamp WHERE Hash = @hash AND Kind = @kind", connection);
                    _ = touch.Parameters.AddWithValue("@timestamp", Now());
                    _ = touch.Parameters.AddWithValue("@hash", hash);
                    _ = touch.Parameters.AddWithValue("@kind", kind);
                    _ = touch.ExecuteNonQuery();
                    return;
                }
            }

            if (kind == "Image" && pngBytes != null)
            {
                string filePath = Path.Combine(HistoryDirectory, imageFile!);
                if (!File.Exists(filePath))
                {
                    File.WriteAllBytes(filePath, pngBytes);
                }
            }

            using (SqliteCommand insert = new(
                "INSERT INTO ClipboardHistory (CreatedAt, Kind, Content, ImageFile, Hash, Context) VALUES (@timestamp, @kind, @content, @imageFile, @hash, @context)", connection))
            {
                _ = insert.Parameters.AddWithValue("@timestamp", Now());
                _ = insert.Parameters.AddWithValue("@kind", kind);
                _ = insert.Parameters.AddWithValue("@content", (object?)content ?? DBNull.Value);
                _ = insert.Parameters.AddWithValue("@imageFile", (object?)imageFile ?? DBNull.Value);
                _ = insert.Parameters.AddWithValue("@hash", hash);
                _ = insert.Parameters.AddWithValue("@context", (object?)context ?? DBNull.Value);
                _ = insert.ExecuteNonQuery();
            }

            Prune(connection);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ClipboardHistory record failed: {ex.Message}");
        }
    }

    /// <summary>Newest-first entries, optional text filter (empty filter = all).</summary>
    public static List<ClipboardHistoryItem> GetItems(string? filter = null)
    {
        List<ClipboardHistoryItem> items = [];

        try
        {
            using SqliteConnection connection = new(ConnectionString);
            connection.Open();

            string sql = @"
                SELECT Id, CreatedAt, Kind, Content, ImageFile, Hash, Context
                FROM ClipboardHistory
                WHERE (@filter = '' OR Content LIKE '%' || @filter || '%' OR Kind = 'Image')
                ORDER BY Id DESC LIMIT @limit";

            using SqliteCommand command = new(sql, connection);
            _ = command.Parameters.AddWithValue("@filter", (filter ?? "").Trim());
            _ = command.Parameters.AddWithValue("@limit", Limit);

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string kind = reader.GetString(2);
                string? imageFile = reader.IsDBNull(4) ? null : reader.GetString(4);

                items.Add(new ClipboardHistoryItem
                {
                    Id = reader.GetInt64(0),
                    CreatedAt = DateTime.TryParse(reader.GetString(1), out DateTime created) ? created : DateTime.MinValue,
                    Kind = kind,
                    Content = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ImagePath = kind == "Image" && imageFile != null ? Path.Combine(HistoryDirectory, imageFile) : null,
                    Hash = reader.GetString(5),
                    SourceContext = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ClipboardHistory query failed: {ex.Message}");
        }

        return items;
    }

    public static void Delete(long id)
    {
        try
        {
            using SqliteConnection connection = new(ConnectionString);
            connection.Open();

            string? imageFile = null;
            using (SqliteCommand select = new("SELECT ImageFile FROM ClipboardHistory WHERE Id = @id", connection))
            {
                _ = select.Parameters.AddWithValue("@id", id);
                object? value = select.ExecuteScalar();
                if (value is string s)
                {
                    imageFile = s;
                }
            }

            using (SqliteCommand delete = new("DELETE FROM ClipboardHistory WHERE Id = @id", connection))
            {
                _ = delete.Parameters.AddWithValue("@id", id);
                _ = delete.ExecuteNonQuery();
            }

            if (imageFile != null)
            {
                DeleteImageFileIfUnreferenced(connection, imageFile);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ClipboardHistory delete failed: {ex.Message}");
        }
    }

    public static void ClearAll()
    {
        try
        {
            using SqliteConnection connection = new(ConnectionString);
            connection.Open();
            _ = new SqliteCommand("DELETE FROM ClipboardHistory", connection).ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ClipboardHistory clear failed: {ex.Message}");
        }

        try
        {
            foreach (string file in Directory.GetFiles(HistoryDirectory, "*.png"))
            {
                File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ClipboardHistory file cleanup failed: {ex.Message}");
        }
    }

    private static void Prune(SqliteConnection connection)
    {
        List<string> overflowImages = [];

        using (SqliteCommand select = new(
            "SELECT ImageFile FROM ClipboardHistory ORDER BY Id DESC LIMIT -1 OFFSET @limit", connection))
        {
            _ = select.Parameters.AddWithValue("@limit", Limit);
            using SqliteDataReader reader = select.ExecuteReader();
            while (reader.Read() && !reader.IsDBNull(0))
            {
                overflowImages.Add(reader.GetString(0));
            }
        }

        using (SqliteCommand delete = new(
            "DELETE FROM ClipboardHistory WHERE Id <= (SELECT Id FROM ClipboardHistory ORDER BY Id DESC LIMIT 1 OFFSET @limit)", connection))
        {
            _ = delete.Parameters.AddWithValue("@limit", Limit);
            _ = delete.ExecuteNonQuery();
        }

        foreach (string imageFile in overflowImages)
        {
            DeleteImageFileIfUnreferenced(connection, imageFile);
        }
    }

    private static void DeleteImageFileIfUnreferenced(SqliteConnection connection, string imageFile)
    {
        // A file is named by content hash, so duplicates share it: delete from
        // disk only when the last row referencing it is gone.
        using SqliteCommand stillUsed = new("SELECT COUNT(*) FROM ClipboardHistory WHERE ImageFile = @file", connection);
        _ = stillUsed.Parameters.AddWithValue("@file", imageFile);
        long references = Convert.ToInt64(stillUsed.ExecuteScalar());

        if (references == 0)
        {
            try
            {
                string path = Path.Combine(HistoryDirectory, imageFile);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not delete history image {imageFile}: {ex.Message}");
            }
        }
    }

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
