using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
namespace ClippyAI.Views;

public partial class ViewResultDialog : Window
{
    public ViewResultDialog()
    {
        // position the window above the MainWindow
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Window? mainWindow = desktop.MainWindow;
            if (mainWindow != null)
            {
                Position = new PixelPoint(
                    mainWindow.Position.X - 200,
                    mainWindow.Position.Y + 400);
            }
        }
        InitializeComponent();
        Resized += OnResized;
    }

    private void OnResized(object? sender, WindowResizedEventArgs e)
    {
        Screen? screen = Screens.ScreenFromWindow(this);
        PixelPoint center = screen!.WorkingArea.Center;
        double newX = center.X - (Bounds.Width / 2);
        double newY = center.Y - (Bounds.Height / 2);
        Position = new PixelPoint((int)newX, (int)newY);
    }
}