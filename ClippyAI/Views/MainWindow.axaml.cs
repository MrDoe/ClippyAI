using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using ClippyAI.Desktop;
using ClippyAI.ViewModels;
using ReactiveUI;
using DesktopNotifications;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
namespace ClippyAI.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    public readonly System.Timers.Timer clipboardPollingTimer;
    private readonly INotificationManager _notificationManager;

    private Notification? _lastNotification;
    public MainWindow()
    {
        InitializeComponent();

        if (Screens.Primary != null)
            Height = Screens.Primary.Bounds.Height;

        // poll clipboard every 3 seconds
        clipboardPollingTimer = new System.Timers.Timer(1000);
        clipboardPollingTimer.Elapsed += ClipboardPollingTimer_Elapsed;

        // get notification manager from ClippyAI.Desktop.Program
        _notificationManager = Program.NotificationManager ??
                                throw new InvalidOperationException("Missing notification manager");
        _notificationManager.NotificationActivated += OnNotificationActivated;
        _notificationManager.NotificationDismissed += OnNotificationDismissed;

    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // set window position to bottom right corner
        SetWindowPos();
        clipboardPollingTimer.Start();
        
        PositionChanged += MainWindow_PositionChanged;
        Resized += MainWindow_Resized;
    }

    private void SetWindowPos()
    {
        // set window position to bottom right corner
        if (Screens.Primary != null)
        {
            // get primary screen size
            PixelSize screenSize = Screens.Primary.Bounds.Size;
            PixelSize windowSize = PixelSize.FromSize(ClientSize, Screens.Primary.Scaling);

            Position = new PixelPoint(
              screenSize.Width - windowSize.Width,
              0);

            Height = screenSize.Height;
        }
    }
    private void MainWindow_Resized(object? sender, EventArgs e)
    {
        SetWindowPos();
    }

    private void MainWindow_PositionChanged(object? sender, EventArgs e)
    {
        SetWindowPos();
    }

    private async void ClipboardPollingTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            // update the data context on the UI thread
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // update clipboard content
                await ((MainViewModel)DataContext!).UpdateClipboardContent(CancellationToken.None);
            });
        }
        catch (Exception)
        {
            // ignore
        }
    }
}