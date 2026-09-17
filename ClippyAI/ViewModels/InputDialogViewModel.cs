using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;
using System.Linq;
using ReactiveUI.Primitives;
namespace ClippyAI.Views;

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

    public ReactiveCommand<RxVoid, RxVoid> CloseCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid>? SubmitCommand { get; }

    private void CloseWindow()
    {
        // close the InputDialog
        if (Avalonia.Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Windows.FirstOrDefault(w => w is InputDialog)?.Close();
        }
    }
}
