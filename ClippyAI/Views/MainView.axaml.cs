using System.Configuration;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using ClippyAI.Services;
using ClippyAI.ViewModels;
namespace ClippyAI.Views;

public partial class MainView : UserControl
{
    private bool Init = false;
    private bool UrlHasChanged = false;

    public MainView()
    {
        Init = false;
        InitializeComponent();

        // add event handler for task selection changed
        var cboTask = this.FindControl<ComboBox>("cboTask");
        if (cboTask != null)
            cboTask.SelectionChanged += OnCboTaskSelectionChanged;

        // add event handler for language selection changed
        var cboLanguage = this.FindControl<ComboBox>("cboLanguage");
        if (cboLanguage != null)
            cboLanguage.SelectionChanged += OnCboLanguageSelectionChanged;

        // add event handler for model selection changed
        var cboOllamaModel = this.FindControl<ComboBox>("cboOllamaModel");
        if (cboOllamaModel != null)
            cboOllamaModel.SelectionChanged += OnCboOllamaModelSelectionChanged;

        // add event handler for Ollama URL text changed
        var txtOllamaUrl = this.FindControl<TextBox>("txtOllamaUrl");
        if (txtOllamaUrl != null)
        {
            txtOllamaUrl.TextChanged += OnTxtOllamaUrlTextChanged;
            txtOllamaUrl.LostFocus += OnTxtOllamaUrlLostFocus;
        }

        // add event handler for auto mode radio button checked
        var rbAuto = this.FindControl<RadioButton>("rbAuto");
        if (rbAuto != null)
            rbAuto.IsCheckedChanged += OnRbAutoChecked;

        // add event handler for Add Model button click
        var btnAddModel = this.FindControl<Button>("btnAddModel");
        if (btnAddModel != null)
            btnAddModel.Click += OnBtnAddModelClick;

        // add event handler for clipboard content changed
        var txtOutput = this.FindControl<TextBox>("txtOutput");
        if (txtOutput != null)
            txtOutput.TextChanged += OnTxtClipboardContentChanged;
    }

    private async void MainView_Loaded(object? sender, RoutedEventArgs e)
    {        
        Init = true;

        if(Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            ((MainViewModel)DataContext!).mainWindow = (MainWindow)desktop.MainWindow!;

        // set embeddings count
        ((MainViewModel)DataContext!).EmbeddingsCount = await OllamaService.GetEmbeddingsCount();
    }

    private void OnCboLanguageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!Init || e.RemovedItems.Count == 0)
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

    private void OnTxtOllamaUrlTextChanged(object? sender, RoutedEventArgs e)
    {
         if (!Init)
            return;
        
        UrlHasChanged = true;
    }

    private void OnTxtClipboardContentChanged(object? sender, RoutedEventArgs e)
    {
        if (!Init)
            return;

        // update clipboard content in view model
        ((MainViewModel)DataContext!).ClipboardContent = txtOutput.Text!;

        // update clipboard content
        ClipboardService.SetText(txtOutput.Text!);
    }

    private void OnTxtOllamaUrlLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!Init || !UrlHasChanged)
            return;

        var txtOllamaUrl = (TextBox)sender!;
        ((MainViewModel)DataContext!).OllamaUrl = txtOllamaUrl.Text!;

        // update Ollama URL in configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("OllamaUrl");
        config.AppSettings.Settings.Add("OllamaUrl", txtOllamaUrl.Text);
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");

        RestartApplication();
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
        // save default task in configuration file
        ((MainViewModel)DataContext!).Task = selectedItem;
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("DefaultTask");
        config.AppSettings.Settings.Add("DefaultTask", selectedItem);
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private void OnCboOllamaModelSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!Init)
            return;

        var comboBox = (ComboBox)sender!;
        var selectedItem = (string)comboBox.SelectedItem!;
        ((MainViewModel)DataContext!).Model = selectedItem;

        // update Ollama model in configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("OllamaModel");
        config.AppSettings.Settings.Add("OllamaModel", selectedItem);
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private void OnRbAutoChecked(object? sender, RoutedEventArgs e)
    {
        if (!Init)
            return;

        var radiobutton = (RadioButton)sender!;
        bool isChecked = radiobutton.IsChecked ?? false;
        ((MainViewModel)DataContext!).AutoMode = isChecked;

        // save to configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("AutoMode");
        config.AppSettings.Settings.Add("AutoMode", isChecked.ToString());
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private void OnBtnAddModelClick(object? sender, RoutedEventArgs e)
    {
        ((MainViewModel)DataContext!).AddModelCommand.Execute(null);
    }
}
