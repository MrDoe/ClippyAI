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
using System.IO;
#if WINDOWS
using DirectShowLib;
#endif
namespace ClippyAI.ViewModels;

public partial class ConfigurationDialogViewModel : ViewModelBase
{
    private readonly MainWindow? _mainWindow;

    [ObservableProperty]
    private string _aiProvider = ConfigurationService.GetConfigurationValue("AIProvider", "Ollama");

    // Models that can produce embeddings. The settings show a single list
    // and the selection is stored per provider.
    private static readonly string[] OllamaEmbeddingModels =
    [
        "nomic-embed-text",
        "mxbai-embed-large",
        "all-minilm",
        "bge-m3",
        "snowflake-arctic-embed"
    ];

    private static readonly string[] OpenAIEmbeddingModels =
    [
        "text-embedding-3-small",
        "text-embedding-3-large",
        "text-embedding-ada-002"
    ];

    // Used when the OpenAI endpoint cannot be reached, for example because
    // no API key is configured yet.
    private static readonly string[] CommonOpenAIModels =
    [
        "gpt-4o",
        "gpt-4o-mini",
        "gpt-4.1",
        "gpt-4.1-mini",
        "o3-mini"
    ];

    /// <summary>
    /// Called when the selected provider changes. The available models come
    /// from that provider, so both lists have to be rebuilt.
    /// </summary>
    partial void OnAiProviderChanged(string value)
    {
        _ = RefreshModels();
        RefreshEmbeddingModels();
    }

    [ObservableProperty]
    private string _ollamaUrl = ConfigurationService.GetConfigurationValue("OllamaUrl", "http://localhost:11434/api");

    [ObservableProperty]
    private string _ollamaModel = ConfigurationService.GetConfigurationValue("OllamaModel", "");

    [ObservableProperty]
    private string _openAIApiKey = ConfigurationService.GetConfigurationValue("OpenAIApiKey", "");

    [ObservableProperty]
    private string _openAIBaseUrl = ConfigurationService.GetConfigurationValue("OpenAIBaseUrl", "https://api.openai.com/v1");

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
    private string _vectorDbPath = ConfigurationService.GetConfigurationValue("VectorDbPath", "");

    [ObservableProperty]
    private string _videoDevice = ConfigurationService.GetConfigurationValue("VideoDevice", "");

    [ObservableProperty]
    private string _defaultLanguage = ConfigurationService.GetConfigurationValue("DefaultLanguage", "English");

    [ObservableProperty]
    private string _linuxKeyboardDevice = ConfigurationService.GetConfigurationValue("LinuxKeyboardDevice", "");

    [ObservableProperty]
    private string _ollamaEmbeddingModel = EmbeddingService.GetModel("Ollama");

    [ObservableProperty]
    private string _openAIEmbeddingModel = EmbeddingService.GetModel("OpenAI");

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
    private string _sshLocalTunnel = ConfigurationService.GetConfigurationValue("SSHLocalTunnel", "localhost:11443:localhost:11434");

    [ObservableProperty]
    private string _sshRemoteTunnel = ConfigurationService.GetConfigurationValue("SSHRemoteTunnel", "");

    [ObservableProperty]
    private string _sshPrivateKeyFile = ConfigurationService.GetConfigurationValue("SSHPrivateKeyFile", "~/.ssh/private.key");

    [ObservableProperty]
    private string _sshTestResult = "";

    [ObservableProperty]
    private bool _sshTestInProgress = false;

    // General AI Provider connection test properties
    [ObservableProperty]
    private bool _generalTestInProgress = false;

    [ObservableProperty]
    private string _generalTestResult = "";

    [ObservableProperty]
    private ObservableCollection<string> _languageItems = new(["English", "Deutsch", "Français", "Español", "Italiano", "Português", "中文", "日本語", "한국어", "Русский"]);

    [ObservableProperty]
    private ObservableCollection<string> _aiProviderItems = new(["Ollama", "OpenAI"]);

    [ObservableProperty]
    private ObservableCollection<string> _availableTasks = [];

    [ObservableProperty]
    private float _threshold = float.Parse(ConfigurationService.GetConfigurationValue("Threshold", "0.2"));

