using System.Threading;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClippyAI.ViewModels;
namespace ClippyAI.Views;

public partial class MainWindow : Window
{
    private readonly System.Timers.Timer clipboardPollingTimer;

    public MainWindow()
    {
        InitializeComponent();

        // poll clipboard every 3 seconds
        clipboardPollingTimer = new System.Timers.Timer(3000);
        clipboardPollingTimer.Elapsed += ClipboardPollingTimer_Elapsed;
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        clipboardPollingTimer.Start();
        await ((MainViewModel)DataContext!).PasteText(CancellationToken.None);
    }

    private async void ClipboardPollingTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        // update the data context on the UI thread
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // update clipboard content
            await ((MainViewModel)DataContext!).UpdateClipboardContent(CancellationToken.None);
        });
    }
}