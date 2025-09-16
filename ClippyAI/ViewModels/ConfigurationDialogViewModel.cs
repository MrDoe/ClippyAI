using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClippyAI.Models;
using ClippyAI.Services;
using ClippyAI.Views;
using Microsoft.Data.Sqlite;
namespace ClippyAI.Views;

public partial class ConfigurationDialogViewModel : ViewModelBase
{
    private MainWindow? _mainWindow;

    [ObservableProperty]
    private string _aiProvider = ConfigurationService.GetConfigurationValue("AIProvider", "Ollama");

    void OnAIProviderChanged(string value)
    {
        // Refresh models when provider changes
        _ = RefreshModels();
    }

    [ObservableProperty]
    private string _ollamaUrl = ConfigurationService.GetConfigurationValue("OllamaUrl", "http://localhost:11434/api");

    [ObservableProperty]
    private string _ollamaModel = ConfigurationService.GetConfigurationValue("OllamaModel", "gemma2:latest");

    [ObservableProperty]
    private string _openAIApiKey = ConfigurationService.GetConfigurationValue("OpenAIApiKey", "");

    [ObservableProperty]
    private string _openAIBaseUrl = ConfigurationService.GetConfigurationValue("OpenAIBaseUrl", "https://api.openai.com/v1");

    [ObservableProperty]
    private string _openAIModel = ConfigurationService.GetConfigurationValue("OpenAIModel", "gpt-3.5-turbo");

    [ObservableProperty]
    private string _openAIVisionModel = ConfigurationService.GetConfigurationValue("OpenAIVisionModel", "gpt-4-vision-preview");

    [ObservableProperty]
    private string _defaultTask = ConfigurationService.GetConfigurationValue("DefaultTask", "Write a response to this email.");

    [ObservableProperty]
    private string _systemPrompt = ConfigurationService.GetConfigurationValue("System", "");

    [ObservableProperty]
    private bool _useEmbeddings = bool.Parse(ConfigurationService.GetConfigurationValue("UseEmbeddings", "True"));

    [ObservableProperty]
    private bool _storeAllResponses = bool.Parse(ConfigurationService.GetConfigurationValue("StoreAllResponses", "False"));

    [ObservableProperty]
    private bool _autoMode = bool.Parse(ConfigurationService.GetConfigurationValue("AutoMode", "False"));

    [ObservableProperty]
    private string _postgreSqlConnection = ConfigurationService.GetConfigurationValue("PostgreSqlConnection", "");

    [ObservableProperty]
    private string _postgresOllamaUrl = ConfigurationService.GetConfigurationValue("PostgresOllamaUrl", "");

    [ObservableProperty]
    private string _visionModel = ConfigurationService.GetConfigurationValue("VisionModel", "llama3.2-vision:latest");

    [ObservableProperty]
    private string _visionPrompt = ConfigurationService.GetConfigurationValue("VisionPrompt", "Describe the image.");

    [ObservableProperty]
    private string _videoDevice = ConfigurationService.GetConfigurationValue("VisionDevice", "/dev/video0");

    [ObservableProperty]
    private string _defaultLanguage = ConfigurationService.GetConfigurationValue("DefaultLanguage", "English");

    [ObservableProperty]
    private string _linuxKeyboardDevice = ConfigurationService.GetConfigurationValue("LinuxKeyboardDevice", "");

    [ObservableProperty]
    private string _embeddingModel = ConfigurationService.GetConfigurationValue("EmbeddingModel", "nomic-embed-text");

    // SSH Configuration properties
    [ObservableProperty]
    private bool _useSSH = bool.Parse(ConfigurationService.GetConfigurationValue("UseSSH", "False"));

    [ObservableProperty]
    private string _sshUsername = ConfigurationService.GetConfigurationValue("SSHUsername", "SSHUser");

    [ObservableProperty]
    private string _sshServerUrl = ConfigurationService.GetConfigurationValue("SSHServerUrl", "myServer");

    [ObservableProperty]
    private string _sshPort = ConfigurationService.GetConfigurationValue("SSHPort", "22");

    [ObservableProperty]
    private string _sshLocalTunnel = ConfigurationService.GetConfigurationValue("SSHLocalTunnel", "localhost:11443:localhost");

