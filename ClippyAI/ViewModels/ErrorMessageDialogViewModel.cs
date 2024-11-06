using Avalonia.Controls.ApplicationLifetimes;
using ClippyAI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;
using System.Linq;
using System.Reactive;
namespace ClippyAI.Views;

public partial class ErrorMessageDialogViewModel : ObservableObject
{
    public ErrorMessageDialogViewModel(string errorMessage)
    {
        ErrorMessage = errorMessage;
        CloseCommand = ReactiveCommand.Create(CloseWindow);
    }

    [ObservableProperty]
    private string errorMessage;

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    private void CloseWindow()
    {
        // close the ErrorMessageDialog
        if (Avalonia.Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Windows.FirstOrDefault(w => w is ErrorMessageDialog)?.Close();
        }
    }
}
