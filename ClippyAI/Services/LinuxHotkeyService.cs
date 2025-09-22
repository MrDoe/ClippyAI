using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClippyAI.Views;
using EvDevSharp;
namespace ClippyAI.Services;
[SupportedOSPlatform("linux")]
public class LinuxHotkeyService
{
    private EvDevDevice? Keyboard;
    private HashSet<string> pressedKeys = new HashSet<string>();
    private Dictionary<string, DateTime> keyTimes = new Dictionary<string, DateTime>();
    private MainViewModel? DataContext;
    private MainWindow? Window;

    public LinuxHotkeyService(MainWindow window)
    {
        Window = window;
        DataContext = Window.DataContext as MainViewModel;

        // get keyboard device from configuration file
        Keyboard = ConfigurationService.GetConfigurationValue("LinuxKeyboardDevice") switch
        {
            { } name when !string.IsNullOrEmpty(name) => EvDevDevice.GetDevices().FirstOrDefault(d => d.Name == name),
            _ => null
        };

        if (Keyboard != null)
        {
            StartMonitoring();
        }
    }

    /// <summary>
    /// Setup hotkey device
    /// </summary>
    public async Task SetupHotkeyDevice()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new Exception("This feature is only supported on Linux.");
        }

        var devices = EvDevDevice.GetDevices().OrderBy(d => d.DevicePath).ToList();
        if (!devices.Any())
        {
            throw new Exception("Devices are not queryable!");
        }

        // get all keyboard devices
        var keyboards = devices.Where(d => d.GuessedDeviceType == EvDevGuessedDeviceType.Keyboard &&
                                      d.Name!.ToLower().Contains("keyboard")).ToList();

        if (!keyboards.Any()) // no keyboard device was found
        {
            throw new Exception("No keyboard device was found.");
        }
        else if (keyboards.Count == 1) // only one keyboard device was found
        {
            Keyboard = keyboards[0];
            Window!.HideLastNotification();
            Window.ShowNotification("ClippyAI", $"Keyboard device detected: {Keyboard.Name}", false, false);
        }
        else // multiple keyboard devices were found
        {
            var keyboardNames = keyboards.Select(k => k.Name).ToList() ?? throw new Exception("No keyboard device was found.");

            // convert to ObservableCollection
            var keyboardNamesCollection = new ObservableCollection<string>(keyboardNames!);

            var selectedDeviceName = await InputDialog.Prompt(
                parentWindow: Window!,
                title: "Select Keyboard Device",
                caption: "Select a keyboard device:",
                subtext: "Please test it after clicking OK by pressing [Ctrl]+[Alt]+[C] hotkey.",
                isRequired: true,
                initialValue: Keyboard?.Name ?? "",
                comboBoxItems: keyboardNamesCollection
            );

            if (string.IsNullOrEmpty(selectedDeviceName))
            {
                return;
            }

            Keyboard = keyboards.FirstOrDefault(k => k.Name == selectedDeviceName);
        }

        if (Keyboard != null)
        {
            // save to configuration database
            if (Keyboard.Name != null)
            {
                ConfigurationService.SetConfigurationValue("LinuxKeyboardDevice", Keyboard.Name);
            }
            StartMonitoring();
        }
    }

    private void OnKeyEvent(object sender, OnKeyEventArgs e)
    {
        string? keyStr = e.Key switch
        {
            EvDevKeyCode.KEY_LEFTCTRL => "Ctrl",
            EvDevKeyCode.KEY_LEFTALT => "Alt",
            EvDevKeyCode.KEY_C => "C",
            EvDevKeyCode.KEY_A => "A",
            _ => null
        };

        if (keyStr != null)
        {
            if (e.Value == EvDevKeyValue.KeyDown)
            {
                pressedKeys.Add(keyStr);
                keyTimes[keyStr] = DateTime.Now;
            }
            else if (e.Value == EvDevKeyValue.KeyUp)
            {
                pressedKeys.Remove(keyStr);
                keyTimes.Remove(keyStr);
            }
        }

        // Check for Ctrl + Alt + C
        if (pressedKeys.Contains("Ctrl") && pressedKeys.Contains("Alt") && pressedKeys.Contains("C") &&
            DateTime.Now.Subtract(keyTimes["Ctrl"]).TotalSeconds < 3 &&
            DateTime.Now.Subtract(keyTimes["Alt"]).TotalSeconds < 3 &&
            DateTime.Now.Subtract(keyTimes["C"]).TotalSeconds < 3)
        {
            Console.WriteLine("Hotkey Ctrl + Alt + C pressed");
            pressedKeys.Remove("Ctrl");
            pressedKeys.Remove("Alt");
            pressedKeys.Remove("C");
            keyTimes.Remove("Ctrl");
            keyTimes.Remove("Alt");
            keyTimes.Remove("C");

            // execute relay command AskClippy
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DataContext?.AskClippy(new CancellationToken());
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute AskClippy: {ex.Message}");
            }
        }
        // Check for Ctrl + Alt + A
        else if (pressedKeys.Contains("Ctrl") && pressedKeys.Contains("Alt") && pressedKeys.Contains("A") &&
                 DateTime.Now.Subtract(keyTimes["Ctrl"]).TotalSeconds < 3 &&
                 DateTime.Now.Subtract(keyTimes["Alt"]).TotalSeconds < 3 &&
                 DateTime.Now.Subtract(keyTimes["A"]).TotalSeconds < 3)
        {
            Console.WriteLine("Hotkey Ctrl + Alt + A pressed");
            pressedKeys.Remove("Ctrl");
            pressedKeys.Remove("Alt");
            pressedKeys.Remove("A");
            keyTimes.Remove("Ctrl");
            keyTimes.Remove("Alt");
            keyTimes.Remove("A");

            // execute relay command CaptureAndAnalyze
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    DataContext?.CaptureAndAnalyze();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute CaptureAndAnalyze: {ex.Message}");
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
            Console.WriteLine("No keyboard device was found.");
            return;
        }

        Keyboard.OnKeyEvent -= OnKeyEvent;
        Keyboard.StopMonitoring();
        Keyboard.OnKeyEvent += OnKeyEvent;
        Keyboard.StartMonitoring();
    }
}
