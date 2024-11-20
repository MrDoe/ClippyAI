using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using Avalonia.Threading;
using ClippyAI.Views;
using EvDevSharp;
namespace ClippyAI.Services;
[SupportedOSPlatform("linux")]
public class HotkeyService
{
    private EvDevDevice? Keyboard;
    private IList<string> LastKeys = [];
    private MainViewModel? DataContext;
    private MainWindow? Window;

    public HotkeyService(MainWindow window)
    {
        Window = window;
        DataContext = Window.DataContext as MainViewModel;

        // get keyboard device from configuration file
        Keyboard = ConfigurationManager.AppSettings?.Get("LinuxKeyboardDevice") switch
        {
            { } name => EvDevDevice.GetDevices().FirstOrDefault(d => d.Name == name),
            _ => null
        };

        if (Keyboard == null)
        {
            SetupHotkeyDevice();
        }
        else
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
            throw new Exception("This program is only supported on Linux.");
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
        }
        else // multiple keyboard devices were found
        {
            var keyboardNames = keyboards.Select(k => k.Name).ToList();
            var selectedDeviceName = await InputDialog.Prompt(
                parentWindow: Window!,
                title: "Select Keyboard Device",
                caption: "Please select a keyboard device:",
                subtext: string.Join("\n", keyboardNames),
                isRequired: true
            );

            if (string.IsNullOrEmpty(selectedDeviceName))
            {
                throw new Exception("No keyboard device was selected.");
            }

            Keyboard = keyboards.FirstOrDefault(k => k.Name == selectedDeviceName);
        }

        if (Keyboard != null)
        {
            // save to configuration file
            ConfigurationManager.AppSettings?.Set("LinuxKeyboardDevice", Keyboard.Name);

            StartMonitoring();
        }
    }

    private void OnKeyEvent(object sender, OnKeyEventArgs e)
    {
        if (e.Key == EvDevKeyCode.KEY_LEFTCTRL && e.Value == EvDevKeyValue.KeyDown)
        {
            LastKeys.Add("Ctrl");
        }
        else if (e.Key == EvDevKeyCode.KEY_LEFTALT && e.Value == EvDevKeyValue.KeyDown)
        {
            LastKeys.Add("Alt");
        }
        else if (e.Key == EvDevKeyCode.KEY_C && e.Value == EvDevKeyValue.KeyDown)
        {
            LastKeys.Add("C");
        }

        if(LastKeys.Count < 3)
            return;

        // check if [Ctrl]+[Alt]+[C] hotkey is pressed in a row
        bool foundCtrl = false;
        bool foundAlt = false;
        bool foundC = false;
        for(int i = 0; i < LastKeys.Count; i++)
        {
            switch(LastKeys[i])
            {
                case "Ctrl":
                    foundCtrl = true;
                break;
                case "Alt":
                    foundAlt = true;
                break;
                case "C":
                    foundC = true;
                break;
            }
        }
        
        if (foundCtrl && foundAlt && foundC)
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

        // remove previous event handler
        Keyboard.OnKeyEvent -= OnDetectKeyboard;
        Keyboard.OnKeyEvent += OnKeyEvent;

        Keyboard.StartMonitoring();
    }
}
