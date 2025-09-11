using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using ClippyAI.Models;
using System.Configuration;

namespace ClippyAI.Services;

public static class ConfigurationService
{
    private static readonly string ConnectionString = GetConnectionString();
    
    private static string GetConnectionString()
    {
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                  "ClippyAI", "clippyai_config.db");
        var directory = Path.GetDirectoryName(dbPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }
        return $"Data Source={dbPath}";
    }

    public static void InitializeDatabase()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        // Create Configuration table for general app settings
        var createConfigTable = @"
            CREATE TABLE IF NOT EXISTS Configuration (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Key TEXT NOT NULL UNIQUE,
                Value TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )";

        // Create TaskConfigurations table
        var createTaskConfigTable = @"
            CREATE TABLE IF NOT EXISTS TaskConfigurations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TaskName TEXT NOT NULL UNIQUE,
                SystemPrompt TEXT NOT NULL,
                Model TEXT NOT NULL,
                Temperature REAL NOT NULL DEFAULT 0.8,
                MaxLength INTEGER NOT NULL DEFAULT 2048,
                TopP REAL NOT NULL DEFAULT 0.9,
                TopK INTEGER NOT NULL DEFAULT 40,
                RepeatPenalty REAL NOT NULL DEFAULT 1.1,
                NumCtx INTEGER NOT NULL DEFAULT 2048,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )";

        // Create JobConfigurations table
        var createJobConfigTable = @"
            CREATE TABLE IF NOT EXISTS JobConfigurations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                JobName TEXT NOT NULL UNIQUE,
                Description TEXT NOT NULL,
                TaskConfigurationId INTEGER NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (TaskConfigurationId) REFERENCES TaskConfigurations (Id)
            )";

        using var command0 = new SqliteCommand(createConfigTable, connection);
        command0.ExecuteNonQuery();

        using var command1 = new SqliteCommand(createTaskConfigTable, connection);
        command1.ExecuteNonQuery();

        using var command2 = new SqliteCommand(createJobConfigTable, connection);
        command2.ExecuteNonQuery();

        // Migrate App.config settings to database if not already done
        MigrateAppConfigToDatabase(connection);

        // Insert default task configurations if none exist
        InsertDefaultTaskConfigurations(connection);
        
        // Migrate legacy tasks
        connection.Close();
        MigrateLegacyTasks();
    }

    private static void InsertDefaultTaskConfigurations(SqliteConnection connection)
    {
        var countCommand = new SqliteCommand("SELECT COUNT(*) FROM TaskConfigurations", connection);
        var count = Convert.ToInt32(countCommand.ExecuteScalar());

        if (count == 0)
        {
            // Get current system prompt from App.config
            var systemPrompt = ConfigurationManager.AppSettings["System"] ?? 
                "You are an expert assistant that provides detailed responses to tasks.";

            var defaultConfigs = new[]
            {
                new TaskConfiguration 
                { 
                    TaskName = "Default Email Response", 
                    SystemPrompt = systemPrompt,
                    Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "gemma2:latest",
                    Temperature = 0.8,
                    MaxLength = 2048
                },
                new TaskConfiguration 
                { 
                    TaskName = "Creative Writing", 
                    SystemPrompt = "You are a creative writing assistant. Help with creative and imaginative content.",
                    Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "gemma2:latest",
                    Temperature = 1.2,
                    MaxLength = 4096
                },
                new TaskConfiguration 
                { 
                    TaskName = "Technical Analysis", 
                    SystemPrompt = "You are a technical expert. Provide precise, factual, and analytical responses.",
                    Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "gemma2:latest",
                    Temperature = 0.3,
                    MaxLength = 2048
                }
            };

            foreach (var config in defaultConfigs)
            {
                SaveTaskConfiguration(config, connection);
            }
        }
    }

    public static List<TaskConfiguration> GetAllTaskConfigurations()
    {
        var configurations = new List<TaskConfiguration>();

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var selectCommand = @"
            SELECT Id, TaskName, SystemPrompt, Model, Temperature, MaxLength, 
                   TopP, TopK, RepeatPenalty, NumCtx, IsActive, CreatedAt, UpdatedAt
            FROM TaskConfigurations 
            WHERE IsActive = 1
            ORDER BY TaskName";

        using var command = new SqliteCommand(selectCommand, connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            configurations.Add(new TaskConfiguration
            {
                Id = reader.GetInt32(0),
                TaskName = reader.GetString(1),
                SystemPrompt = reader.GetString(2),
                Model = reader.GetString(3),
                Temperature = reader.GetDouble(4),
                MaxLength = reader.GetInt32(5),
                TopP = reader.GetDouble(6),
                TopK = reader.GetInt32(7),
                RepeatPenalty = reader.GetDouble(8),
                NumCtx = reader.GetInt32(9),
                IsActive = reader.GetInt32(10) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(11)),
                UpdatedAt = DateTime.Parse(reader.GetString(12))
            });
        }

        return configurations;
    }

    public static TaskConfiguration? GetTaskConfiguration(string taskName)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var selectCommand = @"
            SELECT Id, TaskName, SystemPrompt, Model, Temperature, MaxLength, 
                   TopP, TopK, RepeatPenalty, NumCtx, IsActive, CreatedAt, UpdatedAt
            FROM TaskConfigurations 
            WHERE TaskName = @taskName AND IsActive = 1";

        using var command = new SqliteCommand(selectCommand, connection);
        command.Parameters.AddWithValue("@taskName", taskName);

        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            return new TaskConfiguration
            {
                Id = reader.GetInt32(0),
                TaskName = reader.GetString(1),
                SystemPrompt = reader.GetString(2),
                Model = reader.GetString(3),
                Temperature = reader.GetDouble(4),
                MaxLength = reader.GetInt32(5),
                TopP = reader.GetDouble(6),
                TopK = reader.GetInt32(7),
                RepeatPenalty = reader.GetDouble(8),
                NumCtx = reader.GetInt32(9),
                IsActive = reader.GetInt32(10) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(11)),
                UpdatedAt = DateTime.Parse(reader.GetString(12))
            };
        }

        return null;
    }

    public static void SaveTaskConfiguration(TaskConfiguration config, SqliteConnection? connection = null)
    {
        var shouldCloseConnection = connection == null;
        connection ??= new SqliteConnection(ConnectionString);
        
        if (shouldCloseConnection)
            connection.Open();

        try
        {
            var insertCommand = @"
                INSERT OR REPLACE INTO TaskConfigurations 
                (TaskName, SystemPrompt, Model, Temperature, MaxLength, TopP, TopK, 
                 RepeatPenalty, NumCtx, IsActive, CreatedAt, UpdatedAt)
                VALUES (@taskName, @systemPrompt, @model, @temperature, @maxLength, 
                        @topP, @topK, @repeatPenalty, @numCtx, @isActive, @createdAt, @updatedAt)";

            using var command = new SqliteCommand(insertCommand, connection);
            command.Parameters.AddWithValue("@taskName", config.TaskName);
            command.Parameters.AddWithValue("@systemPrompt", config.SystemPrompt);
            command.Parameters.AddWithValue("@model", config.Model);
            command.Parameters.AddWithValue("@temperature", config.Temperature);
            command.Parameters.AddWithValue("@maxLength", config.MaxLength);
            command.Parameters.AddWithValue("@topP", config.TopP);
            command.Parameters.AddWithValue("@topK", config.TopK);
            command.Parameters.AddWithValue("@repeatPenalty", config.RepeatPenalty);
            command.Parameters.AddWithValue("@numCtx", config.NumCtx);
            command.Parameters.AddWithValue("@isActive", config.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@createdAt", config.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            command.ExecuteNonQuery();
        }
        finally
        {
            if (shouldCloseConnection)
                connection.Close();
        }
    }

    public static void DeleteTaskConfiguration(string taskName)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var deleteCommand = "UPDATE TaskConfigurations SET IsActive = 0 WHERE TaskName = @taskName";
        using var command = new SqliteCommand(deleteCommand, connection);
        command.Parameters.AddWithValue("@taskName", taskName);
        command.ExecuteNonQuery();
    }

    public static void MigrateLegacyTasks()
    {
        try
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            // Check if legacy tasks have already been migrated
            var checkCommand = new SqliteCommand("SELECT COUNT(*) FROM TaskConfigurations WHERE TaskName LIKE 'Legacy_%'", connection);
            var existingLegacyCount = Convert.ToInt32(checkCommand.ExecuteScalar());

            if (existingLegacyCount > 0)
            {
                // Legacy tasks already migrated
                return;
            }

            // Get legacy task resources from Resources
            var resourceType = typeof(Resources.Resources);
            var defaultModel = ConfigurationManager.AppSettings["OllamaModel"] ?? "gemma2:latest";
            
            var legacyTasks = new List<TaskConfiguration>();

            // Map legacy tasks to their appropriate categories and settings
            var legacyTaskMappings = new Dictionary<string, (string category, double temperature, int maxLength)>
            {
                ["Task_1"] = ("Email Response", 0.8, 2048),
                ["Task_2"] = ("Email Response", 0.7, 3072),
                ["Task_3"] = ("Email Response", 0.8, 2048),
                ["Task_4"] = ("Email Response", 0.8, 2048),
                ["Task_5"] = ("Email Response", 0.8, 2048),
                ["Task_6"] = ("Email Response", 0.8, 2048),
                ["Task_7"] = ("Email Response", 0.8, 2048),
                ["Task_8"] = ("Email Response", 0.8, 2048),
                ["Task_9"] = ("Email Response", 0.8, 2048),
                ["Task_10"] = ("Email Response", 0.8, 2048),
                ["Task_11"] = ("Email Response", 0.8, 2048),
                ["Task_12"] = ("Analysis", 0.5, 2048),
                ["Task_13"] = ("Technical Analysis", 0.3, 2048),
                ["Task_14"] = ("Analysis", 0.6, 2048),
                ["Task_15"] = ("Custom", 0.8, 2048),
                ["Task_16"] = ("Translation", 0.3, 1024),
                ["Task_17"] = ("Translation", 0.3, 1024),
                ["Task_18"] = ("Translation", 0.3, 1024),
                ["Task_19"] = ("Translation", 0.3, 1024),
                ["Task_20"] = ("Email Response", 0.8, 2048),
                ["Task_21"] = ("Email Response", 0.8, 2048),
                ["Task_22"] = ("Summary", 0.5, 1024)
            };

            foreach (var taskMapping in legacyTaskMappings)
            {
                var property = resourceType.GetProperty(taskMapping.Key);
                if (property != null)
                {
                    var taskPrompt = property.GetValue(null)?.ToString();
                    if (!string.IsNullOrEmpty(taskPrompt))
                    {
                        var (category, temperature, maxLength) = taskMapping.Value;
                        var legacyTask = new TaskConfiguration
                        {
                            TaskName = $"Legacy_{category}_{taskMapping.Key}",
                            SystemPrompt = $"You are an expert assistant specialized in {category.ToLower()}. {GetSystemPromptForCategory(category)}",
                            Model = defaultModel,
                            Temperature = temperature,
                            MaxLength = maxLength,
                            TopP = 0.9,
                            TopK = 40,
                            RepeatPenalty = 1.1,
                            NumCtx = 2048,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        legacyTasks.Add(legacyTask);
                    }
                }
            }

            // Save all legacy tasks
            foreach (var task in legacyTasks)
            {
                SaveTaskConfiguration(task, connection);
            }

            System.Diagnostics.Debug.WriteLine($"Migrated {legacyTasks.Count} legacy tasks to TaskConfiguration system.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error migrating legacy tasks: {ex.Message}");
        }
    }

    private static void MigrateAppConfigToDatabase(SqliteConnection connection)
    {
        try
        {
            // Check if migration has already been done
            var checkCommand = new SqliteCommand("SELECT COUNT(*) FROM Configuration", connection);
            var existingCount = Convert.ToInt32(checkCommand.ExecuteScalar());

            if (existingCount > 0)
            {
                // Configuration already migrated
                return;
            }

            // Get all App.config settings and migrate them to database
            var appSettings = ConfigurationManager.AppSettings;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (string key in appSettings.AllKeys)
            {
                var value = appSettings[key] ?? string.Empty;
                
                var insertCommand = @"
                    INSERT INTO Configuration (Key, Value, CreatedAt, UpdatedAt)
                    VALUES (@key, @value, @createdAt, @updatedAt)";

                using var command = new SqliteCommand(insertCommand, connection);
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@value", value);
                command.Parameters.AddWithValue("@createdAt", timestamp);
                command.Parameters.AddWithValue("@updatedAt", timestamp);
                command.ExecuteNonQuery();
            }

            System.Diagnostics.Debug.WriteLine($"Migrated {appSettings.AllKeys.Length} configuration settings to database.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error migrating App.config to database: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a configuration value from the database. Falls back to App.config if not found.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key is not found</param>
    /// <returns>Configuration value or default</returns>
    public static string GetConfigurationValue(string key, string defaultValue = "")
    {
        try
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var selectCommand = "SELECT Value FROM Configuration WHERE Key = @key";
            using var command = new SqliteCommand(selectCommand, connection);
            command.Parameters.AddWithValue("@key", key);

            var result = command.ExecuteScalar()?.ToString();
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }

            // Fallback to App.config if not found in database
            return ConfigurationManager.AppSettings[key] ?? defaultValue;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting configuration value for key '{key}': {ex.Message}");
            // Fallback to App.config
            return ConfigurationManager.AppSettings[key] ?? defaultValue;
        }
    }

    /// <summary>
    /// Sets a configuration value in the database.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Configuration value</param>
    public static void SetConfigurationValue(string key, string value)
    {
        try
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var upsertCommand = @"
                INSERT OR REPLACE INTO Configuration (Key, Value, CreatedAt, UpdatedAt)
                VALUES (@key, @value, 
                    COALESCE((SELECT CreatedAt FROM Configuration WHERE Key = @key), @timestamp),
                    @timestamp)";

            using var command = new SqliteCommand(upsertCommand, connection);
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value);
            command.Parameters.AddWithValue("@timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting configuration value for key '{key}': {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all configuration values from the database.
    /// </summary>
    /// <returns>Dictionary of configuration key-value pairs</returns>
    public static Dictionary<string, string> GetAllConfigurationValues()
    {
        var configurations = new Dictionary<string, string>();

        try
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var selectCommand = "SELECT Key, Value FROM Configuration ORDER BY Key";
            using var command = new SqliteCommand(selectCommand, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                configurations[reader.GetString(0)] = reader.GetString(1);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting all configuration values: {ex.Message}");
        }

        return configurations;
    }

    private static string GetSystemPromptForCategory(string category)
    {
        return category switch
        {
            "Email Response" => "Provide professional and courteous email responses that are contextually appropriate.",
            "Technical Analysis" => "Provide precise, factual, and analytical responses with technical accuracy.",
            "Analysis" => "Analyze the content thoroughly and provide detailed, objective insights.",
            "Translation" => "Provide accurate translations while maintaining the original meaning and tone.",
            "Summary" => "Create concise, well-structured summaries that capture the key points.",
            "Custom" => "Provide helpful responses tailored to the specific request.",
            _ => "Provide detailed and helpful responses to the given task."
        };
    }
}