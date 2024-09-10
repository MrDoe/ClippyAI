using System;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Concurrency;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using System.Reactive.Disposables;
using ClippyAI.Services;
using System.Collections.Generic;
using ClippyAI.Views;
using Avalonia.Automation.Peers;
using System.Collections.ObjectModel;
using System.Configuration;

namespace ClippyAI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        PopulateTasks();
    }

    private CancellationTokenSource _askClippyCts = new();

    [ObservableProperty]
    private string _task = "";

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
    private int selectedItemIndex = -1;

    [ObservableProperty]
    private string _customTask = "";

    [ObservableProperty]
    private bool _showCustomTask = false;

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private bool _isEnabled = false;

    [ObservableProperty]
    private string _language = ConfigurationManager.AppSettings["DefaultLanguage"] ?? "English";

    [ObservableProperty]
    private ObservableCollection<string> _languageItems = ["English", "Deutsch"];

    [ObservableProperty]
    private string _ollamaUrl = ConfigurationManager.AppSettings["OllamaUrl"] ?? "http://127.0.0.1:11434/api/generate";

    [ObservableProperty]
    private string _model = ConfigurationManager.AppSettings["Model"] ?? "gemma2";

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

        SelectedItemIndex = 0;
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
            ErrorMessages?.Add("Please select a task.");
            IsBusy = false;
            return;
        }

        try
        {
            response = await OllamaService.SendRequest(ClipboardContent!,
                                                       task,
                                                       KeyboardOutputSelected,
                                                       _askClippyCts.Token); // Use the token from _askClippyCts
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
        var newContent = await ClipboardService.GetText();

        // Check if the content has changed
        if (newContent != ClipboardContent)
        {
            IsBusy = true;

            // Update the property
            ClipboardContent = newContent;

            if(IsEnabled)
            {
                await AskClippy(cancellationToken);
            }

            IsBusy = false;
        }
    }
}
