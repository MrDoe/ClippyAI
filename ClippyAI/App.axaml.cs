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
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
        {
            singleViewLifetime.MainView = new MainView { DataContext = new MainViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }
}