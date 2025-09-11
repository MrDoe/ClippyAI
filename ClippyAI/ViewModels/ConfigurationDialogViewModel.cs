using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClippyAI.Models;
using ClippyAI.Services;
using ClippyAI.Views;

namespace ClippyAI.Views;

public partial class ConfigurationDialogViewModel : ViewModelBase
{
    private MainWindow? _mainWindow;

    [ObservableProperty]
    private string _ollamaUrl = ConfigurationManager.AppSettings["OllamaUrl"] ?? "http://localhost:11434/api";

    [ObservableProperty]
    private string _ollamaModel = ConfigurationManager.AppSettings["OllamaModel"] ?? "gemma2:latest";

    [ObservableProperty]
    private string _defaultTask = ConfigurationManager.AppSettings["DefaultTask"] ?? "Write a response to this email.";

    [ObservableProperty]
    private string _systemPrompt = ConfigurationManager.AppSettings["System"] ?? "";

    [ObservableProperty]
    private bool _useEmbeddings = Convert.ToBoolean(ConfigurationManager.AppSettings["UseEmbeddings"] ?? "True");

    [ObservableProperty]
    private bool _storeAllResponses = Convert.ToBoolean(ConfigurationManager.AppSettings["StoreAllResponses"] ?? "False");

    [ObservableProperty]
    private bool _autoMode = Convert.ToBoolean(ConfigurationManager.AppSettings["AutoMode"] ?? "False");

    [ObservableProperty]
    private string _postgreSqlConnection = ConfigurationManager.AppSettings["PostgreSqlConnection"] ?? "";

    [ObservableProperty]
    private string _postgresOllamaUrl = ConfigurationManager.AppSettings["PostgresOllamaUrl"] ?? "";

    [ObservableProperty]
    private string _visionModel = ConfigurationManager.AppSettings["VisionModel"] ?? "llama3.2-vision:latest";

    [ObservableProperty]
    private string _visionPrompt = ConfigurationManager.AppSettings["VisionPrompt"] ?? "Describe the image.";

    [ObservableProperty]
    private string _videoDevice = ConfigurationManager.AppSettings["VisionDevice"] ?? "/dev/video0";

    [ObservableProperty]
    private string _defaultLanguage = ConfigurationManager.AppSettings["DefaultLanguage"] ?? "English";

    [ObservableProperty]
    private string _linuxKeyboardDevice = ConfigurationManager.AppSettings["LinuxKeyboardDevice"] ?? "";

    [ObservableProperty]
    private string _embeddingModel = ConfigurationManager.AppSettings["EmbeddingModel"] ?? "nomic-embed-text";

    // Collections for dropdowns
    [ObservableProperty]
    private ObservableCollection<string> _languageItems = new(new[] { "English", "Deutsch", "Français", "Español", "Italiano", "Português", "中文", "日本語", "한국어", "Русский" });

    [ObservableProperty]
    private ObservableCollection<string> _availableTasks = new();

    // New advanced configuration options
    [ObservableProperty]
    private double _temperature = Convert.ToDouble(ConfigurationManager.AppSettings["Temperature"] ?? "1.0");

    [ObservableProperty]
    private int _maxLength = Convert.ToInt32(ConfigurationManager.AppSettings["MaxLength"] ?? "2048");

    [ObservableProperty]
    private double _topP = Convert.ToDouble(ConfigurationManager.AppSettings["TopP"] ?? "0.9");

    [ObservableProperty]
    private int _topK = Convert.ToInt32(ConfigurationManager.AppSettings["TopK"] ?? "40");

    [ObservableProperty]
    private double _repeatPenalty = Convert.ToDouble(ConfigurationManager.AppSettings["RepeatPenalty"] ?? "1.1");

    [ObservableProperty]
    private int _numCtx = Convert.ToInt32(ConfigurationManager.AppSettings["NumCtx"] ?? "2048");

    // Additional configuration options from main window
    [ObservableProperty]
    private float _threshold = Convert.ToSingle(ConfigurationManager.AppSettings["Threshold"] ?? "0.2");

    [ObservableProperty]
    private int _embeddingsCount = 0;

    [ObservableProperty]
    private ObservableCollection<string> _modelItems = new();

    [ObservableProperty]
    private ObservableCollection<string> _videoDevices = new();

    // Task-specific configurations
    [ObservableProperty]
    private ObservableCollection<TaskConfiguration> _taskConfigurations = new();

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
        InitializeCollections();
    }

    private void PopulateAvailableTasks()
    {
        AvailableTasks.Clear();
        foreach (var task in TaskConfigurations)
        {
            AvailableTasks.Add(task.TaskName);
        }
    }

    private async void InitializeCollections()
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
            Temperature = Temperature,
            MaxLength = MaxLength,
            TopP = TopP,
            TopK = TopK,
            RepeatPenalty = RepeatPenalty,
            NumCtx = NumCtx
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
    private async Task RefreshModels()
    {
        try
        {
            ModelItems.Clear();
            var models = await OllamaService.GetModelsAsync();
            foreach (var model in models)
            {
                ModelItems.Add(model);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing models: {ex.Message}");
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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error configuring hotkey device: {ex.Message}");
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
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        
        // Update all configuration values
        UpdateConfigValue(config, "OllamaUrl", OllamaUrl);
        UpdateConfigValue(config, "OllamaModel", OllamaModel);
        UpdateConfigValue(config, "DefaultTask", DefaultTask);
        UpdateConfigValue(config, "System", SystemPrompt);
        UpdateConfigValue(config, "UseEmbeddings", UseEmbeddings.ToString());
        UpdateConfigValue(config, "StoreAllResponses", StoreAllResponses.ToString());
        UpdateConfigValue(config, "AutoMode", AutoMode.ToString());
        UpdateConfigValue(config, "PostgreSqlConnection", PostgreSqlConnection);
        UpdateConfigValue(config, "PostgresOllamaUrl", PostgresOllamaUrl);
        UpdateConfigValue(config, "VisionModel", VisionModel);
        UpdateConfigValue(config, "VisionPrompt", VisionPrompt);
        UpdateConfigValue(config, "VisionDevice", VideoDevice);
        UpdateConfigValue(config, "DefaultLanguage", DefaultLanguage);
        UpdateConfigValue(config, "LinuxKeyboardDevice", LinuxKeyboardDevice);
        
        // Add new advanced configuration options
        UpdateConfigValue(config, "Temperature", Temperature.ToString());
        UpdateConfigValue(config, "MaxLength", MaxLength.ToString());
        UpdateConfigValue(config, "TopP", TopP.ToString());
        UpdateConfigValue(config, "TopK", TopK.ToString());
        UpdateConfigValue(config, "RepeatPenalty", RepeatPenalty.ToString());
        UpdateConfigValue(config, "NumCtx", NumCtx.ToString());
        UpdateConfigValue(config, "Threshold", Threshold.ToString());
        UpdateConfigValue(config, "EmbeddingModel", EmbeddingModel);

        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    public void Cancel()
    {
        // Close dialog without saving
    }

    private void UpdateConfigValue(Configuration config, string key, string value)
    {
        config.AppSettings.Settings.Remove(key);
        config.AppSettings.Settings.Add(key, value);
    }
}