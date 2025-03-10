using System.Configuration;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using ClippyAI.Services;
using ClippyAI.Views;
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

        // add event handler for automatic mode radio button checked
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
        
        // add event handler for PostgreSqlConnection text changed
        var txtPostgreSqlConnection = this.FindControl<TextBox>("txtPostgreConnection");
        if (txtPostgreSqlConnection != null)
            txtPostgreSqlConnection.TextChanged += OnTxtPostgreConnectionChanged;

        // add event handler for PostgresOllamaUrl text changed
        var txtPostgresOllamaUrl = this.FindControl<TextBox>("txtPostgresOllamaUrl");
        if (txtPostgresOllamaUrl != null)
            txtPostgresOllamaUrl.TextChanged += OnTxtPostgresOllamaUrlChanged;

        // add event hanlder for UseEmbeddings
        var chkUseEmbeddings = this.FindControl<CheckBox>("chkUseEmbeddings");
        if (chkUseEmbeddings != null)
            chkUseEmbeddings.IsCheckedChanged += OnChkUseEmbeddingsChecked;

        // add event handler for StoreAllResponses checkbox checked
        var chkStoreAllResponses = this.FindControl<CheckBox>("chkStoreAllResponses");
        if (chkStoreAllResponses != null)
            chkStoreAllResponses.IsCheckedChanged += OnChkStoreAllResponsesChecked;

        // add event handler for video device text changed
        var txtVideoDevice = this.FindControl<TextBox>("txtVideoDevice");
        if (txtVideoDevice != null)
            txtVideoDevice.TextChanged += OnTxtVideoDeviceChanged;

        // add event handler for vision model text changed
        var txtVisionModel = this.FindControl<TextBox>("txtVisionModel");
        if (txtVisionModel != null)
            txtVisionModel.TextChanged += OnTxtVisionModelChanged;

        // add event handler for vision prompt text changed
        var txtVisionPrompt = this.FindControl<TextBox>("txtVisionPrompt");
        if (txtVisionPrompt != null)
            txtVisionPrompt.TextChanged += OnTxtVisionPromptChanged;

        // add event handler for video device selection changed
        var cboVideoDevice = this.FindControl<ComboBox>("cboVideoDevice");
        if (cboVideoDevice != null)
            cboVideoDevice.SelectionChanged += OnCboVideoDeviceSelectionChanged;

        var btnShowCamera = this.FindControl<Button>("btnShowCamera");
        if (btnShowCamera != null)
            btnShowCamera.Click += OnBtnShowCameraClick;
    }

    private async void MainView_Loaded(object? sender, RoutedEventArgs e)
    {        
        Init = true;

        if(Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            ((MainViewModel)DataContext!).mainWindow = (MainWindow)desktop.MainWindow!;

        // set embeddings count
        int embeddingsCount = await OllamaService.GetEmbeddingsCount();
        if(embeddingsCount >= 0)
            ((MainViewModel)DataContext!).EmbeddingsCount = embeddingsCount;

        // Start monitoring clipboard content
        var viewModel = (MainViewModel)DataContext!;
        var cancellationTokenSource = new CancellationTokenSource();
        while (true)
        {
            await viewModel.UpdateClipboardContent(cancellationTokenSource.Token);
            await Task.Delay(1000); // Poll every second
        }
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

    private void OnTxtPostgreConnectionChanged(object? sender, RoutedEventArgs e)
    {
        if (!Init)
            return;

        var txtPostgreSqlConnection = (TextBox)sender!;
        ((MainViewModel)DataContext!).PostgreSqlConnection = txtPostgreSqlConnection.Text!;

        // update PostgreSqlConnection in configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("PostgreSqlConnection");
        config.AppSettings.Settings.Add("PostgreSqlConnection", txtPostgreSqlConnection.Text);
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private void OnTxtPostgresOllamaUrlChanged(object? sender, RoutedEventArgs e)
    {
        if (!Init)
            return;

        var txtPostgresOllamaUrl = (TextBox)sender!;
        ((MainViewModel)DataContext!).PostgresOllamaUrl = txtPostgresOllamaUrl.Text!;

        // update PostgresOllamaUrl in configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("PostgresOllamaUrl");
        config.AppSettings.Settings.Add("PostgresOllamaUrl", txtPostgresOllamaUrl.Text);
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private void OnChkStoreAllResponsesChecked(object? sender, RoutedEventArgs e)
    {
        if (!Init)
            return;

        var chkStoreAllResponses = (CheckBox)sender!;
        bool isChecked = chkStoreAllResponses.IsChecked ?? false;
        ((MainViewModel)DataContext!).StoreAllResponses = isChecked;

        // save to configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("StoreAllResponses");
        config.AppSettings.Settings.Add("StoreAllResponses", isChecked.ToString());
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private void OnChkUseEmbeddingsChecked(object? sender, RoutedEventArgs e)
    {
        if (!Init)
            return;

        var chkUseEmbeddings = (CheckBox)sender!;
        bool isChecked = chkUseEmbeddings.IsChecked ?? false;
        ((MainViewModel)DataContext!).UseEmbeddings = isChecked;

        // save to configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("UseEmbeddings");
        config.AppSettings.Settings.Add("UseEmbeddings", isChecked.ToString());
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private void OnTxtVideoDeviceChanged(object? sender, RoutedEventArgs e)
    {
        if (!Init)
            return;

        var txtVideoDevice = (TextBox)sender!;
        ((MainViewModel)DataContext!).VideoDevice = txtVideoDevice.Text!;

        // update VideoDevice in configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("VideoDevice");
        config.AppSettings.Settings.Add("VideoDevice", txtVideoDevice.Text);
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private void OnTxtVisionModelChanged(object? sender, RoutedEventArgs e)
    {
        if (!Init)
            return;

        var txtVisionModel = (TextBox)sender!;
        ((MainViewModel)DataContext!).VisionModel = txtVisionModel.Text!;

        // update VisionModel in configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("VisionModel");
        config.AppSettings.Settings.Add("VisionModel", txtVisionModel.Text);
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private void OnTxtVisionPromptChanged(object? sender, RoutedEventArgs e)
    {
        if (!Init)
            return;

        var txtVisionPrompt = (TextBox)sender!;
        ((MainViewModel)DataContext!).VisionPrompt = txtVisionPrompt.Text!;

        // update VisionPrompt in configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("VisionPrompt");
        config.AppSettings.Settings.Add("VisionPrompt", txtVisionPrompt.Text);
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private void OnCboVideoDeviceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!Init)
            return;

        var comboBox = (ComboBox)sender!;
        var selectedItem = (string)comboBox.SelectedItem!;
        ((MainViewModel)DataContext!).VideoDevice = selectedItem;

        // update VideoDevice in configuration file
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Remove("VideoDevice");
        config.AppSettings.Settings.Add("VideoDevice", selectedItem);
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private void OnBtnShowCameraClick(object? sender, RoutedEventArgs e)
    {
        ((MainViewModel)DataContext!).ShowCameraCommand.Execute(null);
    }
}
