using Avalonia.Controls.ApplicationLifetimes;
using ClippyAI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;
using System.Linq;
using System.Reactive;
namespace ClippyAI.ViewModels;

public partial class InputDialogViewModel : ObservableObject
{
    public InputDialogViewModel(string input, string title)
    {
        _input = input;
        _title = title;
        CloseCommand = ReactiveCommand.Create(CloseWindow);
    }

    [ObservableProperty]
    private string _input;

    [ObservableProperty]
    private string _title;

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public ReactiveCommand<Unit, Unit>? SubmitCommand { get; }

    private void CloseWindow()
    {
        // close the InputDialog
        if (Avalonia.Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Windows.FirstOrDefault(w => w is InputDialog)?.Close();
        }
    }
}
