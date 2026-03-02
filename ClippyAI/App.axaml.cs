using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ClippyAI.Services;
using ClippyAI.Views;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Globalization;
using System.Text;
namespace ClippyAI;

public partial class App : Application
{
    public static new App? Current { get; private set; }

    public App()
    {
        Current = this;
        DataContext = new ApplicationViewModel();
    }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
    private SSHService? _sshService;

    /// <summary>
    /// Reinitialize services after configuration changes (SSH, Embeddings, etc.)
    /// </summary>
    public void ReinitializeServices()
    {
        try
        {
            // Reinitialize SSH connection
            _sshService?.Disconnect();
            _sshService = new SSHService();
            _sshService.Connect();
            Console.WriteLine("SSH service reinitialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reinitializing SSH: {ex.Message}");
            _mainWindow?.ShowNotification("ClippyAI", $"SSH reinitialization failed: {ex.Message}", false, false);
        }

        try
        {
            // Reinitialize embeddings if enabled
            if (ConfigurationService.GetConfigurationValue("UseEmbeddings") == "True")
            {
                OllamaService.InitializeEmbeddings();
                Console.WriteLine("Embeddings service reinitialized");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reinitializing embeddings: {ex.Message}");
            _mainWindow?.ShowNotification("ClippyAI", $"Embeddings reinitialization failed: {ex.Message}", false, false);
        }
    }

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        if (_mainWindow!.WindowState == WindowState.Minimized || !_mainWindow.IsVisible)
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
        }
        else
        {
            _mainWindow.Hide();
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Initialize configuration database first
        ClippyAI.Services.ConfigurationService.InitializeDatabase();

        string language = ConfigurationService.GetConfigurationValue("DefaultLanguage", "English");
        language = language.Normalize(NormalizationForm.FormC); // Normalize the input string

        switch (language)
        {
            case string lang when lang.Equals("English", StringComparison.OrdinalIgnoreCase):
                ClippyAI.Resources.Resources.Culture = new CultureInfo("en-US");
                break;
            case string lang when lang.Equals("Deutsch", StringComparison.OrdinalIgnoreCase):
                ClippyAI.Resources.Resources.Culture = new CultureInfo("de-DE");
                break;
            case string lang when lang.Equals("Français", StringComparison.OrdinalIgnoreCase):
                ClippyAI.Resources.Resources.Culture = new CultureInfo("fr-FR");
                break;
            case string lang when lang.Equals("Español", StringComparison.OrdinalIgnoreCase):
                ClippyAI.Resources.Resources.Culture = new CultureInfo("es-ES");
                break;
            default:
                // Handle unknown languages if necessary
                break;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Initialize SSHService
            _sshService = new SSHService();
            _sshService.Connect();

            _mainWindow = new MainWindow();
            desktop.MainWindow = _mainWindow;

            Uri iconUri = new("avares://ClippyAI/Assets/bulb.ico");
            Bitmap bitmap = new(AssetLoader.Open(iconUri));

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(bitmap),
                ToolTipText = "ClippyAI",
                Menu = []
            };

            _trayIcon.Menu.Items.Add(new NativeMenuItem
            {
                Header = ClippyAI.Resources.Resources.Show,
                Command = new RelayCommand(() =>
                {
                    if (_mainWindow.WindowState == WindowState.Minimized || !_mainWindow.IsVisible)
                    {
                        _mainWindow.Show();
                        _mainWindow.WindowState = WindowState.Normal;
                    }
                    else
                    {
                        _mainWindow.Hide();
                    }
                })
            });

            _trayIcon.Menu.Items.Add(new NativeMenuItem
            {
                Header = ClippyAI.Resources.Resources.Exit,
                Command = new RelayCommand(() =>
                {
                    _mainWindow.Close();
                })
            });
            _trayIcon.Clicked += TrayIcon_Clicked;
            _trayIcon.IsVisible = true;

            // initialize the Ollama Embedding Service
            if (ConfigurationService.GetConfigurationValue("UseEmbeddings") == "True")
            {
                try
                {
                    OllamaService.InitializeEmbeddings();
                }
                catch (Exception ex)
                {
                    _mainWindow.ShowNotification("ClippyAI", ex.Message, false, true);
                }
            }
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
        {
            singleViewLifetime.MainView = new MainView { DataContext = new MainViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }

}