    [ObservableProperty]
    private string _sshRemoteTunnel = ConfigurationService.GetConfigurationValue("SSHRemoteTunnel", "");

    [ObservableProperty]
    private string _sshPrivateKeyFile = ConfigurationService.GetConfigurationValue("SSHPrivateKeyFile", "~/.ssh/private.key");

    [ObservableProperty]
    private string _sshTestResult = "";

    [ObservableProperty]
    private bool _sshTestInProgress = false;

    // Collections for dropdowns
    [ObservableProperty]
    private ObservableCollection<string> _languageItems = new(new[] { "English", "Deutsch", "Français", "Español", "Italiano", "Português", "中文", "日本語", "한국어", "Русский" });

    [ObservableProperty]
    private ObservableCollection<string> _aiProviderItems = new(new[] { "Ollama", "OpenAI" });

    [ObservableProperty]
    private ObservableCollection<string> _availableTasks = [];

    // Additional configuration options from main window
    [ObservableProperty]
    private float _threshold = float.Parse(ConfigurationService.GetConfigurationValue("Threshold", "0.2"));

    [ObservableProperty]
    private int _embeddingsCount = 0;

    [ObservableProperty]
    private ObservableCollection<string> _modelItems = [];

    [ObservableProperty]
    private ObservableCollection<string> _videoDevices = [];

    // Static reference to ModelItems for XAML binding
    public static ObservableCollection<string> AvailableModels { get; set; } = [];

    // Task-specific configurations
    [ObservableProperty]
    private ObservableCollection<TaskConfiguration> _taskConfigurations = [];

    [ObservableProperty]
    private TaskConfiguration? _selectedTaskConfiguration;

    [ObservableProperty]
    private string _newTaskName = string.Empty;

    public bool IsTaskSelected => SelectedTaskConfiguration != null;

    partial void OnSelectedTaskConfigurationChanged(TaskConfiguration? value)
    {
        OnPropertyChanged(nameof(IsTaskSelected));
    }

    public ConfigurationDialogViewModel(MainWindow? mainWindow = null)
    {
        _mainWindow = mainWindow;
        LoadTaskConfigurations();
        PopulateAvailableTasks();
        Task.Run(InitializeCollections).Wait();
    }

    private void PopulateAvailableTasks()
    {
        AvailableTasks.Clear();
        foreach (var task in TaskConfigurations)
        {
            AvailableTasks.Add(task.TaskName);
        }
    }

    private async Task InitializeCollections()
    {
        await RefreshModels();
        await RefreshVideoDevices();
        await LoadEmbeddingsCount();
    }

    private void LoadTaskConfigurations()
    {
        try
        {
            ConfigurationService.InitializeDatabase();
            var tasks = ConfigurationService.GetAllTaskConfigurations();
            TaskConfigurations.Clear();
            foreach (var task in tasks)
            {
                TaskConfigurations.Add(task);
            }
            PopulateAvailableTasks();
        }
        catch (Exception ex)
        {
            // Handle error - could show error dialog
            System.Diagnostics.Debug.WriteLine($"Error loading task configurations: {ex.Message}");
        }
    }

