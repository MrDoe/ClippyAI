using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Globalization;
using ClippyAI.ViewModels;
using ClippyAI.Views;
using ClippyAI.Resources;
using System.Configuration;
namespace ClippyAI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        string language = ConfigurationManager.AppSettings["DefaultLanguage"] ?? "English";
        ClippyAI.Resources.Resources.Culture = new CultureInfo(language == "Deutsch" ? "de-DE" : "en-US");
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}