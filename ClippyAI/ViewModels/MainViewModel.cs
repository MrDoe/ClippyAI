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

namespace ClippyAI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        PopulateTasks();
    }

    [ObservableProperty]
    private string _task = "";

    [ObservableProperty]
    private string? _clipboardContent = "";

    [ObservableProperty]
    private int _caretIndex;

    [ObservableProperty]
    private string? _clippyResponse;

    [ObservableProperty]
    private bool _keyboardOutputSelected = true;

    [ObservableProperty]
    private bool _textBoxOutputSelected;

    [ObservableProperty]
    private ObservableCollection<string> taskItems = [];

    [ObservableProperty]
    private int selectedItemIndex = -1;
    
    [ObservableProperty]
    private string _customTask = "";

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
        ErrorMessages?.Clear();
        string? response = null;
        string task;

        // user defined task
        if(Task == Resources.Resources.Task_15)
            task = CustomTask;
        else
            task = Task;

        if(string.IsNullOrEmpty(task))
            return;
        
        try
        {
            response = await OllamaService.SendRequest(ClipboardContent!,
                                                       task,
                                                       KeyboardOutputSelected,
                                                       token);
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
        }

        if (TextBoxOutputSelected && response != null)
        {
            ClippyResponse = response;
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

    [RelayCommand]
    public void OpenConfiguration(CancellationToken token)
    {
        ErrorMessages?.Clear();
        try
        {
            var window = new ConfigurationWindow();
            window.Show();
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
        }
    }

    // updates the clipboard content regularly
    public async Task UpdateClipboardContent(CancellationToken cancellationToken)
    {
        // Get the clipboard content
        var newContent = await ClipboardService.GetText();

        // Check if the content has changed
        if (newContent != ClipboardContent)
        {
            // Update the property
            ClipboardContent = newContent;
        }
    }
}