    [RelayCommand]
    private void AddNewTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskName))
            return;

        var newTask = new TaskConfiguration
        {
            TaskName = NewTaskName,
            SystemPrompt = SystemPrompt,
            Model = OllamaModel,
            Temperature = 0.8,
            MaxLength = 2048,
            TopP = 0.9,
            TopK = 40,
            RepeatPenalty = 1.1,
            NumCtx = 2048
        };

        try
        {
            ConfigurationService.SaveTaskConfiguration(newTask);
            TaskConfigurations.Add(newTask);
            PopulateAvailableTasks();
            SelectedTaskConfiguration = newTask;
            NewTaskName = string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving task configuration: {ex.Message}");
        }
    }

    [RelayCommand]
    private void DeleteTask()
    {
        if (SelectedTaskConfiguration == null)
            return;

        try
        {
            ConfigurationService.DeleteTaskConfiguration(SelectedTaskConfiguration.TaskName);
            TaskConfigurations.Remove(SelectedTaskConfiguration);
            PopulateAvailableTasks();
            SelectedTaskConfiguration = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting task configuration: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveTaskConfiguration()
    {
        if (SelectedTaskConfiguration == null)
            return;

        try
        {
            ConfigurationService.SaveTaskConfiguration(SelectedTaskConfiguration);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating task configuration: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RestoreDefaultTasks()
    {
        try
        {
            // Restore default tasks using the public method
            ConfigurationService.RestoreDefaultTaskConfigurations();

            // Refresh the UI
            LoadTaskConfigurations();
            SelectedTaskConfiguration = TaskConfigurations.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error restoring default tasks: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshModels()
    {
        try
        {
            ModelItems.Clear();
            AvailableModels.Clear();
            var models = await OllamaService.GetModelsAsync();
            foreach (var model in models)
            {
                ModelItems.Add(model);
                AvailableModels.Add(model);
            }
            // Force SelectedItem to update after ItemsSource is populated
            if (!string.IsNullOrEmpty(OllamaModel) && !ModelItems.Contains(OllamaModel))
            {
                // Optional: Add the configured model if missing (e.g., not pulled yet)
                ModelItems.Add(OllamaModel);
                AvailableModels.Add(OllamaModel);
            }
            // Trigger property change to refresh binding
            OnPropertyChanged(nameof(OllamaModel));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing models: {ex.Message}");
            // Fallback: Ensure at least the configured model is available
            if (!ModelItems.Contains(OllamaModel))
            {
                ModelItems.Add(OllamaModel);
                AvailableModels.Add(OllamaModel);
            }
        }
    }

    [RelayCommand]
    private async Task AddModel()
    {
        // This would typically open a dialog to enter a model name to pull
        // For now, we'll just refresh the model list
        await RefreshModels();
    }

    [RelayCommand]
    private async Task DeleteModel()
    {
        if (!string.IsNullOrEmpty(OllamaModel))
        {
            try
            {
                await OllamaService.DeleteModelAsync(OllamaModel);
                await RefreshModels();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting model: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task RefreshVideoDevices()
    {
        try
        {
            VideoDevices.Clear();
            // Load video devices (this would need to be implemented similar to MainViewModel)
            var devices = await Task.Run(() => GetVideoDevices());
            foreach (var device in devices)
            {
                VideoDevices.Add(device);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing video devices: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ClearEmbeddings()
    {
        try
        {
            await OllamaService.ClearEmbeddings();
            EmbeddingsCount = 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing embeddings: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ConfigureHotkeyDevice()
    {
        // only for Linux
        if (!OperatingSystem.IsLinux())
        {
            System.Diagnostics.Debug.WriteLine("This feature is only supported on Linux.");
            return;
        }

        if (_mainWindow == null)
        {
            System.Diagnostics.Debug.WriteLine("MainWindow reference is required for hotkey configuration.");
            return;
        }

        try
        {
            var hotkeyService = new HotkeyService(_mainWindow);
            await hotkeyService.SetupHotkeyDevice();
            LinuxKeyboardDevice = ConfigurationService.GetConfigurationValue("LinuxKeyboardDevice", "");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error configuring hotkey device: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TestSSHConnection()
    {
        SshTestInProgress = true;
        SshTestResult = "Testing SSH connection...";
        
        try
        {
            // Save current SSH settings temporarily to test with current form values
            var tempUseSSH = ConfigurationService.GetConfigurationValue("UseSSH");
            var tempUsername = ConfigurationService.GetConfigurationValue("SSHUsername");
            var tempServerUrl = ConfigurationService.GetConfigurationValue("SSHServerUrl");
            var tempPort = ConfigurationService.GetConfigurationValue("SSHPort");
            var tempPrivateKeyFile = ConfigurationService.GetConfigurationValue("SSHPrivateKeyFile");

            // Set current form values for testing
            ConfigurationService.SetConfigurationValue("UseSSH", UseSSH.ToString());
            ConfigurationService.SetConfigurationValue("SSHUsername", SshUsername);
            ConfigurationService.SetConfigurationValue("SSHServerUrl", SshServerUrl);
            ConfigurationService.SetConfigurationValue("SSHPort", SshPort);
            ConfigurationService.SetConfigurationValue("SSHPrivateKeyFile", SshPrivateKeyFile);

            await Task.Run(() =>
            {
                var sshService = new SSHService();
                SshTestResult = sshService.TestConnection();
            });

            // Restore original settings
            ConfigurationService.SetConfigurationValue("UseSSH", tempUseSSH);
            ConfigurationService.SetConfigurationValue("SSHUsername", tempUsername);
            ConfigurationService.SetConfigurationValue("SSHServerUrl", tempServerUrl);
            ConfigurationService.SetConfigurationValue("SSHPort", tempPort);
            ConfigurationService.SetConfigurationValue("SSHPrivateKeyFile", tempPrivateKeyFile);
        }
        catch (Exception ex)
        {
            SshTestResult = $"SSH test error: {ex.Message}";
        }
        finally
        {
            SshTestInProgress = false;
        }
    }

    private List<string> GetVideoDevices()
    {
        // This is a simplified version - the actual implementation should match MainViewModel
        var devices = new List<string>();
        try
        {
            // Add basic video devices for Linux/Windows
            for (int i = 0; i < 10; i++)
            {
                devices.Add($"/dev/video{i}");
            }
        }
        catch
        {
            // Ignore errors and return empty list
        }
        return devices;
    }

    private async Task LoadEmbeddingsCount()
    {
        try
        {
            EmbeddingsCount = await OllamaService.GetEmbeddingsCount();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading embeddings count: {ex.Message}");
            EmbeddingsCount = 0;
        }
    }

    public void Save()
    {
        // Update all configuration values
        ConfigurationService.SetConfigurationValue("AIProvider", AiProvider);
        ConfigurationService.SetConfigurationValue("OllamaUrl", OllamaUrl);
        ConfigurationService.SetConfigurationValue("OllamaModel", OllamaModel);
        ConfigurationService.SetConfigurationValue("OpenAIApiKey", OpenAIApiKey);
        ConfigurationService.SetConfigurationValue("OpenAIBaseUrl", OpenAIBaseUrl);
        ConfigurationService.SetConfigurationValue("OpenAIModel", OpenAIModel);
        ConfigurationService.SetConfigurationValue("OpenAIVisionModel", OpenAIVisionModel);
        ConfigurationService.SetConfigurationValue("DefaultTask", DefaultTask);
        ConfigurationService.SetConfigurationValue("System", SystemPrompt);
        ConfigurationService.SetConfigurationValue("UseEmbeddings", UseEmbeddings.ToString());
        ConfigurationService.SetConfigurationValue("StoreAllResponses", StoreAllResponses.ToString());
        ConfigurationService.SetConfigurationValue("AutoMode", AutoMode.ToString());
        ConfigurationService.SetConfigurationValue("PostgreSqlConnection", PostgreSqlConnection);
        ConfigurationService.SetConfigurationValue("PostgresOllamaUrl", PostgresOllamaUrl);
        ConfigurationService.SetConfigurationValue("VisionModel", VisionModel);
        ConfigurationService.SetConfigurationValue("VisionPrompt", VisionPrompt);
        ConfigurationService.SetConfigurationValue("VisionDevice", VideoDevice);
        ConfigurationService.SetConfigurationValue("DefaultLanguage", DefaultLanguage);
        ConfigurationService.SetConfigurationValue("LinuxKeyboardDevice", LinuxKeyboardDevice);
        ConfigurationService.SetConfigurationValue("Threshold", Threshold.ToString());
        ConfigurationService.SetConfigurationValue("EmbeddingModel", EmbeddingModel);

        // SSH Configuration
        ConfigurationService.SetConfigurationValue("UseSSH", UseSSH.ToString());
        ConfigurationService.SetConfigurationValue("SSHUsername", SshUsername);
        ConfigurationService.SetConfigurationValue("SSHServerUrl", SshServerUrl);
        ConfigurationService.SetConfigurationValue("SSHPort", SshPort);
        ConfigurationService.SetConfigurationValue("SSHLocalTunnel", SshLocalTunnel);
        ConfigurationService.SetConfigurationValue("SSHRemoteTunnel", SshRemoteTunnel);
        ConfigurationService.SetConfigurationValue("SSHPrivateKeyFile", SshPrivateKeyFile);
    }

    public void Cancel()
    {
        // Close dialog without saving
    }
}