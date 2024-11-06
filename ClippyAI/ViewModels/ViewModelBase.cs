using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Controls.ApplicationLifetimes;
namespace ClippyAI.Views;

public partial class ViewModelBase : ObservableObject
{
    protected ViewModelBase()
    {
        ErrorMessages = [];
    }

    [ObservableProperty]
    private ObservableCollection<string>? _errorMessages;

    protected static async void ShowErrorMessage(string message)
    {
        var dialog = new ErrorMessageDialog
        {
            DataContext = new ErrorMessageDialogViewModel(message)
        };

        if (Avalonia.Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // show main window if it's minimized
            if (desktop.MainWindow!.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                desktop.MainWindow.Show();
                // restore the window if it's minimized
                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
            }
            await dialog.ShowDialog(desktop.MainWindow!);
        }
    }
}
