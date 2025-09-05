using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClippyAI.Views;

public partial class ConfigurationDialogViewModel : ViewModelBase
{
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

    // New advanced configuration options
    [ObservableProperty]
    private double _temperature = Convert.ToDouble(ConfigurationManager.AppSettings["Temperature"] ?? "0.8");

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