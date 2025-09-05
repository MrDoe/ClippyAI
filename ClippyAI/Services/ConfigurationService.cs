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

        using var command1 = new SqliteCommand(createTaskConfigTable, connection);
        command1.ExecuteNonQuery();

        using var command2 = new SqliteCommand(createJobConfigTable, connection);
        command2.ExecuteNonQuery();

        // Insert default task configurations if none exist
        InsertDefaultTaskConfigurations(connection);
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
}