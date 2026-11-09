using Avalonia.Threading;
using ClippyAI.Models;
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

    /// <summary>A configured hotkey and the action it triggers.</summary>
    private sealed record HotkeyCombo(GlobalHotkey Hotkey, Action Action);

    private List<HotkeyCombo> _combos = [];

    public LinuxHotkeyService(MainWindow window)
    {
        Window = window;
        DataContext = Window.DataContext as MainViewModel;

        LoadHotkeyConfiguration();

        // get keyboard device from configuration file
        Keyboard = FindConfiguredDevice();

        if (Keyboard != null)
        {
            StartMonitoring();
        }
    }

    /// <summary>
    /// Re-reads the configured hotkey combos. The evdev reader keeps running
    /// and simply matches against the new set on the next key event.
    /// </summary>
    public void ApplyConfiguration()
    {
        LoadHotkeyConfiguration();
    }

    private void LoadHotkeyConfiguration()
    {
        List<HotkeyCombo> combos = [];

        TryAdd(combos, "HotkeyAskClippy", "Ctrl+Alt+C",
            () => Dispatcher.UIThread.Post(() => { _ = DataContext?.AskClippy(new CancellationToken()); }));
        TryAdd(combos, "HotkeyPreviousTask", "Ctrl+Alt+Up",
            () => Dispatcher.UIThread.Post(() => DataContext?.SelectPreviousTask()));
        TryAdd(combos, "HotkeyNextTask", "Ctrl+Alt+Down",
            () => Dispatcher.UIThread.Post(() => DataContext?.SelectNextTask()));

        _combos = combos;

        void TryAdd(List<HotkeyCombo> target, string configKey, string defaultCombo, Action action)
        {
            string raw = ConfigurationService.GetConfigurationValue(configKey, defaultCombo);
            GlobalHotkey? parsed = GlobalHotkey.TryParse(raw);

            if (parsed == null && !string.IsNullOrWhiteSpace(raw) && !string.Equals(raw, defaultCombo, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Invalid hotkey configuration {configKey}='{raw}' - using default {defaultCombo}.");
                parsed = GlobalHotkey.TryParse(defaultCombo);
            }

            if (parsed?.EvDevKeyCodes == null)
            {
                if (parsed != null)
                {
                    Console.WriteLine($"Hotkey '{parsed}' has no evdev mapping for key '{parsed.KeyName}'.");
                }

                return; // disabled or unmappable
            }

            target.Add(new HotkeyCombo(parsed, action));
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
                subtext: "Please test it after clicking OK by pressing the configured global hotkey.",
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

    // All parts of a combo must have been pressed within this window, mirroring
    // the tolerance of the original hard-coded implementation.
    private const int ComboWindowSeconds = 3;

    private void OnKeyEvent(object sender, OnKeyEventArgs e)
    {
        string? partName = GlobalHotkey.EvDevPartName(e.Key);
        if (partName == null)
        {
            return; // key irrelevant for every configured hotkey
        }

        if (e.Value == EvDevKeyValue.KeyDown)
        {
            _ = pressedKeys.Add(partName);
            keyTimes[partName] = DateTime.Now;
        }
        else if (e.Value == EvDevKeyValue.KeyUp)
        {
            _ = pressedKeys.Remove(partName);
            _ = keyTimes.Remove(partName);
            return;
        }
        else
        {
            return; // KeyRepeat: never a fresh trigger
        }

        foreach (HotkeyCombo combo in _combos)
        {
            if (IsPressed(combo.Hotkey))
            {
                Console.WriteLine($"Hotkey {combo.Hotkey} pressed");
                Consume(combo.Hotkey);

                try
                {
                    combo.Action();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to execute hotkey action: {ex.Message}");
                }

                break;
            }
        }

        bool IsPressed(GlobalHotkey hotkey)
        {
            DateTime now = DateTime.Now;
            foreach (string part in hotkey.PartNames)
            {
                if (!pressedKeys.Contains(part)
                    || !keyTimes.TryGetValue(part, out DateTime pressedAt)
                    || now.Subtract(pressedAt).TotalSeconds >= ComboWindowSeconds)
                {
                    return false;
                }
            }

            return true;
        }

        static void Consume(GlobalHotkey hotkey)
        {
            foreach (string part in hotkey.PartNames)
            {
                _ = pressedKeys.Remove(part);
                _ = keyTimes.Remove(part);
            }
        }
    }

    /// <summary>
    /// Listen for the configured global hotkeys.
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
