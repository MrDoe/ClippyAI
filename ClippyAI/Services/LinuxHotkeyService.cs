using Avalonia.Threading;
using ClippyAI.Views;
using EvDevSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
namespace ClippyAI.Services;

[SupportedOSPlatform("linux")]
public class LinuxHotkeyService
{
    private EvDevDevice? Keyboard;
    private readonly HashSet<string> pressedKeys = [];
    private readonly Dictionary<string, DateTime> keyTimes = [];
    private readonly MainViewModel? DataContext;
    private readonly MainWindow? Window;

    public LinuxHotkeyService(MainWindow window)
    {
        Window = window;
        DataContext = Window.DataContext as MainViewModel;

        // get keyboard device from configuration file
        Keyboard = FindConfiguredDevice();

        if (Keyboard != null)
        {
            StartMonitoring();
        }
    }

    private static EvDevDevice? FindConfiguredDevice()
    {
        string name = ConfigurationService.GetConfigurationValue("LinuxKeyboardDevice");
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        try
        {
            return EvDevDevice.GetDevices().FirstOrDefault(d => d.Name == name);
        }
        catch (Exception ex)
        {
            // The device may be gone, or reading /dev/input may not be permitted.
            // Do not take the app down at startup; the settings button explains it.
            System.Diagnostics.Debug.WriteLine($"Could not load keyboard device '{name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Setup hotkey device
    /// </summary>
    public async Task SetupHotkeyDevice()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("This feature is only supported on Linux.");
        }

        List<EvDevDevice> devices = GetReadableDevices();

        // A keyboard is identified by its key set, not by its name: many
        // keyboards are called "AT Raw Set 2 keyboard", "N-KEY Device",
        // "Logitech K380" and so on. Requiring "keyboard" in the name hid them.
        List<EvDevDevice> keyboards = devices
            .Where(IsKeyboard)
            // Prefer the device whose name says "keyboard", then the one with
            // the richest key set (secondary HID interfaces often expose more
            // media keys than the real keyboard).
            .OrderByDescending(d => d.Name?.Contains("keyboard", StringComparison.OrdinalIgnoreCase) == true)
            .ThenByDescending(d => d.Keys?.Count ?? 0)
            .ToList();

        if (keyboards.Count == 0)
        {
            throw new InvalidOperationException(
                "No keyboard device was found. Make sure your user can read /dev/input/event* (group 'input').");
        }

        EvDevDevice selected;

        if (keyboards.Count == 1)
        {
            selected = keyboards[0];
        }
        else
        {
            List<string> keyboardNames = keyboards
                .Select(k => k.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct()
                .ToList();

            string? selectedDeviceName = await InputDialog.Prompt(
                parentWindow: Window!,
                title: "Select Keyboard Device",
                caption: "Select a keyboard device:",
                subtext: "Please test it after clicking OK by pressing [Ctrl]+[Alt]+[C] hotkey.",
                isRequired: true,
                initialValue: Keyboard?.Name ?? keyboardNames.FirstOrDefault() ?? "",
                comboBoxItems: new ObservableCollection<string>(keyboardNames)
            );

            if (string.IsNullOrEmpty(selectedDeviceName))
            {
                return;
            }

            selected = keyboards.FirstOrDefault(k => k.Name == selectedDeviceName)
                ?? throw new InvalidOperationException($"Keyboard device '{selectedDeviceName}' is no longer available.");
        }

        SelectDevice(selected);
        Window?.ShowNotification("ClippyAI", $"Keyboard device: {selected.Name}", false, false);
    }

    private void SelectDevice(EvDevDevice device)
    {
        // Keep the live instance when the same device is re-selected: replacing
        // it would start a second reader on the same event node.
        if (Keyboard?.DevicePath != device.DevicePath)
        {
            StopMonitoringCurrent();
            Keyboard = device;
        }

        if (!string.IsNullOrEmpty(Keyboard?.Name))
        {
            ConfigurationService.SetConfigurationValue("LinuxKeyboardDevice", Keyboard.Name);
        }

        StartMonitoring();
    }

    private void StopMonitoringCurrent()
    {
        EvDevDevice? previous = Keyboard;
        if (previous == null)
        {
            return;
        }

        // Unsubscribe first, otherwise the old device keeps firing hotkeys
        // until its blocked read returns.
        previous.OnKeyEvent -= OnKeyEvent;

        // EvDevSharp blocks in read() and StopMonitoring() waits for the next
        // event, so never call this synchronously on the UI thread.
        _ = Task.Run(() =>
        {
            try
            {
                previous.StopMonitoring();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not stop monitoring {previous.DevicePath}: {ex.Message}");
            }
        });
    }

    private const string InputPermissionMessage =
        "ClippyAI is not allowed to read the keyboard devices in /dev/input.\n\n" +
        "Add your user to the 'input' group, then log out and back in:\n\n" +
        "    sudo usermod -aG input $USER\n\n" +
        "Open this dialog again afterwards.";

    private static List<EvDevDevice> GetReadableDevices()
    {
        string[] eventPaths;
        try
        {
            eventPaths = Directory.GetFiles("/dev/input", "event*");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cannot list /dev/input: {ex.Message}", ex);
        }

        if (eventPaths.Length == 0)
        {
            throw new InvalidOperationException("No input devices were found in /dev/input.");
        }

        if (!eventPaths.Any(IsReadable))
        {
            throw new UnauthorizedAccessException(InputPermissionMessage);
        }

        try
        {
            return EvDevDevice.GetDevices().OrderBy(d => d.DevicePath).ToList();
        }
        catch (AggregateException ex) when (ex.InnerExceptions.Any(e => e is UnauthorizedAccessException))
        {
            throw new UnauthorizedAccessException(InputPermissionMessage, ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Input devices are not queryable: {ex.Message}", ex);
        }
    }

    private static bool IsReadable(string path)
    {
        try
        {
            using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsKeyboard(EvDevDevice device)
    {
        List<EvDevKeyCode>? keys = device.Keys;
        return keys != null
            && keys.Contains(EvDevKeyCode.KEY_A)
            && keys.Contains(EvDevKeyCode.KEY_Z)
            && keys.Contains(EvDevKeyCode.KEY_SPACE)
            && keys.Contains(EvDevKeyCode.KEY_ENTER);
    }

    private void OnKeyEvent(object sender, OnKeyEventArgs e)
    {
        string? keyStr = e.Key switch
        {
            EvDevKeyCode.KEY_LEFTCTRL => "Ctrl",
            EvDevKeyCode.KEY_LEFTALT => "Alt",
            EvDevKeyCode.KEY_C => "C",
            EvDevKeyCode.KEY_A => "A",
            EvDevKeyCode.KEY_UP => "Up",
            EvDevKeyCode.KEY_DOWN => "Down",
            _ => null
        };

        if (keyStr != null)
        {
            if (e.Value == EvDevKeyValue.KeyDown)
            {
                _ = pressedKeys.Add(keyStr);
                keyTimes[keyStr] = DateTime.Now;
            }
            else if (e.Value == EvDevKeyValue.KeyUp)
            {
                _ = pressedKeys.Remove(keyStr);
                _ = keyTimes.Remove(keyStr);
            }
        }

        // Check for Ctrl + Alt + C
        if (pressedKeys.Contains("Ctrl") && pressedKeys.Contains("Alt") && pressedKeys.Contains("C") &&
            DateTime.Now.Subtract(keyTimes["Ctrl"]).TotalSeconds < 3 &&
            DateTime.Now.Subtract(keyTimes["Alt"]).TotalSeconds < 3 &&
            DateTime.Now.Subtract(keyTimes["C"]).TotalSeconds < 3)
        {
            Console.WriteLine("Hotkey Ctrl + Alt + C pressed");
            _ = pressedKeys.Remove("Ctrl");
            _ = pressedKeys.Remove("Alt");
            _ = pressedKeys.Remove("C");
            _ = keyTimes.Remove("Ctrl");
            _ = keyTimes.Remove("Alt");
            _ = keyTimes.Remove("C");

            // execute relay command AskClippy
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _ = (DataContext?.AskClippy(new CancellationToken()));
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute AskClippy: {ex.Message}");
            }
        }
        // Check for Ctrl + Alt + Up
        else if (pressedKeys.Contains("Ctrl") && pressedKeys.Contains("Alt") && pressedKeys.Contains("Up") &&
                 DateTime.Now.Subtract(keyTimes["Ctrl"]).TotalSeconds < 3 &&
                 DateTime.Now.Subtract(keyTimes["Alt"]).TotalSeconds < 3 &&
                 DateTime.Now.Subtract(keyTimes["Up"]).TotalSeconds < 3)
        {
            Console.WriteLine("Hotkey Ctrl + Alt + Up pressed");
            _ = pressedKeys.Remove("Ctrl");
            _ = pressedKeys.Remove("Alt");
            _ = pressedKeys.Remove("Up");
            _ = keyTimes.Remove("Ctrl");
            _ = keyTimes.Remove("Alt");
            _ = keyTimes.Remove("Up");

            // execute relay command SelectPreviousTask
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DataContext?.SelectPreviousTask();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute SelectPreviousTask: {ex.Message}");
            }
        }
        // Check for Ctrl + Alt + Down
        else if (pressedKeys.Contains("Ctrl") && pressedKeys.Contains("Alt") && pressedKeys.Contains("Down") &&
                 DateTime.Now.Subtract(keyTimes["Ctrl"]).TotalSeconds < 3 &&
                 DateTime.Now.Subtract(keyTimes["Alt"]).TotalSeconds < 3 &&
                 DateTime.Now.Subtract(keyTimes["Down"]).TotalSeconds < 3)
        {
            Console.WriteLine("Hotkey Ctrl + Alt + Down pressed");
            _ = pressedKeys.Remove("Ctrl");
            _ = pressedKeys.Remove("Alt");
            _ = pressedKeys.Remove("Down");
            _ = keyTimes.Remove("Ctrl");
            _ = keyTimes.Remove("Alt");
            _ = keyTimes.Remove("Down");

            // execute relay command SelectNextTask
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DataContext?.SelectNextTask();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute SelectNextTask: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Listen for [Ctrl]+[Alt]+[C] hotkey
    /// </summary>
    public void StartMonitoring()
    {
        if (Keyboard == null)
        {
            Console.WriteLine("No keyboard device is configured.");
            return;
        }

        // StartMonitoring() is idempotent, so no need to stop a healthy reader.
        Keyboard.OnKeyEvent -= OnKeyEvent;
        Keyboard.OnKeyEvent += OnKeyEvent;
        Keyboard.StartMonitoring();
    }
}
