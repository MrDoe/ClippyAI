using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
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
        ErrorMessageDialog dialog = new()
        {
            DataContext = new ErrorMessageDialogViewModel(message)
        };

        if (Avalonia.Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // show main window if it's minimized
            desktop.MainWindow!.Show();
            if (desktop.MainWindow!.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                // restore the window if it's minimized
                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
            }
            await dialog.ShowDialog(desktop.MainWindow!);
        }
    }
}
