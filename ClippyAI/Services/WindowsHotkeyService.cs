using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClippyAI.Views;
using System.Timers;

namespace ClippyAI.Services
{
    [SupportedOSPlatform("windows")]
    public class WindowsHotkeyService
    {
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int VK_C = 0x43;
        private const int VK_A = 0x41;
        
        private readonly MainWindow _window;
        private readonly MainViewModel _viewModel;
        private IntPtr _windowHandle;
        private bool _isRegistered = false;
        private System.Timers.Timer? _hotkeyCheckTimer;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt key

        public WindowsHotkeyService(MainWindow window)
        {
            _window = window;
            _viewModel = window.DataContext as MainViewModel ?? throw new ArgumentException("Window must have MainViewModel");
        }

        public bool RegisterHotkeys()
        {
            if (!OperatingSystem.IsWindows())
                return false;

            try
            {
                // Wait for window to be fully initialized
                Task.Run(async () =>
                {
                    await Task.Delay(1000); // Give the window time to initialize
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        RegisterHotkeysInternal();
                    });
                });

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during hotkey registration: {ex.Message}");
                return false;
            }
        }

        private void RegisterHotkeysInternal()
        {
            try
            {
                // Get window handle using Avalonia's platform handle
                var platformHandle = _window.TryGetPlatformHandle();
                if (platformHandle?.HandleDescriptor != "HWND")
                {
                    Console.WriteLine("Could not get HWND for hotkey registration - using fallback polling method");
                    StartPollingMethod();
                    return;
                }

                _windowHandle = platformHandle.Handle;
                
                if (_windowHandle == IntPtr.Zero)
                {
                    Console.WriteLine("Invalid window handle for hotkey registration - using fallback polling method");
                    StartPollingMethod();
                    return;
                }

                // Register Ctrl+Alt+C (ID: 1)
                bool result1 = RegisterHotKey(_windowHandle, 1, MOD_CONTROL | MOD_ALT, VK_C);
                
                // Register Ctrl+Alt+A (ID: 2)  
                bool result2 = RegisterHotKey(_windowHandle, 2, MOD_CONTROL | MOD_ALT, VK_A);

                if (result1 && result2)
                {
                    _isRegistered = true;
                    Console.WriteLine("Windows hotkeys registered successfully using RegisterHotKey API");
                    
                    // Start a simple polling method as fallback since we can't hook messages safely
                    StartPollingMethod();
                }
                else
                {
                    Console.WriteLine($"Failed to register hotkeys: Ctrl+Alt+C={result1}, Ctrl+Alt+A={result2}");
                    
                    // If registration failed, use polling method as fallback
                    if (!result1)
                        Console.WriteLine("Ctrl+Alt+C may already be registered by another application");
                    if (!result2)
                        Console.WriteLine("Ctrl+Alt+A may already be registered by another application");
                        
                    Console.WriteLine("Falling back to polling method for hotkey detection");
                    StartPollingMethod();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during internal hotkey registration: {ex.Message}");
                Console.WriteLine("Falling back to polling method for hotkey detection");
                StartPollingMethod();
            }
        }

        private void StartPollingMethod()
        {
            // Use a polling approach to detect key combinations
            _hotkeyCheckTimer = new System.Timers.Timer(50); // Check every 50ms
            
            bool lastCtrlAltC = false;
            bool lastCtrlAltA = false;
            DateTime lastCCheck = DateTime.MinValue;
            DateTime lastACheck = DateTime.MinValue;
            
            _hotkeyCheckTimer.Elapsed += (sender, e) =>
            {
                try
                {
                    // Check if Ctrl+Alt+C is pressed
                    bool ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                    bool altPressed = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                    bool cPressed = (GetAsyncKeyState(VK_C) & 0x8000) != 0;
                    bool aPressed = (GetAsyncKeyState(VK_A) & 0x8000) != 0;

                    bool currentCtrlAltC = ctrlPressed && altPressed && cPressed;
                    bool currentCtrlAltA = ctrlPressed && altPressed && aPressed;

                    // Detect Ctrl+Alt+C press (with debouncing)
                    if (currentCtrlAltC && !lastCtrlAltC && (DateTime.Now - lastCCheck).TotalMilliseconds > 500)
                    {
                        lastCCheck = DateTime.Now;
                        _ = Task.Run(async () =>
                        {
                            await HandleHotkeyMessage(1); // Ctrl+Alt+C
                        });
                    }

                    // Detect Ctrl+Alt+A press (with debouncing)
                    if (currentCtrlAltA && !lastCtrlAltA && (DateTime.Now - lastACheck).TotalMilliseconds > 500)
                    {
                        lastACheck = DateTime.Now;
                        _ = Task.Run(async () =>
                        {
                            await HandleHotkeyMessage(2); // Ctrl+Alt+A
                        });
                    }

                    lastCtrlAltC = currentCtrlAltC;
                    lastCtrlAltA = currentCtrlAltA;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in hotkey polling: {ex.Message}");
                }
            };

            _hotkeyCheckTimer.Start();
            Console.WriteLine("Started polling-based hotkey detection for Ctrl+Alt+C and Ctrl+Alt+A");
        }

        public void UnregisterHotkeys()
        {
            if (!OperatingSystem.IsWindows())
                return;

            try
            {
                // Stop polling timer
                _hotkeyCheckTimer?.Stop();
                _hotkeyCheckTimer?.Dispose();
                _hotkeyCheckTimer = null;

                // Unregister system hotkeys if they were registered
                if (_isRegistered && _windowHandle != IntPtr.Zero)
                {
                    UnregisterHotKey(_windowHandle, 1);
                    UnregisterHotKey(_windowHandle, 2);
                    _isRegistered = false;
                }

                Console.WriteLine("Windows hotkeys unregistered");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during hotkey unregistration: {ex.Message}");
            }
        }

        private async Task HandleHotkeyMessage(int hotkeyId)
        {
            try
            {
                switch (hotkeyId)
                {
                    case 1: // Ctrl+Alt+C
                        Console.WriteLine("Ctrl+Alt+C hotkey detected");
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            await _viewModel.AskClippy(CancellationToken.None);
                        });
                        break;
                        
                    case 2: // Ctrl+Alt+A
                        Console.WriteLine("Ctrl+Alt+A hotkey detected");
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            await _viewModel.CaptureAndAnalyze();
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling hotkey: {ex.Message}");
            }
        }
    }
}