    [ObservableProperty]
    private int _embeddingsCount = 0;

    [ObservableProperty]
    private ObservableCollection<string> _modelItems = [];

    [ObservableProperty]
    private ObservableCollection<string> _embeddingModelItems = [];

    [ObservableProperty]
    private ObservableCollection<string> _videoDevices = [];

    // Static reference to ModelItems for XAML binding
    public static ObservableCollection<string> AvailableModels { get; set; } = [];

    /// <summary>
    /// The embedding model of the provider that is currently selected. The
    /// value is stored per provider, but the interface shows one list, so the
    /// selection follows the provider.
    /// </summary>
    public string SelectedEmbeddingModel
    {
        get => IsOpenAiSelected ? OpenAIEmbeddingModel : OllamaEmbeddingModel;
        set
        {
            if (IsOpenAiSelected)
            {
                OpenAIEmbeddingModel = value;
            }
            else
            {
                OllamaEmbeddingModel = value;
            }

            OnPropertyChanged();
        }
    }

    private bool IsOpenAiSelected => string.Equals(AiProvider, "OpenAI", StringComparison.OrdinalIgnoreCase);

    // Static list of image sources for XAML binding in DataTemplate
    public static ObservableCollection<string> ImageSources { get; } = ["Clipboard", "Webcam"];

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
        Save();
        OnPropertyChanged(nameof(IsTaskSelected));
    }

    public ConfigurationDialogViewModel(MainWindow? mainWindow = null)
    {
        _mainWindow = mainWindow;
        LoadTaskConfigurations();
        PopulateAvailableTasks();
        RefreshEmbeddingModels();
        Task.Run(InitializeCollections).Wait();
    }

    private void PopulateAvailableTasks()
    {
        AvailableTasks.Clear();
        foreach (TaskConfiguration task in TaskConfigurations)
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
            List<TaskConfiguration> tasks = ConfigurationService.GetAllTaskConfigurations();
            TaskConfigurations.Clear();
            foreach (TaskConfiguration task in tasks)
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
        {
            return;
        }

        TaskConfiguration newTask = new()
        {
            TaskName = NewTaskName,
            SystemPrompt = "",
            Model = OllamaModel,
            Temperature = 1.0,
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
        {
            return;
        }

        try
        {
            ConfigurationService.DeleteTaskConfiguration(SelectedTaskConfiguration.TaskName);
            _ = TaskConfigurations.Remove(SelectedTaskConfiguration);
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
        {
            return;
        }

        try
        {
            // Never persist a MaxLength the request path would have to clamp later.
            SelectedTaskConfiguration.MaxLength =
                RequestLimits.ClampOutputTokens(SelectedTaskConfiguration.MaxLength);
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
            ObservableCollection<string> models = await GetModelsForSelectedProviderAsync();
            foreach (string model in models)
            {
                ModelItems.Add(model);
                AvailableModels.Add(model);
            }

            if (IsOpenAiSelected && ModelItems.Count == 0)
            {
                // The endpoint did not answer, most likely because no API key
                // is configured yet. Offer the common models instead of an
                // empty list.
                foreach (string model in CommonOpenAIModels)
                {
                    ModelItems.Add(model);
                    AvailableModels.Add(model);
                }
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

    /// <summary>
    /// Asks the provider that is selected in the form for its models. The
    /// provider is chosen through the configuration, so the form value is
    /// applied for the call and restored afterwards.
    /// </summary>
    private async Task<ObservableCollection<string>> GetModelsForSelectedProviderAsync()
    {
        string? previous = ConfigurationService.GetConfigurationValue("AIProvider");

        try
        {
            ConfigurationService.SetConfigurationValue("AIProvider", AiProvider);
            return await OllamaService.GetCurrentProvider().GetModelsAsync();
        }
        finally
        {
            ConfigurationService.SetConfigurationValue("AIProvider", previous ?? AiProvider);
        }
    }

    /// <summary>
    /// Rebuilds the list of embedding models for the selected provider.
    /// </summary>
    private void RefreshEmbeddingModels()
    {
        EmbeddingModelItems.Clear();

        IEnumerable<string> models = IsOpenAiSelected
            ? OpenAIEmbeddingModels
            : OllamaEmbeddingModels;

        foreach (string model in models)
        {
            EmbeddingModelItems.Add(model);
        }

        // A model that is configured but not in the list stays selectable.
        string current = SelectedEmbeddingModel;
        if (!string.IsNullOrWhiteSpace(current) && !EmbeddingModelItems.Contains(current))
        {
            EmbeddingModelItems.Add(current);
        }
    }

    [RelayCommand]
    private async Task AddModel()
    {
        if (_mainWindow == null)
        {
            System.Diagnostics.Debug.WriteLine("MainWindow reference is required for pulling models.");
            return;
        }

        try
        {
            // Prompt user for model name
            string? modelName = await InputDialog.Prompt(
                _mainWindow,
                "Pull Model",
                "Enter the model name to pull:",
                "e.g., llama2, mistral, neural-chat",
                isRequired: true
            );

            if (string.IsNullOrWhiteSpace(modelName))
            {
                return;
            }

            // Pull the model
            await OllamaService.PullModelAsync(modelName);

            // Refresh the model list
            await RefreshModels();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error pulling model: {ex.Message}");
        }
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
            List<string> devices = await Task.Run(() => GetVideoDevices());
            foreach (string device in devices)
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
            ShowErrorMessage("Hotkey devices can only be configured on Linux.");
            return;
        }

        if (_mainWindow == null)
        {
            ShowErrorMessage("The main window is not available.");
            return;
        }

        try
        {
#if LINUX
            // Reuse the monitor that is already running instead of creating a
            // second one, so the selected device takes effect immediately.
            await _mainWindow.ConfigureHotkeyDeviceAsync();
#endif
            LinuxKeyboardDevice = ConfigurationService.GetConfigurationValue("LinuxKeyboardDevice", "");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error configuring hotkey device: {ex.Message}");
            ShowErrorMessage(ex.Message);
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
            string tempUseSSH = ConfigurationService.GetConfigurationValue("UseSSH");
            string tempUsername = ConfigurationService.GetConfigurationValue("SSHUsername");
            string tempServerUrl = ConfigurationService.GetConfigurationValue("SSHServerUrl");
            string tempPort = ConfigurationService.GetConfigurationValue("SSHPort");
            string tempPrivateKeyFile = ConfigurationService.GetConfigurationValue("SSHPrivateKeyFile");

            // Set current form values for testing
            ConfigurationService.SetConfigurationValue("UseSSH", UseSSH.ToString());
            ConfigurationService.SetConfigurationValue("SSHUsername", SshUsername);
            ConfigurationService.SetConfigurationValue("SSHServerUrl", SshServerUrl);
            ConfigurationService.SetConfigurationValue("SSHPort", SshPort);
            ConfigurationService.SetConfigurationValue("SSHPrivateKeyFile", SshPrivateKeyFile);

            await Task.Run(() =>
            {
                SSHService sshService = new();
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

    [RelayCommand]
    private async Task TestConnection()
    {
        GeneralTestInProgress = true;
        GeneralTestResult = "Testing AI Provider connection...";

        try
        {
            // Save current settings temporarily to test with current form values
            string tempAiProvider = ConfigurationService.GetConfigurationValue("AIProvider");
            string tempOllamaUrl = ConfigurationService.GetConfigurationValue("OllamaUrl");
            string tempOpenAIApiKey = ConfigurationService.GetConfigurationValue("OpenAIApiKey");
            string tempOpenAIBaseUrl = ConfigurationService.GetConfigurationValue("OpenAIBaseUrl");

            // Set current form values for testing
            ConfigurationService.SetConfigurationValue("AIProvider", AiProvider);
            ConfigurationService.SetConfigurationValue("OllamaUrl", OllamaUrl);
            ConfigurationService.SetConfigurationValue("OpenAIApiKey", OpenAIApiKey);
            ConfigurationService.SetConfigurationValue("OpenAIBaseUrl", OpenAIBaseUrl);

            await Task.Run(async () =>
            {
                try
                {
                    if (AiProvider == "Ollama")
                    {
                        // Test Ollama connection
                        ObservableCollection<string> models = await OllamaService.GetModelsAsync();
                        GeneralTestResult = models.Count > 0
                            ? $"✓ Ollama connection successful!\nFound {models.Count} model(s)"
                            : "⚠ Ollama connection successful but no models found.\nYou may need to pull a model first.";
                    }
                    else if (AiProvider == "OpenAI")
                    {
                        // Test OpenAI connection
                        if (string.IsNullOrWhiteSpace(OpenAIApiKey))
                        {
                            GeneralTestResult = "✗ OpenAI connection failed: API key is empty.";
                        }
                        else
                        {
                            ObservableCollection<string> models = await OllamaService.GetModelsAsync();
                            GeneralTestResult = models.Count > 0
                                ? $"✓ OpenAI connection successful!\nFound {models.Count} model(s)"
                                : "⚠ OpenAI connection successful but no models found.\nCheck your API key permissions.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    GeneralTestResult = $"✗ Connection test failed: {ex.Message}";
                }
            });

            // Restore original settings
            ConfigurationService.SetConfigurationValue("AIProvider", tempAiProvider);
            ConfigurationService.SetConfigurationValue("OllamaUrl", tempOllamaUrl);
            ConfigurationService.SetConfigurationValue("OpenAIApiKey", tempOpenAIApiKey);
            ConfigurationService.SetConfigurationValue("OpenAIBaseUrl", tempOpenAIBaseUrl);
        }
        catch (Exception ex)
        {
            GeneralTestResult = $"Connection test error: {ex.Message}";
        }
        finally
        {
            GeneralTestInProgress = false;
        }
    }

    [RelayCommand]
    public void ShowCamera()
    {
        Save();
        CameraWindow cameraWindow = new();
        cameraWindow.Show();
    }

    private List<string> GetVideoDevices()
    {
        List<string> devices = [];
        if (OperatingSystem.IsWindows())
        {
            // Windows-specific code to get video devices
#if WINDOWS
            DsDevice[] systemDeviceEnum = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            devices.AddRange(systemDeviceEnum.Select(device => device.Name));
#endif
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // Unix-based systems code to get video devices
            for (int i = 0; i < 10; ++i)
            {
                string devicePath = $"/dev/video{i}";
                if (File.Exists(devicePath))
                {
                    devices.Add(devicePath);
                }
            }
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
        SaveTaskConfiguration();

        // Update all configuration values
        ConfigurationService.SetConfigurationValue("AIProvider", AiProvider);
        ConfigurationService.SetConfigurationValue("OllamaUrl", OllamaUrl);
        ConfigurationService.SetConfigurationValue("OllamaModel", OllamaModel);
        ConfigurationService.SetConfigurationValue("OpenAIApiKey", OpenAIApiKey);
        ConfigurationService.SetConfigurationValue("OpenAIBaseUrl", OpenAIBaseUrl);
        ConfigurationService.SetConfigurationValue("DefaultTask", DefaultTask);
        ConfigurationService.SetConfigurationValue("System", SystemPrompt);
        ConfigurationService.SetConfigurationValue("UseEmbeddings", UseEmbeddings.ToString());
        ConfigurationService.SetConfigurationValue("StoreAllResponses", StoreAllResponses.ToString());
        ConfigurationService.SetConfigurationValue("AutoMode", AutoMode.ToString());
        ConfigurationService.SetConfigurationValue("VectorDbPath", VectorDbPath.Trim());
        ConfigurationService.SetConfigurationValue("OllamaEmbeddingModel", OllamaEmbeddingModel.Trim());
        ConfigurationService.SetConfigurationValue("OpenAIEmbeddingModel", OpenAIEmbeddingModel.Trim());
        ConfigurationService.SetConfigurationValue("VideoDevice", VideoDevice);
        ConfigurationService.SetConfigurationValue("DefaultLanguage", DefaultLanguage);
        ConfigurationService.SetConfigurationValue("LinuxKeyboardDevice", LinuxKeyboardDevice);
        ConfigurationService.SetConfigurationValue("Threshold", Threshold.ToString());

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