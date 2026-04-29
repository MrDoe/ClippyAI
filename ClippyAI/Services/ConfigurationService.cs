using ClippyAI.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Reflection;

namespace ClippyAI.Services;

public static class ConfigurationService
{
    private static readonly string ConnectionString = GetConnectionString();

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

    public static void InitializeDatabase()
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        // Create Configuration table for general app settings
        string createConfigTable = @"
            CREATE TABLE IF NOT EXISTS Configuration (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Key TEXT NOT NULL UNIQUE,
                Value TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )";

        // Create TaskConfigurations table
        string createTaskConfigTable = @"
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
                IsImageTask INTEGER NOT NULL DEFAULT 0,
                ImageSource TEXT NOT NULL DEFAULT 'Clipboard',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )";

        // Create JobConfigurations table
        string createJobConfigTable = @"
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

        using SqliteCommand command0 = new(createConfigTable, connection);
        _ = command0.ExecuteNonQuery();

        using SqliteCommand command1 = new(createTaskConfigTable, connection);
        _ = command1.ExecuteNonQuery();

        using SqliteCommand command2 = new(createJobConfigTable, connection);
        _ = command2.ExecuteNonQuery();

        // Migrate schema: add IsImageTask and ImageSource columns if they don't exist
        MigrateTaskConfigurationsSchema(connection);

        // Migrate App.config settings to database if not already done
        MigrateAppConfigToDatabase(connection);

        // Insert default task configurations if none exist
        InsertDefaultTaskConfigurations(connection);

        // Migrate legacy tasks
        connection.Close();
        MigrateLegacyTasks();
    }

    private static void MigrateTaskConfigurationsSchema(SqliteConnection connection)
    {
        // Add IsImageTask column if it doesn't exist
        try
        {
            using SqliteCommand cmd = new("ALTER TABLE TaskConfigurations ADD COLUMN IsImageTask INTEGER NOT NULL DEFAULT 0", connection);
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Column already exists, ignore
        }

        // Add ImageSource column if it doesn't exist
        try
        {
            using SqliteCommand cmd = new("ALTER TABLE TaskConfigurations ADD COLUMN ImageSource TEXT NOT NULL DEFAULT 'Clipboard'", connection);
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Column already exists, ignore
        }
    }

    private static void InsertDefaultTaskConfigurations(SqliteConnection connection)
    {
        SqliteCommand countCommand = new("SELECT COUNT(*) FROM TaskConfigurations", connection);
        int count = Convert.ToInt32(countCommand.ExecuteScalar());

        if (count == 0)
        {
            // Get current system prompt from App.config
            string systemPrompt = ConfigurationManager.AppSettings["System"] ??
                "You are an expert assistant that provides detailed responses to tasks.";

            TaskConfiguration[] defaultConfigs = new[]
            {
                new TaskConfiguration
                {
                    TaskName = "Default Email Response",
                    SystemPrompt = systemPrompt,
                    Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "",
                    Temperature = 0.8,
                    MaxLength = 2048
                },
                new TaskConfiguration
                {
                    TaskName = "Creative Writing",
                    SystemPrompt = "You are a creative writing assistant. Help with creative and imaginative content.",
                    Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "",
                    Temperature = 1.2,
                    MaxLength = 4096
                },
                new TaskConfiguration
                {
                    TaskName = "Technical Analysis",
                    SystemPrompt = "You are a technical expert. Provide precise, factual, and analytical responses.",
                    Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "",
                    Temperature = 0.3,
                    MaxLength = 2048
                },
                new TaskConfiguration
                {
                    TaskName = "Analyze Clipboard Image",
                    SystemPrompt = "Detect what you can find in the image. Use markdown to format the text.",
                    Model = ConfigurationManager.AppSettings["VisionModel"] ?? "",
                    Temperature = 0.5,
                    MaxLength = 2048,
                    IsImageTask = true,
                    ImageSource = "Clipboard"
                },
                new TaskConfiguration
                {
                    TaskName = "Analyze Webcam Image",
                    SystemPrompt = "Detect what you can find in the image. Use markdown to format the text.",
                    Model = ConfigurationManager.AppSettings["VisionModel"] ?? "",
                    Temperature = 0.5,
                    MaxLength = 2048,
                    IsImageTask = true,
                    ImageSource = "Webcam"
                }
            };

            foreach (TaskConfiguration? config in defaultConfigs)
            {
                SaveTaskConfiguration(config, connection);
            }
        }
    }

    public static List<TaskConfiguration> GetAllTaskConfigurations()
    {
        List<TaskConfiguration> configurations = [];

        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        string selectCommand = @"
            SELECT Id, TaskName, SystemPrompt, Model, Temperature, MaxLength, 
                   TopP, TopK, RepeatPenalty, NumCtx, IsActive, IsImageTask, ImageSource, CreatedAt, UpdatedAt
            FROM TaskConfigurations 
            WHERE IsActive = 1
            ORDER BY TaskName";

        using SqliteCommand command = new(selectCommand, connection);
        using SqliteDataReader reader = command.ExecuteReader();

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
                IsImageTask = reader.GetInt32(11) == 1,
                ImageSource = reader.IsDBNull(12) ? "Clipboard" : reader.GetString(12),
                CreatedAt = DateTime.Parse(reader.GetString(13)),
                UpdatedAt = DateTime.Parse(reader.GetString(14))
            });
        }

        return configurations;
    }

    public static TaskConfiguration? GetTaskConfiguration(string taskName)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        string selectCommand = @"
            SELECT Id, TaskName, SystemPrompt, Model, Temperature, MaxLength, 
                   TopP, TopK, RepeatPenalty, NumCtx, IsActive, IsImageTask, ImageSource, CreatedAt, UpdatedAt
            FROM TaskConfigurations 
            WHERE TaskName = @taskName AND IsActive = 1";

        using SqliteCommand command = new(selectCommand, connection);
        _ = command.Parameters.AddWithValue("@taskName", taskName);

        using SqliteDataReader reader = command.ExecuteReader();

        return reader.Read()
            ? new TaskConfiguration
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
                IsImageTask = reader.GetInt32(11) == 1,
                ImageSource = reader.IsDBNull(12) ? "Clipboard" : reader.GetString(12),
                CreatedAt = DateTime.Parse(reader.GetString(13)),
                UpdatedAt = DateTime.Parse(reader.GetString(14))
            }
            : null;
    }

    public static void SaveTaskConfiguration(TaskConfiguration config, SqliteConnection? connection = null)
    {
        bool shouldCloseConnection = connection == null;
        connection ??= new SqliteConnection(ConnectionString);

        if (shouldCloseConnection)
        {
            connection.Open();
        }

        try
        {
            string insertCommand = @"
                INSERT OR REPLACE INTO TaskConfigurations 
                (TaskName, SystemPrompt, Model, Temperature, MaxLength, TopP, TopK, 
                 RepeatPenalty, NumCtx, IsActive, IsImageTask, ImageSource, CreatedAt, UpdatedAt)
                VALUES (@taskName, @systemPrompt, @model, @temperature, @maxLength, 
                        @topP, @topK, @repeatPenalty, @numCtx, @isActive, @isImageTask, @imageSource, @createdAt, @updatedAt)";

            using SqliteCommand command = new(insertCommand, connection);
            _ = command.Parameters.AddWithValue("@taskName", config.TaskName);
            _ = command.Parameters.AddWithValue("@systemPrompt", config.SystemPrompt);
            _ = command.Parameters.AddWithValue("@model", config.Model);
            _ = command.Parameters.AddWithValue("@temperature", config.Temperature);
            _ = command.Parameters.AddWithValue("@maxLength", config.MaxLength);
            _ = command.Parameters.AddWithValue("@topP", config.TopP);
            _ = command.Parameters.AddWithValue("@topK", config.TopK);
            _ = command.Parameters.AddWithValue("@repeatPenalty", config.RepeatPenalty);
            _ = command.Parameters.AddWithValue("@numCtx", config.NumCtx);
            _ = command.Parameters.AddWithValue("@isActive", config.IsActive ? 1 : 0);
            _ = command.Parameters.AddWithValue("@isImageTask", config.IsImageTask ? 1 : 0);
            _ = command.Parameters.AddWithValue("@imageSource", config.ImageSource);
            _ = command.Parameters.AddWithValue("@createdAt", config.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            _ = command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            _ = command.ExecuteNonQuery();
        }
        finally
        {
            if (shouldCloseConnection)
            {
                connection.Close();
            }
        }
    }

    public static void DeleteTaskConfiguration(string taskName)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        string deleteCommand = "UPDATE TaskConfigurations SET IsActive = 0 WHERE TaskName = @taskName";
        using SqliteCommand command = new(deleteCommand, connection);
        _ = command.Parameters.AddWithValue("@taskName", taskName);
        _ = command.ExecuteNonQuery();
    }

    public static void MigrateLegacyTasks()
    {
        try
        {
            using SqliteConnection connection = new(ConnectionString);
            connection.Open();

            // Check if legacy tasks have already been migrated
            SqliteCommand checkCommand = new("SELECT COUNT(*) FROM TaskConfigurations WHERE TaskName LIKE 'Legacy_%'", connection);
            int existingLegacyCount = Convert.ToInt32(checkCommand.ExecuteScalar());

            if (existingLegacyCount > 0)
            {
                // Legacy tasks already migrated
                return;
            }

            // Get legacy task resources from Resources
            Type resourceType = typeof(Resources.Resources);
            string defaultModel = ConfigurationManager.AppSettings["OllamaModel"] ?? "";

            List<TaskConfiguration> legacyTasks = [];

            // Map legacy tasks to their appropriate categories and settings
            Dictionary<string, (string category, double temperature, int maxLength)> legacyTaskMappings = new()
            {
                ["Task_1"] = ("Email - Generic Response", 0.8, 2048),
                ["Task_2"] = ("Email - Detailed Email Response", 0.7, 3072),
                ["Task_3"] = ("Email - Agree Proposal", 0.8, 2048),
                ["Task_4"] = ("Email - Decline Proposal", 0.8, 2048),
                ["Task_5"] = ("Email - Request More Info", 0.8, 2048),
                ["Task_6"] = ("Email - Request Meeting", 0.8, 2048),
                ["Task_7"] = ("Email - Will Solve Problem", 0.8, 2048),
                ["Task_8"] = ("Email - Cannot Solve Problem", 0.8, 2048),
                ["Task_9"] = ("Email - Request Feedback", 0.8, 2048),
                ["Task_10"] = ("Email - Request Confirmation", 0.8, 2048),
                ["Task_11"] = ("Email - Request Rescheduling", 0.8, 2048),
                ["Task_12"] = ("Analysis - Explain Precisely", 0.5, 2048),
                ["Task_13"] = ("Analysis - Search For Errors", 0.3, 2048),
                ["Task_14"] = ("Analysis - Propose Improvements", 0.6, 2048),
                ["Task_16"] = ("Translation to English", 0.3, 1024),
                ["Task_17"] = ("Translation to German", 0.3, 1024),
                ["Task_18"] = ("Translation to French", 0.3, 1024),
                ["Task_19"] = ("Translation to Spanish", 0.3, 1024),
                ["Task_20"] = ("Email - Already Solved", 0.8, 2048),
                ["Task_21"] = ("Email - Working on Problem", 0.8, 2048),
                ["Task_22"] = ("Summarize", 0.5, 1024)
            };

            foreach (KeyValuePair<string, (string category, double temperature, int maxLength)> taskMapping in legacyTaskMappings)
            {
                PropertyInfo? property = resourceType.GetProperty(taskMapping.Key);
                if (property != null)
                {
                    string? taskPrompt = property.GetValue(null)?.ToString();
                    if (!string.IsNullOrEmpty(taskPrompt))
                    {
                        (string? category, double temperature, int maxLength) = taskMapping.Value;
                        TaskConfiguration legacyTask = new()
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
            foreach (TaskConfiguration task in legacyTasks)
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
            SqliteCommand checkCommand = new("SELECT COUNT(*) FROM Configuration", connection);
            int existingCount = Convert.ToInt32(checkCommand.ExecuteScalar());

            if (existingCount > 0)
            {
                // Configuration already migrated
                return;
            }

            // Get all App.config settings and migrate them to database
            NameValueCollection appSettings = ConfigurationManager.AppSettings;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (string? key in appSettings.AllKeys)
            {
                if (key is null)
                {
                    continue;
                }

                string value = appSettings[key] ?? string.Empty;

                string insertCommand = @"
                    INSERT INTO Configuration (Key, Value, CreatedAt, UpdatedAt)
                    VALUES (@key, @value, @createdAt, @updatedAt)";

                using SqliteCommand command = new(insertCommand, connection);
                _ = command.Parameters.AddWithValue("@key", key);
                _ = command.Parameters.AddWithValue("@value", value);
                _ = command.Parameters.AddWithValue("@createdAt", timestamp);
                _ = command.Parameters.AddWithValue("@updatedAt", timestamp);
                _ = command.ExecuteNonQuery();
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
            using SqliteConnection connection = new(ConnectionString);
            connection.Open();

            string selectCommand = "SELECT Value FROM Configuration WHERE Key = @key";
            using SqliteCommand command = new(selectCommand, connection);
            _ = command.Parameters.AddWithValue("@key", key);

            string? result = command.ExecuteScalar()?.ToString();
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
            value ??= string.Empty;
            using SqliteConnection connection = new(ConnectionString);
            connection.Open();

            string upsertCommand = @"
                INSERT OR REPLACE INTO Configuration (Key, Value, CreatedAt, UpdatedAt)
                VALUES (@key, @value, 
                    COALESCE((SELECT CreatedAt FROM Configuration WHERE Key = @key), @timestamp),
                    @timestamp)";

            using SqliteCommand command = new(upsertCommand, connection);
            _ = command.Parameters.AddWithValue("@key", key);
            _ = command.Parameters.AddWithValue("@value", value);
            _ = command.Parameters.AddWithValue("@timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            _ = command.ExecuteNonQuery();
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
        Dictionary<string, string> configurations = [];

        try
        {
            using SqliteConnection connection = new(ConnectionString);
            connection.Open();

            string selectCommand = "SELECT Key, Value FROM Configuration ORDER BY Key";
            using SqliteCommand command = new(selectCommand, connection);
            using SqliteDataReader reader = command.ExecuteReader();

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

    /// <summary>
    /// Restores the default task configurations by soft-deleting all existing tasks and inserting defaults.
    /// </summary>
    public static void RestoreDefaultTaskConfigurations()
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        // Soft-delete all existing task configurations
        string softDeleteCommand = "UPDATE TaskConfigurations SET IsActive = 0, UpdatedAt = @updatedAt";
        using SqliteCommand deleteCommand = new(softDeleteCommand, connection);
        _ = deleteCommand.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        _ = deleteCommand.ExecuteNonQuery();

        // Insert default task configurations
        InsertDefaultTaskConfigurationsForced(connection);
    }

    private static void InsertDefaultTaskConfigurationsForced(SqliteConnection connection)
    {
        // Get current system prompt from App.config
        string systemPrompt = ConfigurationManager.AppSettings["System"] ??
            "You are an expert assistant that provides detailed responses to tasks.";

        TaskConfiguration[] defaultConfigs = new[]
        {
            new TaskConfiguration
            {
                TaskName = "Answering Emails",
                SystemPrompt = systemPrompt,
                Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "",
                Temperature = 0.8,
                MaxLength = 2048,
                TopP = 0.9,
                TopK = 40,
                RepeatPenalty = 1.1,
                NumCtx = 2048,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new TaskConfiguration
            {
                TaskName = "Spelling Correction",
                SystemPrompt = "You are a spelling and grammar correction assistant. Correct spelling and grammar errors while preserving the original meaning and tone.",
                Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "",
                Temperature = 0.1,
                MaxLength = 2048,
                TopP = 0.9,
                TopK = 40,
                RepeatPenalty = 1.1,
                NumCtx = 2048,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new TaskConfiguration
            {
                TaskName = "Summary",
                SystemPrompt = "You are a summarization assistant. Create concise, well-structured summaries that capture the key points and main ideas.",
                Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "",
                Temperature = 0.3,
                MaxLength = 1024,
                TopP = 0.9,
                TopK = 40,
                RepeatPenalty = 1.1,
                NumCtx = 2048,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new TaskConfiguration
            {
                TaskName = "Explanation",
                SystemPrompt = "You are an explanation assistant. Provide clear, detailed explanations that are easy to understand.",
                Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "",
                Temperature = 0.5,
                MaxLength = 2048,
                TopP = 0.9,
                TopK = 40,
                RepeatPenalty = 1.1,
                NumCtx = 2048,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new TaskConfiguration
            {
                TaskName = "Translate to German",
                SystemPrompt = "You are a professional translator. Translate the given text to German while maintaining the original meaning, tone, and context.",
                Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "",
                Temperature = 0.3,
                MaxLength = 2048,
                TopP = 0.9,
                TopK = 40,
                RepeatPenalty = 1.1,
                NumCtx = 2048,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new TaskConfiguration
            {
                TaskName = "Translate to English",
                SystemPrompt = "You are a professional translator. Translate the given text to English while maintaining the original meaning, tone, and context.",
                Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "",
                Temperature = 0.3,
                MaxLength = 2048,
                TopP = 0.9,
                TopK = 40,
                RepeatPenalty = 1.1,
                NumCtx = 2048,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new TaskConfiguration
            {
                TaskName = "Translate to French",
                SystemPrompt = "You are a professional translator. Translate the given text to French while maintaining the original meaning, tone, and context.",
                Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "",
                Temperature = 0.3,
                MaxLength = 2048,
                TopP = 0.9,
                TopK = 40,
                RepeatPenalty = 1.1,
                NumCtx = 2048,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new TaskConfiguration
            {
                TaskName = "Translate to Spanish",
                SystemPrompt = "You are a professional translator. Translate the given text to Spanish while maintaining the original meaning, tone, and context.",
                Model = ConfigurationManager.AppSettings["OllamaModel"] ?? "",
                Temperature = 0.3,
                MaxLength = 2048,
                TopP = 0.9,
                TopK = 40,
                RepeatPenalty = 1.1,
                NumCtx = 2048,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new TaskConfiguration
            {
                TaskName = "Analyze Clipboard Image",
                SystemPrompt = "Detect what you can find in the image. Use markdown to format the text.",
                Model = ConfigurationManager.AppSettings["VisionModel"] ?? "",
                Temperature = 0.5,
                MaxLength = 2048,
                TopP = 0.9,
                TopK = 40,
                RepeatPenalty = 1.1,
                NumCtx = 2048,
                IsActive = true,
                IsImageTask = true,
                ImageSource = "Clipboard",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new TaskConfiguration
            {
                TaskName = "Analyze Webcam Image",
                SystemPrompt = "Detect what you can find in the image. Use markdown to format the text.",
                Model = ConfigurationManager.AppSettings["VisionModel"] ?? "",
                Temperature = 0.5,
                MaxLength = 2048,
                TopP = 0.9,
                TopK = 40,
                RepeatPenalty = 1.1,
                NumCtx = 2048,
                IsActive = true,
                IsImageTask = true,
                ImageSource = "Webcam",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            }
        };

        foreach (TaskConfiguration? config in defaultConfigs)
        {
            SaveTaskConfiguration(config, connection);
        }
    }
}