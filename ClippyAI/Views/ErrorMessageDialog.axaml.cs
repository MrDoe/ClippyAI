using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
namespace ClippyAI.Views;

public partial class ErrorMessageDialog : Window
{
    public ErrorMessageDialog()
    {
        // position the window above the MainWindow
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow != null)
            {
                Position = new PixelPoint(
                    mainWindow.Position.X - 200,
                    mainWindow.Position.Y + 400);
            }
        }
        InitializeComponent();
    }
}