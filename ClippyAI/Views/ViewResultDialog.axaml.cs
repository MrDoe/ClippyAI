using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
namespace ClippyAI.Views;
public partial class ViewResultDialog : Window
{
    public ViewResultDialog()
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
        this.Resized += OnResized;
    }

    private void OnResized(object? sender, WindowResizedEventArgs e)
    {
        var screen = Screens.ScreenFromVisual(this);
        var center = screen!.WorkingArea.Center;
        var newX = center.X - (this.Bounds.Width / 2);
        var newY = center.Y - (this.Bounds.Height / 2);
        this.Position = new PixelPoint((int)newX, (int)newY);
    }
}