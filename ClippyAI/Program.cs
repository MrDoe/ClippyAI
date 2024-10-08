using System;
using Avalonia;
using Avalonia.ReactiveUI;
using DesktopNotifications;
using DesktopNotifications.Avalonia;
namespace ClippyAI;
public class Program
{
    public static INotificationManager NotificationManager = null!;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .SetupDesktopNotifications(out NotificationManager!)
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI();
    }
}
