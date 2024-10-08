using System;
using System.Threading;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ClippyAI.ViewModels;
using ClippyAI.Resources;
using DesktopNotifications;
using System.Diagnostics;
namespace ClippyAI.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    public readonly System.Timers.Timer clipboardPollingTimer;
    private readonly INotificationManager _notificationManager;

    public MainWindow()
    {
        InitializeComponent();

        if (Screens.Primary != null)
            Height = Screens.Primary.Bounds.Height - 70;

        // poll clipboard every second
        clipboardPollingTimer = new System.Timers.Timer(1000);
        clipboardPollingTimer.Elapsed += ClipboardPollingTimer_Elapsed;

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

    private void OnNotificationDismissed(object? sender, NotificationDismissedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            string reason = e.Reason.ToString();
            if (reason == "User")
            {
                // abort the ongoing task
                ((MainViewModel)DataContext!).StopClippyTask();
            }
        });
    }

    private void OnNotificationActivated(object? sender, NotificationActivatedEventArgs e)
    {
    }

    public async void ShowNotification(string title, string body, bool showAbortButton = false)
    {
        try
        {
            Debug.Assert(_notificationManager != null);
            Notification nf;

            if(showAbortButton)
            {
                nf = new Notification
                {
                    Title = title,
                    Body = body,
                    Buttons = { (ClippyAI.Resources.Resources.TaskStop, ClippyAI.Resources.Resources.TaskStop) }
                };
            }
            else
            {
                nf = new Notification
                {
                    Title = title,
                    Body = body
                };
            }
            await _notificationManager.ShowNotification(nf);
        }
        catch (Exception)
        {
        }
    }
}