using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClippyAI.Services;

/// <summary>
/// Optional background-knowledge retrieval from the team's ClickHouse server
/// (the OpenCodeRAG index: code/doc chunks plus shared quirks). Before a task
/// is sent to the LLM, <see cref="GetContextAsync"/> queries the configured
/// chunk tables with the task input and returns a compact, formatted context
/// block that gets appended to the task's system prompt.
///
/// Retrieval modes (ClickHouseMode):
///   auto    - vector search when the embedding model matches the stored
///             dimension, otherwise keyword search (default)
///   vector  - cosineDistance() over Array(Float32) embeddings
///   keyword - ILIKE term matching (works without any embedding setup)
///
/// Configuration keys (Configuration table / settings dialog, ClickHouse tab):
///   ClickHouseEnabled (False), ClickHouseUrl, ClickHouseUser,
///   ClickHousePassword, ClickHouseDatabase (OpenCodeRAG),
///   ClickHouseTables (empty = all chunks_* tables), ClickHouseTopK (5),
///   ClickHouseMaxChars (2500), ClickHouseMode (auto)
///
/// All failures are non-fatal: the knowledge block is simply omitted. A short
/// circuit breaker stops hammering an unreachable server.
/// </summary>
public static class ClickHouseKnowledgeService
{
    private static readonly HttpClient Http = new(new HttpClientHandler { UseProxy = false })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static DateTime _circuitOpenUntilUtc = DateTime.MinValue;
    private static string[]? _cachedTables;
    private static DateTime _cachedTablesUntilUtc = DateTime.MinValue;
    private static int _cachedEmbeddingDim = -1;

    private const int QuerySnippetChars = 700;

    // Junk tables from OpenCodeRAG self-tests (chunks_opencode_rag_enabled_*,
    // doc_reload, ...) would flood retrieval with noise.
    private static bool IsInterestingTable(string name) =>
        name.StartsWith("chunks_", StringComparison.OrdinalIgnoreCase)
        && !name.Contains("opencode_rag_", StringComparison.OrdinalIgnoreCase);

    public static bool Enabled =>
        ConfigurationService.GetConfigurationValue("ClickHouseEnabled", "False")
            .Equals("True", StringComparison.OrdinalIgnoreCase);

    public static int TopK =>
        int.TryParse(ConfigurationService.GetConfigurationValue("ClickHouseTopK", "5"), out int n) ? Math.Clamp(n, 1, 20) : 5;

    public static int MaxContextChars =>
        int.TryParse(ConfigurationService.GetConfigurationValue("ClickHouseMaxChars", "2500"), out int n) ? Math.Clamp(n, 200, 20000) : 2500;

    private static string Url => ConfigurationService.GetConfigurationValue("ClickHouseUrl", "http://161.42.232.20:8123/");
    private static string User => ConfigurationService.GetConfigurationValue("ClickHouseUser", "starlims");
    private static string Password => ConfigurationService.GetConfigurationValue("ClickHousePassword", "");
    private static string Database => ConfigurationService.GetConfigurationValue("ClickHouseDatabase", "OpenCodeRAG");
    private static string Mode => ConfigurationService.GetConfigurationValue("ClickHouseMode", "auto").Trim().ToLowerInvariant();

    /// <summary>
    /// Returns a formatted knowledge block for the given query, or null when
    /// disabled, unreachable, or nothing relevant was found.
    /// </summary>
    public static async Task<string?> GetContextAsync(string query, CancellationToken token = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        if (DateTime.UtcNow < _circuitOpenUntilUtc)
        {
            return null;
        }

        try
        {
            string trimmedQuery = query.Length > 4000 ? query.Substring(0, 4000) : query;

            List<string> tables = await ResolveTablesAsync(token);
            if (tables.Count == 0)
            {
                return null;
            }

            List<Hit> hits;
            string mode = Mode;

            if (mode == "keyword")
            {
                hits = await SearchKeywordAsync(tables, trimmedQuery, token);
            }
            else
            {
                hits = await SearchVectorAsync(tables, trimmedQuery, token);

                if (hits.Count == 0 && mode != "vector")
                {
                    // auto: fall back to keyword search (no embedding-model match,
                    // embedding provider down, empty vector result, ...)
                    hits = await SearchKeywordAsync(tables, trimmedQuery, token);
                }
            }

            if (hits.Count == 0)
            {
                return null;
            }

            return Format(hits);
        }
        catch (Exception ex)
        {
            // Never let knowledge retrieval break the actual task; back off
            // from a failing server for two minutes.
            System.Diagnostics.Debug.WriteLine($"ClickHouse knowledge retrieval failed: {ex.Message}");
            _circuitOpenUntilUtc = DateTime.UtcNow.AddMinutes(2);
            return null;
        }
    }

