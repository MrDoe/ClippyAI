using Avalonia.Threading;
using ClippyAI.Views;
using EvDevSharp;
using EvDevSharp.Enums;
using EvDevSharp.EventArgs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        List<EvDevDevice> devices = EvDevDevice.GetDevices().OrderBy(d => d.DevicePath).ToList();
        if (!devices.Any())
        {
            throw new Exception("Devices are not queryable!");
        }

        // get all keyboard devices
        List<EvDevDevice> keyboards = devices.Where(d => d.GuessedDeviceType == EvDevGuessedDeviceType.Keyboard &&
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
            List<string?> keyboardNames = keyboards.Select(k => k.Name).ToList() ?? throw new Exception("No keyboard device was found.");

            // convert to ObservableCollection
            ObservableCollection<string> keyboardNamesCollection = new(keyboardNames!);

            string? selectedDeviceName = await InputDialog.Prompt(
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
        // Check for Ctrl + Alt + A
        else if (pressedKeys.Contains("Ctrl") && pressedKeys.Contains("Alt") && pressedKeys.Contains("A") &&
                 DateTime.Now.Subtract(keyTimes["Ctrl"]).TotalSeconds < 3 &&
                 DateTime.Now.Subtract(keyTimes["Alt"]).TotalSeconds < 3 &&
                 DateTime.Now.Subtract(keyTimes["A"]).TotalSeconds < 3)
        {
            Console.WriteLine("Hotkey Ctrl + Alt + A pressed");
            _ = pressedKeys.Remove("Ctrl");
            _ = pressedKeys.Remove("Alt");
            _ = pressedKeys.Remove("A");
            _ = keyTimes.Remove("Ctrl");
            _ = keyTimes.Remove("Alt");
            _ = keyTimes.Remove("A");

            // execute relay command CaptureAndAnalyze
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _ = (DataContext?.CaptureAndAnalyze());
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute CaptureAndAnalyze: {ex.Message}");
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
            Console.WriteLine("No keyboard device was found.");
            return;
        }

        Keyboard.OnKeyEvent -= OnKeyEvent;
        Keyboard.StopMonitoring();
        Keyboard.OnKeyEvent += OnKeyEvent;
        Keyboard.StartMonitoring();
    }
}
