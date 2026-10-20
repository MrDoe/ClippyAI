using Apache.Arrow;
using Apache.Arrow.Types;
using ClippyAI.Models;
using lancedb;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Table = lancedb.Table;

namespace ClippyAI.Services;

/// <summary>
/// Embedded vector store for the response-template cache.
///
/// Uses LanceDB — an in-process, file-based vector database — so ClippyAI no
/// longer needs a PostgreSQL server, the pgai extension, or network access to
/// store embeddings. One table is kept per provider/model pair because vectors
/// from different models are not comparable and may have different dimensions.
/// </summary>
public static class EmbeddingStore
{
    private const string TablePrefix = "clippy_";

    /// <summary>Serializes check-then-insert so two turns cannot store duplicates.</summary>
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    /// <summary>Seed for row ids; ticks are unique across restarts in practice.</summary>
    private static long _lastId = DateTime.UtcNow.Ticks;

    /// <summary>
    /// Directory holding the LanceDB database. Configurable with
    /// <c>VectorDbPath</c>; defaults to the app data folder next to the config DB.
    /// </summary>
    public static string DatabasePath
    {
        get
        {
            string configured = ConfigurationService.GetConfigurationValue("VectorDbPath", "");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(ExpandHome(configured.Trim()));
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClippyAI",
                "lancedb");
        }
    }

    /// <summary>
    /// Table name for a provider/model pair, normalized to the characters
    /// LanceDB/object stores accept.
    /// </summary>
    public static string BuildTableName(string provider, string model)
    {
        string raw = $"{provider}_{model}".ToLowerInvariant();
        System.Text.StringBuilder builder = new(raw.Length + TablePrefix.Length);
        bool lastWasSeparator = false;

        foreach (char c in raw)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(c);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }

        string suffix = builder.ToString().Trim('_');
        return TablePrefix + (suffix.Length == 0 ? "default" : suffix);
    }

    /// <summary>
    /// Creates the database directory and verifies the native LanceDB library is
    /// usable. Called on startup so a broken install surfaces immediately.
    /// </summary>
    public static void EnsureInitialized()
    {
        try
        {
            string path = DatabasePath;
            _ = Directory.CreateDirectory(path);

            using Connection connection = new();
            connection.Connect(path).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not open the local vector database at '{DatabasePath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Stores a template unless a similar embedding already exists for the task.
    /// </summary>
    /// <param name="tableName">Table for the current provider/model pair.</param>
    /// <param name="task">The task the answer belongs to.</param>
    /// <param name="clipboardData">The original clipboard text.</param>
    /// <param name="answer">The answer to cache.</param>
    /// <param name="vector">Embedding of <paramref name="clipboardData"/>.</param>
    /// <param name="duplicateDistance">Maximum cosine distance treated as a duplicate.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns><c>true</c> when the row was inserted, <c>false</c> for a duplicate.</returns>
    public static async Task<bool> StoreAsync(string tableName, string task, string clipboardData,
                                              string answer, float[] vector, float duplicateDistance,
                                              CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        _ = Directory.CreateDirectory(DatabasePath);

        await WriteLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            using Connection connection = new();
            await connection.Connect(DatabasePath).ConfigureAwait(false);

            string[] tables = [.. await connection.TableNames().ConfigureAwait(false)];

            if (tables.Contains(tableName, StringComparer.Ordinal))
            {
                using Table table = await connection.OpenTable(tableName).ConfigureAwait(false);
                int dimension = await GetVectorDimensionAsync(table).ConfigureAwait(false);

                if (dimension != vector.Length)
                {
                    // Same model name, different dimension (e.g. a model update).
                    // Old vectors cannot be compared with new ones, so rebuild.
                    Console.WriteLine(
                        $"Embedding dimension changed for '{tableName}' ({dimension} -> {vector.Length}); " +
                        "rebuilding the table.");
                    table.Dispose();
                    await connection.DropTable(tableName).ConfigureAwait(false);

                    using Table recreated = await connection
                        .CreateTable(tableName, BuildBatch(NextId(), task, clipboardData, answer, vector))
                        .ConfigureAwait(false);
                    return true;
                }

                float? nearest = await NearestDistanceAsync(table, task, vector).ConfigureAwait(false);
                if (nearest.HasValue && nearest.Value <= duplicateDistance)
                {
                    return false;
                }

                _ = await table
                    .Add(BuildBatch(NextId(), task, clipboardData, answer, vector))
                    .ConfigureAwait(false);
                return true;
            }

            using Table created = await connection
                .CreateTable(tableName, BuildBatch(NextId(), task, clipboardData, answer, vector))
                .ConfigureAwait(false);
            return true;
        }
        finally
        {
            _ = WriteLock.Release();
        }
    }

    /// <summary>
    /// Finds the answers whose embeddings are within <paramref name="maxDistance"/>
    /// of <paramref name="vector"/>, closest first.
    /// </summary>
    public static async Task<List<Embedding>> SearchAsync(string tableName, string task, float[] vector,
                                                          float maxDistance, int limit = 10,
                                                          CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        List<Embedding> results = [];

        using Connection connection = new();
        await connection.Connect(DatabasePath).ConfigureAwait(false);

        string[] tables = [.. await connection.TableNames().ConfigureAwait(false)];
        if (!tables.Contains(tableName, StringComparer.Ordinal))
        {
            return results;
        }

        using Table table = await connection.OpenTable(tableName).ConfigureAwait(false);

        using VectorQuery query = table.Query()
            .NearestTo(vector)
            .DistanceType(DistanceType.Cosine)
            .Where(TaskPredicate(task))
            .Limit(limit);

        IReadOnlyList<Dictionary<string, object?>> rows = await query.ToList().ConfigureAwait(false);

        foreach (Dictionary<string, object?> row in rows)
        {
            token.ThrowIfCancellationRequested();

            float distance = ReadDistance(row);
            if (distance > maxDistance)
            {
                continue;
            }

            results.Add(new Embedding
            {
                Id = ReadInt64(row, "id"),
                Answer = ReadString(row, "answer"),
                Distance = distance
            });
        }

        return results;
    }

    /// <summary>Total number of stored templates across all model tables.</summary>
    public static async Task<long> CountAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        long total = 0;

        using Connection connection = new();
        await connection.Connect(DatabasePath).ConfigureAwait(false);

        foreach (string name in await connection.TableNames().ConfigureAwait(false))
        {
            if (!name.StartsWith(TablePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using Table table = await connection.OpenTable(name).ConfigureAwait(false);
            total += await table.CountRows().ConfigureAwait(false);
        }

        return total;
    }

    /// <summary>Removes every stored template (all model tables).</summary>
    public static async Task ClearAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        using Connection connection = new();
        await connection.Connect(DatabasePath).ConfigureAwait(false);

        foreach (string name in await connection.TableNames().ConfigureAwait(false))
        {
            if (name.StartsWith(TablePrefix, StringComparison.Ordinal))
            {
                await connection.DropTable(name).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Deletes one stored template by id.</summary>
    public static async Task<bool> DeleteAsync(long id, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        using Connection connection = new();
        await connection.Connect(DatabasePath).ConfigureAwait(false);

        foreach (string name in await connection.TableNames().ConfigureAwait(false))
        {
            if (!name.StartsWith(TablePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using Table table = await connection.OpenTable(name).ConfigureAwait(false);
            string predicate = $"id = {id.ToString(CultureInfo.InvariantCulture)}";

            if (await table.CountRows(predicate).ConfigureAwait(false) == 0)
            {
                continue;
            }

            _ = await table.Delete(predicate).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static long NextId() => Interlocked.Increment(ref _lastId);

    /// <summary>Expands a leading <c>~</c> to the user's home directory.</summary>
    private static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) ||
            path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..]);
        }

        return path;
    }

    private static async Task<int> GetVectorDimensionAsync(Table table)
    {
        Schema schema = await table.Schema().ConfigureAwait(false);
        Field? field = schema.FieldsList.FirstOrDefault(f => f.Name == "vector");

        return field?.DataType is FixedSizeListType list ? list.ListSize : -1;
    }

    private static async Task<float?> NearestDistanceAsync(Table table, string task, float[] vector)
    {
        using VectorQuery query = table.Query()
            .NearestTo(vector)
            .DistanceType(DistanceType.Cosine)
            .Where(TaskPredicate(task))
            .Limit(1);

        IReadOnlyList<Dictionary<string, object?>> rows = await query.ToList().ConfigureAwait(false);
        return rows.Count > 0 ? ReadDistance(rows[0]) : null;
    }

    private static RecordBatch BuildBatch(long id, string task, string clipboardData,
                                         string answer, float[] vector)
    {
        Field valueField = new("item", FloatType.Default, nullable: false);
        FixedSizeListArray.Builder vectorBuilder = new(valueField, vector.Length);
        FloatArray.Builder valueBuilder = (FloatArray.Builder)vectorBuilder.ValueBuilder;

        vectorBuilder.Append();
        foreach (float value in vector)
        {
            valueBuilder.Append(value);
        }

        FixedSizeListType vectorType = new(valueField, vector.Length);

        Schema schema = new Schema.Builder()
            .Field(new Field("id", Int64Type.Default, nullable: false))
            .Field(new Field("task", StringType.Default, nullable: false))
            .Field(new Field("clipboard_data", StringType.Default, nullable: false))
            .Field(new Field("answer", StringType.Default, nullable: false))
            .Field(new Field("vector", vectorType, nullable: false))
            .Build();

        return new RecordBatch(
            schema,
            new IArrowArray[]
            {
                new Int64Array.Builder().Append(id).Build(),
                new StringArray.Builder().Append(task).Build(),
                new StringArray.Builder().Append(clipboardData).Build(),
                new StringArray.Builder().Append(answer).Build(),
                vectorBuilder.Build()
            },
            1);
    }

    /// <summary>SQL literal for the task column (single quotes doubled).</summary>
    private static string TaskPredicate(string task) =>
        "task = '" + task.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static float ReadDistance(Dictionary<string, object?> row)
    {
        if (row.TryGetValue("_distance", out object? value) && value is not null)
        {
            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        return float.PositiveInfinity;
    }

    private static long ReadInt64(Dictionary<string, object?> row, string column) =>
        row.TryGetValue(column, out object? value) && value is not null
            ? Convert.ToInt64(value, CultureInfo.InvariantCulture)
            : 0L;

    private static string ReadString(Dictionary<string, object?> row, string column) =>
        row.TryGetValue(column, out object? value) ? value?.ToString() ?? "" : "";
}