    /// <summary>SELECT version() - used by the settings dialog's test button.</summary>
    public static async Task<string> TestConnectionAsync(string? urlOverride = null, string? userOverride = null,
                                                         string? passwordOverride = null, CancellationToken token = default)
    {
        string result = await SendTextAsync("SELECT version() FORMAT TSV", token, urlOverride, userOverride, passwordOverride);
        _circuitOpenUntilUtc = DateTime.MinValue;
        return result.Trim();
    }

    /// <summary>Lists interesting chunk tables (bypassing the cache).</summary>
    public static async Task<List<string>> DiscoverTablesAsync(string? urlOverride = null, string? userOverride = null,
                                                               string? passwordOverride = null, CancellationToken token = default)
    {
        string result = await SendTextAsync($"SHOW TABLES FROM {SanitizeIdent(Database)} FORMAT TSV", token,
                                            urlOverride, userOverride, passwordOverride);
        return result.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                     .Select(t => t.Trim())
                     .Where(IsInterestingTable)
                     .OrderBy(t => t)
                     .ToList();
    }

    private static async Task<List<string>> ResolveTablesAsync(CancellationToken token)
    {
        string configured = ConfigurationService.GetConfigurationValue("ClickHouseTables", "");
        string[] explicitTables = configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (explicitTables.Length > 0)
        {
            return explicitTables.Where(IsInterestingTable).ToList();
        }

        if (_cachedTables == null || DateTime.UtcNow > _cachedTablesUntilUtc)
        {
            _cachedTables = (await DiscoverTablesAsync(token: token)).ToArray();
            _cachedTablesUntilUtc = DateTime.UtcNow.AddMinutes(10);
        }

        return _cachedTables.ToList();
    }

    // ---------------- retrieval ----------------

    private sealed record Hit(string Table, string FilePath, string Kind, string Snippet, double Score);

    private static async Task<List<Hit>> SearchVectorAsync(List<string> tables, string query, CancellationToken token)
    {
        float[] vector;
        try
        {
            vector = await EmbeddingService.EmbedAsync(query, EmbeddingPurpose.Query);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ClickHouse vector mode: embedding failed: {ex.Message}");
            return [];
        }

        if (vector.Length == 0)
        {
            return [];
        }

        int storedDim = await GetStoredDimensionAsync(tables[0], token);
        if (storedDim <= 0 || storedDim != vector.Length)
        {
            if (Mode == "vector")
            {
                System.Diagnostics.Debug.WriteLine($"ClickHouse: stored dim {storedDim} != embedding dim {vector.Length}");
            }

            return [];
        }

        string literal = "[" + string.Join(",", vector.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";
        List<Hit> hits = [];

        foreach (string table in tables)
        {
            string sql = $@"
                SELECT file_path, kind, left(content, {QuerySnippetChars}) AS snippet,
                       1 - cosineDistance(embedding, {literal}) AS score
                FROM {SanitizeIdent(Database)}.{SanitizeIdent(table)}
                WHERE is_deleted = 0
                ORDER BY cosineDistance(embedding, {literal}) ASC
                LIMIT {TopK}
                FORMAT JSONEachRow";

            string body = await SendTextAsync(sql, token);
            foreach (Hit hit in ParseHits(body, table))
            {
                hits.Add(hit);
            }
        }

        return hits.OrderByDescending(h => h.Score).Take(TopK).ToList();
    }

    private static async Task<List<Hit>> SearchKeywordAsync(List<string> tables, string query, CancellationToken token)
    {
        string[] terms = query
            .Split([' ', '\t', '\n', '\r', ',', '.', ';', ':', '(', ')', '[', ']', '{', '}', '"', '\'', '=', '<', '>', '|', '/', '\\', '+', '-', '*', '#', '_', '?', '!', '%', '&'],
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && char.IsLetter(w[0]))
            .GroupBy(w => w.ToLowerInvariant())
            .Select(g => g.First())
            .OrderByDescending(w => w.Length)
            .Take(6)
            .ToArray();

        if (terms.Length == 0)
        {
            return [];
        }

        string where = string.Join(" OR ", terms.Select(t => $"content ILIKE '%{EscapeSql(t)}%'"));
        List<Hit> hits = [];

        foreach (string table in tables)
        {
            string sql = $@"
                SELECT file_path, kind, left(content, {QuerySnippetChars}) AS snippet
                FROM {SanitizeIdent(Database)}.{SanitizeIdent(table)}
                WHERE is_deleted = 0 AND ({where})
                LIMIT {TopK}
                FORMAT JSONEachRow";

            string body = await SendTextAsync(sql, token);
            foreach (Hit hit in ParseHits(body, table))
            {
                hits.Add(hit);
            }
        }

        return hits.Take(TopK).ToList();
    }

    private static async Task<int> GetStoredDimensionAsync(string table, CancellationToken token)
    {
        if (_cachedEmbeddingDim > 0)
        {
            return _cachedEmbeddingDim;
        }

        try
        {
            string body = await SendTextAsync(
                $"SELECT length(embedding) FROM {SanitizeIdent(Database)}.{SanitizeIdent(table)} WHERE is_deleted = 0 LIMIT 1 FORMAT TSV", token);
            if (int.TryParse(body.Trim(), out int dim))
            {
                _cachedEmbeddingDim = dim;
            }
        }
        catch
        {
            // dimension probe failures just disable vector mode
        }

        return _cachedEmbeddingDim;
    }

    private static List<Hit> ParseHits(string jsonLines, string table)
    {
        List<Hit> hits = [];

        foreach (string line in jsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(line);
                JsonElement root = doc.RootElement;
                hits.Add(new Hit(
                    table,
                    root.TryGetProperty("file_path", out JsonElement fp) ? fp.GetString() ?? "" : "",
                    root.TryGetProperty("kind", out JsonElement k) ? k.GetString() ?? "" : "",
                    root.TryGetProperty("snippet", out JsonElement s) ? s.GetString() ?? "" : "",
                    root.TryGetProperty("score", out JsonElement sc) && sc.TryGetDouble(out double d) ? d : 0.0));
            }
            catch (JsonException)
            {
                // skip unparsable line
            }
        }

        return hits;
    }

