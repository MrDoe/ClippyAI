using System;
using System.Threading;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using DesktopNotifications;
using System.Diagnostics;
using Avalonia.Markup.Xaml;
using ClippyAI.Services;

#if WINDOWS
using System.Runtime.Versioning;
#endif

#if LINUX
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
#endif

namespace ClippyAI.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    public readonly System.Timers.Timer clipboardPollingTimer;
    private readonly INotificationManager _notificationManager;
    private Notification? _lastNotification { get; set; }
    
#if WINDOWS
    private WindowsHotkeyService? _windowsHotkeyService;
#endif

#if LINUX
    private HotkeyService? HotkeyService;
#endif

    public new string Title
    {
        get => base.Title!;
        set => base.Title = value;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        // poll clipboard every second (reduced from 500ms to reduce CPU load)
        clipboardPollingTimer = new System.Timers.Timer(1000);
        clipboardPollingTimer.Elapsed += ClipboardPollingTimer_Elapsed;

        // register notification manager
        _notificationManager = Program.NotificationManager ??
                                throw new InvalidOperationException("Missing notification manager");
        
        // Only register notification events on Windows
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            _notificationManager.NotificationActivated += OnNotificationActivated;
            _notificationManager.NotificationDismissed += OnNotificationDismissed;
        }

        // Subscribe to the WindowStateChanged event
        PropertyChanged += MainWindow_PropertyChanged;

#if WINDOWS        
        if (OperatingSystem.IsWindows())
            RegisterHotkeyWindows();
#elif LINUX
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            RegisterHotkeyLinux();
#endif
    }

#if WINDOWS
    [SupportedOSPlatform("windows7.0")]
    private void RegisterHotkeyWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            _windowsHotkeyService = new WindowsHotkeyService(this);
            bool success = _windowsHotkeyService.RegisterHotkeys();
            
            if (!success)
            {
                Console.WriteLine("Failed to register Windows hotkeys using native API");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register Windows hotkeys: {ex.Message}");
        }
    }
#endif

#if LINUX
    [SupportedOSPlatform("linux")]
    private void RegisterHotkeyLinux()
    {
        HotkeyService = new HotkeyService(this);
    }

    private async void OnAnalyzeVideoHotkeyHandler(object? sender, EventArgs e)
    {
        Console.WriteLine("Analyze video hotkey pressed");

        // execute relay command AnalyzeVideo
        await ((MainViewModel)DataContext!).CaptureAndAnalyze();
    }
#endif

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // set window position to top left corner
        SetWindowPos();

        clipboardPollingTimer.Start();
        PositionChanged += MainWindow_PositionChanged;

        // hide the window
        // Hide();
    }

    private void SetWindowPos()
    {
        PixelSize screenSize = Screens.Primary!.Bounds.Size;
        Height = screenSize.Height;
        Position = new PixelPoint(0, 0);
    }

    private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // set output to ClipboardService.LastResponse
        if (DataContext is MainViewModel viewModel)
        {
            if (!string.IsNullOrEmpty(ClipboardService.LastInput))
                viewModel.Input = ClipboardService.LastInput;
            if (!string.IsNullOrEmpty(ClipboardService.LastResponse))
                viewModel.Output = ClipboardService.LastResponse;
        }

        if (e.Property == WindowStateProperty)
        {
            MainWindow_WindowStateChanged(sender, e);
        }
    }

    private void MainWindow_WindowStateChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
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
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private void OnNotificationDismissed(object? sender, NotificationDismissedEventArgs e)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return;
            
        string reason = e.Reason.ToString();
        if (reason == "User")
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // abort the ongoing task
                ((MainViewModel)DataContext!).StopClippyTask();
            });
        }
    }

    private void OnNotificationActivated(object? sender, NotificationActivatedEventArgs e)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return;
            
        string actionId = e.ActionId;
        if (actionId == ClippyAI.Resources.Resources.TaskView)
        {
            // get text from notification
            string response = e.Notification.Body!;

            // cut text after first ':' character
            int index = response!.IndexOf(':');
            if (index > 0)
            {
                response = response[(index + 1)..];
            }

            // open view result dialog with the result
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ViewResultDialog viewResultDialog = new()
                {
                    DataContext = new ViewResultDialogViewModel(response)
                };
                viewResultDialog.Show();
            });
        }
    }

    public async void ShowNotification(string title, string body, bool showAbortButton = false, bool showViewButton = false)
    {
        try
        {
            Debug.Assert(_notificationManager != null);
            
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
                return;
                
            Notification nf;
            if (showAbortButton)
            {
                nf = new Notification
                {
                    Title = title,
                    Body = body,
                    Buttons = { (ClippyAI.Resources.Resources.TaskStop, ClippyAI.Resources.Resources.TaskStop) }
                };
            }
            else if (showViewButton)
            {
                nf = new Notification
                {
                    Title = title,
                    Body = body,
                    Buttons = { (ClippyAI.Resources.Resources.TaskView, ClippyAI.Resources.Resources.TaskView) }
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
            _lastNotification = nf;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to show notification", ex);
        }
    }

    public async void HideLastNotification()
    {
        try
        {
            if (_lastNotification != null && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            {
                await _notificationManager.HideNotification(_lastNotification);
            }
        }
        catch (Exception)
        {
        }
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private async void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        // cleanup hotkeys before closing
#if WINDOWS
        _windowsHotkeyService?.UnregisterHotkeys();
#endif

        // confirm closing the application
        string? result = await InputDialog.Confirm(this, ClippyAI.Resources.Resources.CloseApplication, ClippyAI.Resources.Resources.ConfirmClose);
        if (result == ClippyAI.Resources.Resources.Yes)
        {
            Close();
        }
    }
}