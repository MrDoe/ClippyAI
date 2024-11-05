using System;
using System.Threading;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ClippyAI.ViewModels;
using DesktopNotifications;
using System.Diagnostics;
using Avalonia.Markup.Xaml;

#if WINDOWS
using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;
#endif

#if LINUX
using System.Runtime.InteropServices;
using System.Threading.Tasks;
#endif

namespace ClippyAI.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    public readonly System.Timers.Timer clipboardPollingTimer;
    private readonly INotificationManager _notificationManager;
    private Notification? _lastNotification { get; set; }
    public new string Title
    {
        get => base.Title!;
        set => base.Title = value;
    }
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // poll clipboard every second
        clipboardPollingTimer = new System.Timers.Timer(400);
        clipboardPollingTimer.Elapsed += ClipboardPollingTimer_Elapsed;

        // register notification manager
        _notificationManager = Program.NotificationManager ??
                                throw new InvalidOperationException("Missing notification manager");
        _notificationManager.NotificationActivated += OnNotificationActivated;
        _notificationManager.NotificationDismissed += OnNotificationDismissed;

        // Subscribe to the WindowStateChanged event
        PropertyChanged += MainWindow_PropertyChanged;

#if WINDOWS        
        RegisterHotkeyWindows();
#elif LINUX
        RegisterHotkeyLinux();
#endif
    }

    private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            MainWindow_WindowStateChanged(sender, e);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

#if WINDOWS
    private void RegisterHotkeyWindows()
    {
        try
        {
            HotkeyManager.Current.AddOrReplace("Ctrl+Alt+C", System.Windows.Input.Key.C, ModifierKeys.Control | ModifierKeys.Alt, OnHotkeyHandler);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register hotkey: {ex.Message}");
        }
    }

    private async void OnHotkeyHandler(object? sender, HotkeyEventArgs e)
    {
        Console.WriteLine("Hotkey pressed");

        // execute relay command AskClippy
        await ((MainViewModel)DataContext!).AskClippy(new CancellationToken());
        e.Handled = true;
    }
#endif

#if LINUX
       // P/Invoke declarations
        [DllImport("libX11.so")]
        private static extern IntPtr XOpenDisplay(string? display_name);

        [DllImport("libX11.so")]
        private static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport("libX11.so")]
        private static extern uint XStringToKeysym(string? str);

        [DllImport("libX11.so")]
        private static extern uint XKeysymToKeycode(IntPtr display, uint keysym);

        [DllImport("libX11.so")]
        private static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grab_window, int owner_events, int pointer_mode, int keyboard_mode);

        [DllImport("libX11")]
        private static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grab_window);

        [DllImport("libX11.so")]
        private static extern void XSelectInput(IntPtr display, IntPtr window, long event_mask);

        [DllImport("libX11.so")]
        private static extern int XNextEvent(IntPtr display, ref XEvent event_return);

        [DllImport("libX11.so")]
        private static extern int XPending(IntPtr display);

        // Constants
        private const int ControlMask = 1 << 2;
        private const int AltMask = 1 << 3;
        private const int False = 0;
        private const int GrabModeAsync = 1;
        private const long KeyPressMask = 1L << 0;
        private const int KeyPress = 2;

        [StructLayout(LayoutKind.Sequential)]
        struct XEvent
        {
            public int type;
            // Other fields can be added here as needed
        }

    private void RegisterHotkeyLinux()
    {
        var display = XOpenDisplay(null);
        if (display == IntPtr.Zero)
        {
            Console.WriteLine("Cannot open X display");
            return;
        }
        Console.WriteLine("X display opened successfully.");

        IntPtr rootWindow = XDefaultRootWindow(display);
        Console.WriteLine($"Root window: {rootWindow}");

        uint keysym = XStringToKeysym("C");
        int keycode = (int)XKeysymToKeycode(display, keysym);
        Console.WriteLine($"Keysym: {keysym}, Keycode: {keycode}");
        
        XUngrabKey(display, keycode, ControlMask | AltMask, rootWindow);

        int grabResult = XGrabKey(display, keycode, ControlMask | AltMask, rootWindow, False, GrabModeAsync, GrabModeAsync);
        if (grabResult != 1)
        {
            Console.WriteLine($"Failed to grab keyboard shortcut [Ctrl]+[Alt]+[C]. Is another application using it?");
            return;
        }
        Console.WriteLine("Key grabbed successfully.");

        XSelectInput(display, rootWindow, KeyPressMask);
        Console.WriteLine("Input selected successfully.");

        Task.Run(() => ListenHotkey(display));
        Console.WriteLine("Hotkey listening task started.");
    }

        private async Task ListenHotkey(IntPtr display)
        {
            if (display == IntPtr.Zero)
            {
                Console.WriteLine("Invalid display pointer.");
                return;
            }

            Console.WriteLine("Display pointer is valid. Entering event loop...");

            try
            {
                while (true)
                {
                    XEvent ev = new();
                    XNextEvent(display, ref ev);

                    if (ev.type == KeyPress)
                    {
                        Console.WriteLine("Hotkey pressed");

                        // execute relay command AskClippy
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            await ((MainViewModel)DataContext!).AskClippy(new CancellationToken());
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
            }
        }
#endif

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // set window position to bottom right corner
        SetWindowPos();

        clipboardPollingTimer.Start();

        PositionChanged += MainWindow_PositionChanged;
                
        // hide the window
        Hide();
    }

    private void SetWindowPos()
    {
        // set window position to bottom right corner
        if (Screens.Primary != null)
        {
            // get primary screen size
            PixelSize screenSize = Screens.Primary.Bounds.Size;
            Height = screenSize.Height;
            PixelSize windowSize = PixelSize.FromSize(ClientSize, Screens.Primary.Scaling);

            Position = new PixelPoint(
              screenSize.Width - windowSize.Width - 1,
              0);
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
        catch (Exception)
        {
            // ignore
        }
    }

    private void OnNotificationDismissed(object? sender, NotificationDismissedEventArgs e)
    {
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
        catch (Exception)
        {
        }
    }

    public async void HideLastNotification()
    {
        try
        {
            if (_lastNotification != null)
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
        // confirm closing the application
        string? result = await InputDialog.Confirm(this, ClippyAI.Resources.Resources.CloseApplication, ClippyAI.Resources.Resources.ConfirmClose);
        if (result == ClippyAI.Resources.Resources.Yes)
        {
            Close();
        }
    }
}