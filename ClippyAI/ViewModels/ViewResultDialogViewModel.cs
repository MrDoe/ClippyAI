using Avalonia.Controls.ApplicationLifetimes;
using ClippyAI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;
using System.Linq;
using System.Reactive;
namespace ClippyAI.ViewModels;

public partial class ViewResultDialogViewModel : ObservableObject
{
    public ViewResultDialogViewModel(string resultText)
    {
        ResultText = resultText;
        CloseCommand = ReactiveCommand.Create(CloseWindow);
    }

    [ObservableProperty]
    private string resultText;

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    private void CloseWindow()
    {
        // close the ViewResultDialog
        if (Avalonia.Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Windows.FirstOrDefault(w => w is ViewResultDialog)?.Close();
            
            // hide main window if it's minimized
            if (desktop.MainWindow!.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                desktop.MainWindow.Hide();
            }
        }
    }
}
