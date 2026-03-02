using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ClippyAI.Services;
using System.Diagnostics;
namespace ClippyAI.Views;

public partial class MainView : UserControl
{
    private bool Init = false;

    public MainView()
    {
        Init = false;
        InitializeComponent();

        // add event handler for language selection changed
        ComboBox? cboLanguage = this.FindControl<ComboBox>("cboLanguage");
        cboLanguage?.SelectionChanged += OnCboLanguageSelectionChanged;

        // add event handler for clipboard content changed
        TextBox? txtOutput = this.FindControl<TextBox>("txtOutput");
        txtOutput?.TextChanged += OnTxtClipboardContentChanged;

        Button? btnShowCamera = this.FindControl<Button>("btnShowCamera");
        btnShowCamera?.Click += OnBtnShowCameraClick;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        Init = true;

        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ((MainViewModel)DataContext!).mainWindow = (MainWindow)desktop.MainWindow!;
        }

        // set embeddings count
        int embeddingsCount = await OllamaService.GetEmbeddingsCount();
        if (embeddingsCount >= 0)
        {
            ((MainViewModel)DataContext!).EmbeddingsCount = embeddingsCount;
        }

        // Note: Clipboard monitoring is now handled by MainWindow timer to reduce CPU load
        // Removed duplicate polling loop that was running every 1000ms
    }

    private void OnCboLanguageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!Init || e.RemovedItems.Count == 0)
        {
            return;
        }

        ComboBox comboBox = (ComboBox)sender!;
        string selectedItem = (string)comboBox.SelectedItem!;
        ((MainViewModel)DataContext!).Language = selectedItem;

        // update language in configuration database
        ConfigurationService.SetConfigurationValue("DefaultLanguage", selectedItem);

        RestartApplication();
    }

    private void OnTxtClipboardContentChanged(object? sender, RoutedEventArgs e)
    {
        if (!Init)
        {
            return;
        }
    }

    private static void RestartApplication()
    {
        // get the full path to the dotnet executable
        string? fullPath = Process.GetCurrentProcess()?.MainModule?.FileName;

        // Get the full path to the entry assembly (your application's DLL)
        string? entryAssemblyPath = System.Reflection.Assembly.GetEntryAssembly()?.Location;

        // command dependent on the OS
        string fileName = "";
        string command = "";
        if (System.Environment.OSVersion.Platform == System.PlatformID.Win32NT)
        {
            fileName = "cmd.exe";
            command = "\"" + fullPath + "\" \"" + entryAssemblyPath + "\"";
            command = $"/C \"{command}\"";
        }

        if (System.Environment.OSVersion.Platform == System.PlatformID.Unix)
        {
            fileName = "/bin/bash";
            command = $"{fullPath} {entryAssemblyPath}";
            command = $"-c \"{command}\"";
        }

        // Start a new process with the combined command
        _ = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = command,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        // Close the current application instance
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // stop clipboard polling timer
            MainWindow mainWindow = (MainWindow)desktop.MainWindow!;
            mainWindow.clipboardPollingTimer.Stop();

            desktop.Shutdown();
        }
    }

    private void OnBtnShowCameraClick(object? sender, RoutedEventArgs e)
    {
        ((MainViewModel)DataContext!).ShowCameraCommand.Execute(null);
    }
}
