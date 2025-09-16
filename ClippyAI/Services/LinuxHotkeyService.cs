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
    private IList<string> LastKeys = [];
    private MainViewModel? DataContext;
    private MainWindow? Window;
    private DateTime lastCtrl = DateTime.Now;
    private DateTime lastAlt = DateTime.Now;
    private DateTime lastC = DateTime.Now;
    private DateTime lastA = DateTime.Now;

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
        if (e.Key == EvDevKeyCode.KEY_LEFTCTRL && e.Value == EvDevKeyValue.KeyDown)
        {
            LastKeys.Add("Ctrl");
            lastCtrl = DateTime.Now;
        }
        else if (e.Key == EvDevKeyCode.KEY_LEFTALT && e.Value == EvDevKeyValue.KeyDown)
        {
            LastKeys.Add("Alt");
            lastAlt = DateTime.Now;
        }
        else if (e.Key == EvDevKeyCode.KEY_C && e.Value == EvDevKeyValue.KeyDown)
        {
            LastKeys.Add("C");
            lastC = DateTime.Now;
        }
        else if (e.Key == EvDevKeyCode.KEY_A && e.Value == EvDevKeyValue.KeyDown)
        {
            LastKeys.Add("A");
            lastA = DateTime.Now;
        }

        if (LastKeys.Count < 3)
            return;

        bool foundCtrl = false;
        bool foundAlt = false;
        bool foundC = false;
        bool foundA = false;

        // check if [Ctrl]+[Alt]+[C] hotkey is pressed in a row (only in this order)
        for (int i = 0; i < LastKeys.Count; i++)
        {
            if (LastKeys[i] == "Ctrl")
            {
                foundCtrl = true;
            }
            else if (LastKeys[i] == "Alt" && foundCtrl)
            {
                foundAlt = true;
            }
            else if (LastKeys[i] == "C" && foundAlt)
            {
                foundC = true;
            }
            else if (LastKeys[i] == "A" && foundAlt)
            {
                foundA = true; // updated from foundA to foundA
            }
        }

        if (foundCtrl && foundAlt && foundC &&
            DateTime.Now.Subtract(lastCtrl).TotalSeconds < 3 &&
            DateTime.Now.Subtract(lastAlt).TotalSeconds < 3 &&
            DateTime.Now.Subtract(lastC).TotalSeconds < 3)
        {
            Console.WriteLine("Hotkey pressed");
            LastKeys.Clear();

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
        else if (foundA &&
                DateTime.Now.Subtract(lastCtrl).TotalSeconds < 3 &&
                DateTime.Now.Subtract(lastAlt).TotalSeconds < 3 &&
                DateTime.Now.Subtract(lastA).TotalSeconds < 3)
        {
            Console.WriteLine("Ctrl + Alt + A hotkey pressed");
            LastKeys.Clear();

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
        else
        {
            LastKeys.Clear();
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
