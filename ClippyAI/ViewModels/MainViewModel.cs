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

namespace ClippyAI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _task = "";

    [ObservableProperty]
    private string? _clipboardContent = "";

    [ObservableProperty]
    private int _caretIndex;

    [RelayCommand]
    public async Task AskClippy(CancellationToken token)
    {
        ErrorMessages?.Clear();
        try
        {
            await OllamaService.SendRequest(ClipboardContent!, Task);
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
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
