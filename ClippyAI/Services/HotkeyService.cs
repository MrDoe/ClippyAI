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
    public HotkeyService(MainWindow window)
    {
        DataContext = window.DataContext as MainViewModel;

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
    public void SetupHotkeyDevice()
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.WriteLine("This program is only supported on Linux.");
            return;
        }

        var devices = EvDevDevice.GetDevices().OrderBy(d => d.DevicePath).ToList();
        if (!devices.Any())
        {
            Console.WriteLine("No device was found.");
            return;
        }

        // get all keyboard devices
        var keyboards = devices.Where(d => d.GuessedDeviceType == EvDevGuessedDeviceType.Keyboard &&
                                      d.Name!.ToLower().Contains("keyboard")).ToList();

        if (!keyboards.Any()) // no keyboard device was found
        {
            Console.WriteLine("No keyboard device was found.");
            return;
        }
        else if (keyboards.Count == 1) // only one keyboard device was found
        {
            Console.WriteLine("Keyboard device:");
            Console.WriteLine(keyboards[0].Name);
            Keyboard = keyboards[0];
        }
        else // multiple keyboard devices were found
        {
            for (int i = 0; i < keyboards.Count; i++)
            {
                var device = keyboards[i];

                Console.WriteLine("Current device:");
                Console.WriteLine(device.Name);
                Console.WriteLine("Press [X] to test, if it is the right keyboard device or press [ENTER] for testing next device.");

                device.OnKeyEvent += OnDetectKeyboard;
                device.StopMonitoring();
                device.StartMonitoring();

                // wait for the user to press [ENTER]
                Console.ReadLine();

                if (Keyboard != null)
                {
                    break;
                }
            }
        }
    }

    private void OnKeyEvent(object sender, OnKeyEventArgs e)
    {
        if (e.Key == EvDevKeyCode.KEY_LEFTCTRL && e.Value == EvDevKeyValue.KeyDown)
        {
            LastKeys.Add("Ctrl");
            //Console.WriteLine("Ctrl key is pressed.");
        }
        else if (e.Key == EvDevKeyCode.KEY_LEFTALT && e.Value == EvDevKeyValue.KeyDown)
        {
            LastKeys.Add("Alt");
            //Console.WriteLine("Alt key is pressed.");
        }
        else if (e.Key == EvDevKeyCode.KEY_C && e.Value == EvDevKeyValue.KeyDown)
        {
            LastKeys.Add("C");
            //Console.WriteLine("C key is pressed.");
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
    private void OnDetectKeyboard(object sender, OnKeyEventArgs e)
    {
        var device = (EvDevDevice)sender;

        //Console.WriteLine($"Key: {e.Key}\tValue: {e.Value}");

        if (e.Key.ToString() == "KEY_X" && e.Value.ToString() == "KeyDown")
        {
            Console.WriteLine("Selected device:");
            Console.WriteLine(device.Name);
            Keyboard = device;

            // save to configuration file
            ConfigurationManager.AppSettings?.Set("LinuxKeyboardDevice", device.Name);

            StartMonitoring();
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
