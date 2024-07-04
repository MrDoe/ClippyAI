using System.Configuration;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ClippyAI.ViewModels;
namespace ClippyAI.Views;

public partial class MainView : UserControl
{
    private bool initialized = false;

    public MainView()
    {
        InitializeComponent();

        // add event handler for task selection changed
        var cboTask = this.FindControl<ComboBox>("cboTask");
        if (cboTask != null)
            cboTask.SelectionChanged += OnCboTaskSelectionChanged;

        // add event handler for language selection changed
        var cboLanguage = this.FindControl<ComboBox>("cboLanguage");
        if (cboLanguage != null)
            cboLanguage.SelectionChanged += OnCboLanguageSelectionChanged;

        initialized = true;
    }

    private void OnCboLanguageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!initialized)
            return;
        if (e.RemovedItems.Count == 0)
            return;

        var comboBox = (ComboBox)sender!;
        var selectedItem = (string)comboBox.SelectedItem!;
        ((MainViewModel)DataContext!).Language = selectedItem;

        // update language in configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("DefaultLanguage");
        config.AppSettings.Settings.Add("DefaultLanguage", selectedItem);
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");

        RestartApplication();
    }

    private void RestartApplication()
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
        Process.Start(new ProcessStartInfo
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
            var mainWindow = (MainWindow)desktop.MainWindow!;
            mainWindow.clipboardPollingTimer.Stop();

            desktop.Shutdown();
        }
    }

    // Event handler for task selection changed
    private void OnCboTaskSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // set custom task text box enabled/disabled based on selected task
        var comboBox = (ComboBox)sender!;
        var selectedItem = (string)comboBox.SelectedItem!;
        var txtCustomTask = this.FindControl<TextBox>("txtCustomTask");
        if (txtCustomTask != null)
        {
            if (selectedItem == ClippyAI.Resources.Resources.Task_15)
            {
                ((MainViewModel)DataContext!).ShowCustomTask = true;
                txtCustomTask.IsEnabled = true;
            }
            else
            {
                ((MainViewModel)DataContext!).ShowCustomTask = false;
                txtCustomTask.IsEnabled = false;
            }
        }
    }
}