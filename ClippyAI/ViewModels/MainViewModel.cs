using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClippyAI.Services;
using ClippyAI.Views;
using System.Collections.ObjectModel;
using System.Configuration;
namespace ClippyAI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        PopulateTasks();
        StoreAsEmbeddings = false; // Initialize the property
    }

    private CancellationTokenSource _askClippyCts = new();
    private bool initialized = false;

    [ObservableProperty]
    private string _task = ConfigurationManager.AppSettings["DefaultTask"] ?? Resources.Resources.Task_1;

    [ObservableProperty]
    private string? _clipboardContent = "";

    [ObservableProperty]
    private int _caretIndex;

    [ObservableProperty]
    private string? _clippyResponse;

    [ObservableProperty]
    private bool _keyboardOutputSelected = false;

    [ObservableProperty]
    private bool _textBoxOutputSelected = true;

    [ObservableProperty]
    private ObservableCollection<string> taskItems = [];

    [ObservableProperty]
    private string _customTask = "";

    [ObservableProperty]
    private bool _showCustomTask = false;

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private bool _autoMode = Convert.ToBoolean(ConfigurationManager.AppSettings["AutoMode"]);

    [ObservableProperty]
    private string _language = ConfigurationManager.AppSettings["DefaultLanguage"] ?? "English";

    [ObservableProperty]
    private ObservableCollection<string> _languageItems = ["English", "Deutsch", "Français", "Español"];

    [ObservableProperty]
    private ObservableCollection<string> _modelItems = OllamaService.GetModels();

    [ObservableProperty]
    private string _ollamaUrl = ConfigurationManager.AppSettings["OllamaUrl"] ?? "http://127.0.0.1:11434/api";

    [ObservableProperty]
    private string _model = ConfigurationManager.AppSettings["OllamaModel"] ?? "gemma2:latest";

    [ObservableProperty]
    private bool _storeAsEmbeddings = false;

    private void PopulateTasks()
    {
        // iterate over Resources and add Tasks to ComboBox
        foreach (var property in typeof(Resources.Resources).GetProperties())
        {
            if (property.Name.StartsWith("Task_"))
            {
                var value = property.GetValue(null)?.ToString();
                if (value != null)
                {
                    TaskItems.Add(value);
                }
            }
        }       
    }

    [RelayCommand]
    public async Task AskClippy(CancellationToken token)
    {
        IsBusy = true;
        ErrorMessages?.Clear();
        string? response = null;
        string task;

        // user defined task
        if (Task == Resources.Resources.Task_15)
            task = CustomTask;
        else
            task = Task;

        if (string.IsNullOrEmpty(task))
        {
            ErrorMessages?.Add(Resources.Resources.SelectTask);
            IsBusy = false;
            return;
        }

        try
        {
            string model = ModelItems[ModelItems.IndexOf(Model)];
            if(model == null)
            {
                ErrorMessages?.Add(Resources.Resources.SelectModel);
                IsBusy = false;
                return;
            }

            // call ShowNotification method from MainWindow
            if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = (MainWindow)desktop.MainWindow!;
                mainWindow.ShowNotification("ClippyAI", Resources.Resources.PleaseWait, true, false);
            }

            response = await OllamaService.SendRequest(ClipboardContent!,
                                                       task,
                                                       model,
                                                       KeyboardOutputSelected,
                                                       _askClippyCts.Token); // Use the token from _askClippyCts

            if (!string.IsNullOrEmpty(response) && StoreAsEmbeddings)
            {
                string question = task + " " + ClipboardContent!;
                await OllamaService.StoreEmbedding(question, response);
            }
        }
        catch (OperationCanceledException)
        {
            // Handle the task cancellation here if needed
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);

            // show error message in dialog
            ShowErrorMessage(e.Message);
        }

        if (TextBoxOutputSelected && response != null)
        {
            ClipboardContent = response;
            
            // Update the clipboard content
            await ClipboardService.SetText(ClipboardContent);

            // call ShowNotification method from MainWindow
            if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = (MainWindow)desktop.MainWindow!;
                mainWindow.HideLastNotification();
                mainWindow.ShowNotification("ClippyAI", Resources.Resources.TaskCompleted + response, false, true);
            }
        }
        IsBusy = false;
    }

    [RelayCommand]
    public void StopClippyTask()
    {
        if (!_askClippyCts.IsCancellationRequested)
        {
            _askClippyCts.Cancel(); // Cancel the ongoing task
            _askClippyCts = new CancellationTokenSource(); // Reset the CancellationTokenSource for future use
        }
    }

    [RelayCommand]
    public async Task CopyText(CancellationToken token)
    {
        ErrorMessages?.Clear();
        try
        {
            await ClipboardService.SetText(ClipboardContent);
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
        }
    }

    [RelayCommand]
    public async Task PasteText(CancellationToken token)
    {
        ErrorMessages?.Clear();
        try
        {
            if (await ClipboardService.GetText() is { } pastedText)
                ClipboardContent = ClipboardContent?.Insert(CaretIndex, pastedText);
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
        }
    }

    // updates the clipboard content regularly
    public async Task UpdateClipboardContent(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        // Get the clipboard content
        string? newContent;
        try 
        {
            newContent = await ClipboardService.GetText();
        }
        catch (Exception e)
        {
            if(e is InvalidOperationException)
            {
                // Ignore the exception if the clipboard does not contain text
                return;
            }
            else
            {
                ErrorMessages?.Add(e.Message);
                return;
            }
        }
        // Check if the content has changed
        if (newContent != ClipboardContent)
        {
            IsBusy = true;

            // Update the property
            ClipboardContent = newContent;

            if(AutoMode && initialized)
            {
                await AskClippy(cancellationToken);
            }
            else
            {
                initialized = true;
            }

            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task AddModel()
    {
        MainWindow? mainWindow;
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainWindow = (MainWindow)desktop.MainWindow!;
        }
        else
        {
            return;
        }

        try
        {
            // open input dialog to enter model name
            string? modelName = await InputDialog.Prompt(
                    parentWindow: mainWindow,
                    title: Resources.Resources.PullModel,
                    caption: Resources.Resources.EnterModelName,
                    subtext: Resources.Resources.PullModelSubText,
                    isRequired: true
                );
            if(string.IsNullOrEmpty(modelName))
            {
                return;
            }

            await OllamaService.PullModelAsync(modelName);

            // update model items
            ModelItems = OllamaService.GetModels();
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
            ShowErrorMessage(e.Message);
        }
    }

    [RelayCommand]
    public async Task DeleteModel()
    {
        MainWindow? mainWindow;
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainWindow = (MainWindow)desktop.MainWindow!;
        }
        else
        {
            return;
        }

        // confirm deletion
        string? confirmation = await InputDialog.Confirm(
            parentWindow: mainWindow,
            title: Resources.Resources.DeleteModel,
            caption: Resources.Resources.ConfirmDeleteModel
        );
        if (confirmation != "OK")
        {
            return;
        }

        try
        {
            // get selected model
            string modelName = ModelItems[ModelItems.IndexOf(Model)];

            await OllamaService.DeleteModelAsync(modelName);

            // update model items
            ModelItems = OllamaService.GetModels();
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
            ShowErrorMessage(e.Message);
        }
    }
}
