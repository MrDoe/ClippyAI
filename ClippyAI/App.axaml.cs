using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Globalization;
using ClippyAI.ViewModels;
using ClippyAI.Views;
using System.Configuration;
using System.Text;
using System;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
namespace ClippyAI;

public partial class App : Application
{
    public App()
    {
        DataContext = new ApplicationViewModel();
    }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    private Window _mainWindow;
    private TrayIcon _trayIcon;

    private void TrayIcon_Clicked(object? sender, EventArgs e)
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
    }

    public override void OnFrameworkInitializationCompleted()
    {
        string language = ConfigurationManager.AppSettings["DefaultLanguage"] ?? "English";
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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            desktopLifetime.MainWindow = new MainWindow { DataContext = new MainViewModel() };
            _mainWindow = desktopLifetime.MainWindow;

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon("Assets/bulb.png"),
                ToolTipText = "ClippyAI",
                Menu = []
            };

            _trayIcon.Menu.Items.Add(new NativeMenuItem
            {
                Header = "Show",
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
                Header = "Exit",
                Command = new RelayCommand(() =>
                {
                    _mainWindow.Close();
                })
            });
            _trayIcon.Clicked += TrayIcon_Clicked;
            _trayIcon.IsVisible = true;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
        {
            singleViewLifetime.MainView = new MainView { DataContext = new MainViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }


}