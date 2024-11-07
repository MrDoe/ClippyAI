using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClippyAI.Services;
using ClippyAI.Views;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Collections.Generic;
using ClippyAI.Models;
namespace ClippyAI.Views;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        PopulateTasks();
    }

    public MainWindow? mainWindow;
    private CancellationTokenSource _askClippyCts = new();
    private bool initialized = false;

    [ObservableProperty]
    private string _task = ConfigurationManager.AppSettings["DefaultTask"] ?? Resources.Resources.Task_1;

    [ObservableProperty]
    private string? _clipboardContent = "";

    [ObservableProperty]
    private string? _input;

    [ObservableProperty]
    private string? _output;

    [ObservableProperty]
    private int _caretIndex;

    [ObservableProperty]
    private string? _clippyResponse;

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
    private string _PostgreSqlConnection = ConfigurationManager.AppSettings["PostgreSqlConnection"] ?? "";

    [ObservableProperty]
    private string _PostgresOllamaUrl = ConfigurationManager.AppSettings["PostgresOllamaUrl"] ?? "";

    [ObservableProperty]
    private bool _useEmbeddings = ConfigurationManager.AppSettings["UseEmbeddings"] == "True";

    [ObservableProperty]
    private bool _storeAllResponses = ConfigurationManager.AppSettings["StoreAllResponses"] == "True";

    [ObservableProperty]
    private string _responseCounter = "0 / 0";

    [ObservableProperty]
    private float _threshold = 8.0f;

    [ObservableProperty]
    private int _embeddingsCount = 0;

    [ObservableProperty]
    private bool _useAsRefinement = false;

    private bool _lastOutputGenerated = false;

    /// <summary>
    /// List of responseList retrieved from vector database
    /// </summary>
    private List<Embedding> _responseList = [];

    /// <summary>
    /// Index of the current response in responseList
    /// </summary>
    private int _responseIndex = 0;

    /// <summary>
    /// Full task string for storage in database
    /// </summary>
    private string _fullTask = "";

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
    private string GetFullTask()
    {
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
            return "";
        }
        _fullTask = Task + " " + Input!;

        return task;
    }

    [RelayCommand]
    public async Task AskClippy(CancellationToken token)
    {
        IsBusy = true;
        ErrorMessages?.Clear();
        _responseList.Clear();
        string? response = null;
        _lastOutputGenerated = false;

        // get task string
        string task = GetFullTask();
        if(string.IsNullOrEmpty(task))
        {
            return;
        }

        try
        {
            string model = ModelItems[ModelItems.IndexOf(Model)];
            if (model == null)
            {
                ErrorMessages?.Add(Resources.Resources.SelectModel);
                IsBusy = false;
                return;
            }
       
            mainWindow!.ShowNotification("ClippyAI", Resources.Resources.PleaseWait, true, false);

            // try to get response from embedded model first
            if (UseEmbeddings)
            {
                _responseList = await OllamaService.RetrieveAnswersForQuestion(_fullTask, Threshold);
                if (_responseList.Count > 0)
                {
                    _responseIndex = 0;
                    await SetResponse(_responseList[0].Answer);
                    return;
                }
            }

            response = await OllamaService.SendRequest(Input!,
                                                       task,
                                                       model,
                                                       _askClippyCts.Token); // Use the token from _askClippyCts
            
            if (!string.IsNullOrEmpty(response) && StoreAllResponses)
            {
                await OllamaService.StoreEmbedding(_fullTask, response);
                ++EmbeddingsCount;
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

        if (response != null)
        {
            _lastOutputGenerated = true;
            _responseList.Add(new Embedding { Id = 0, Answer = response });
            await SetResponse(response);
        }
    }

    private async Task SetResponse(string response, bool showNotification = true)
    {
        ClipboardContent = response;
        Output = response;

        // Update the clipboard content
        await ClipboardService.SetText(ClipboardContent);

        // call ShowNotification method from MainWindow
        if (showNotification)
        {
            mainWindow!.HideLastNotification();
            mainWindow.ShowNotification("ClippyAI", Resources.Resources.TaskCompleted + response, false, true);
        }

        // update response counter
        if(_lastOutputGenerated)
            ResponseCounter = "      *";
        else
            ResponseCounter = $"{_responseIndex + 1} / {_responseList.Count}";

        IsBusy = false;
    }

    /// <summary>
    /// Get next response from the list of responseList
    /// </summary>
    [RelayCommand]
    public async Task GetNextResponse()
    {
        if(_lastOutputGenerated)
        {
            return;
        }

        if (_responseList.Count > 0)
        {
            _responseIndex++;
            if (_responseIndex >= _responseList.Count)
            {
                _responseIndex = 0;
            }
            await SetResponse(_responseList[_responseIndex].Answer, false);
        }
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
            if (e is InvalidOperationException)
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
            Input = newContent ?? "";

            if (AutoMode && initialized)
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
        try
        {
            // open input dialog to enter model name
            string? modelName = await InputDialog.Prompt(
                    parentWindow: mainWindow!,
                    title: Resources.Resources.PullModel,
                    caption: Resources.Resources.EnterModelName,
                    subtext: Resources.Resources.PullModelSubText,
                    isRequired: true
                );
            if (string.IsNullOrEmpty(modelName))
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
        // confirm deletion
        string? confirmation = await InputDialog.Confirm(
            parentWindow: mainWindow!,
            title: Resources.Resources.DeleteModel,
            caption: Resources.Resources.ConfirmDeleteModel
        );
        if (confirmation != Resources.Resources.Yes)
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

    [RelayCommand]
    public async Task ThumbUp()
    {
        if(string.IsNullOrEmpty(Output))
        {
            return;
        }

        if (_responseList.Count > 0 && _responseIndex >= 0)
        {
            try
            {
                GetFullTask();
                await OllamaService.StoreEmbedding(_fullTask, Output);
            }
            catch (Exception e)
            {
                ErrorMessages?.Add(e.Message);
                mainWindow!.ShowNotification("ClippyAI", e.Message, false, false);
                return;
            }

            ++EmbeddingsCount;
            mainWindow!.ShowNotification("ClippyAI", "Result successfully stored as template in database!", false, false);
        }
    }

    [RelayCommand]
    public async Task Regenerate()
    {
        UseEmbeddings = false;
        await AskClippy(_askClippyCts.Token);
        UseEmbeddings = true;
    }

    [RelayCommand]
    public async Task ClearEmbeddings()
    {
        // confirm deletion
        string? confirmation = await InputDialog.Confirm(
            parentWindow: mainWindow!,
            title: "Delete Embeddings",
            caption: "Do you really want to delete all embeddings?"
        );

        if (confirmation != Resources.Resources.Yes)
        {
            return;
        }

        try
        {
            await OllamaService.ClearEmbeddings();
            EmbeddingsCount = 0;
            mainWindow!.ShowNotification("ClippyAI", "Embeddings successfully deleted!", false, false);
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
            ShowErrorMessage(e.Message);
        }
    }
}