    private static string Format(List<Hit> hits)
    {
        StringBuilder sb = new();
        sb.AppendLine("## Background knowledge (shared project RAG database, ClickHouse)");
        sb.AppendLine("The following authentic excerpts come from the indexed project sources and shared quirks.");
        sb.AppendLine("Use them as factual background; prefer them over guesswork. Do not quote them verbatim into the answer.");
        sb.AppendLine();

        int budget = MaxContextChars;
        foreach (Hit hit in hits)
        {
            string kindLabel = hit.Kind == "quirk" ? "quirk/gotcha" : hit.Kind.Length == 0 ? "source" : hit.Kind;
            string line = $"- [{kindLabel} @ {hit.Table} :: {hit.FilePath}] {Collapse(hit.Snippet)}";

            if (line.Length > budget)
            {
                break;
            }

            sb.AppendLine(line);
            budget -= line.Length + 1;
        }

        return sb.ToString().TrimEnd();
    }

    private static string Collapse(string text)
    {
        string one = text.Replace("\r", " ").Replace("\n", " ");
        while (one.Contains("  "))
        {
            one = one.Replace("  ", " ");
        }

        return one.Trim();
    }

    // ---------------- HTTP plumbing ----------------

    private static async Task<string> SendTextAsync(string sql, CancellationToken token,
                                                    string? urlOverride = null, string? userOverride = null,
                                                    string? passwordOverride = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, string.IsNullOrWhiteSpace(urlOverride) ? Url : urlOverride);
        string user = userOverride ?? User;
        string password = passwordOverride ?? Password;
        request.Headers.TryAddWithoutValidation("Authorization",
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
        request.Content = new StringContent(sql, Encoding.UTF8, "text/plain");

        using HttpResponseMessage response = await Http.SendAsync(request, token);
        string body = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"ClickHouse HTTP {(int)response.StatusCode}: {Truncate(body, 300)}");
        }

        return body;
    }

    private static string SanitizeIdent(string name)
    {
        // Identifiers come from configuration or SHOW TABLES; keep it strict.
        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
            {
                throw new InvalidOperationException($"Invalid ClickHouse identifier: {name}");
            }
        }

        return name;
    }

    private static string EscapeSql(string term) => term.Replace("\\", "\\\\").Replace("'", "\\'");

    private static string Truncate(string text, int max) => text.Length <= max ? text : text.Substring(0, max);
}
