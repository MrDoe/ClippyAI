using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
namespace ClippyAI.Views;
public partial class ApplicationViewModel : ViewModelBase
{
    [RelayCommand]
    public void ShowWindow()
    {
        // show the main window
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            //restore window from minimized state if it is minimized
            if (desktop.MainWindow!.WindowState == WindowState.Minimized)
            {
                desktop.MainWindow.WindowState = WindowState.Normal;
            }
            desktop.MainWindow.Show();
        }
    }

    [RelayCommand]
    public void HideWindow()
    {
        // hide the main window
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Hide();
        }
    }

    [RelayCommand]
    public void CloseWindow()
    {
        // close the main window
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Close();
        }
    }
}
