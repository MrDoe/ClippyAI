using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using ClippyAI.Views;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace ClippyAI.Services
{
    [SupportedOSPlatform("windows")]
    public class WindowsHotkeyService
    {
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_NOREPEAT = 0x4000;
        private const int VK_C = 0x43;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int WM_HOTKEY = 0x0312;

        private readonly MainWindow _window;
        private readonly MainViewModel _viewModel;
        private IntPtr _windowHandle;
        private bool _isRegistered = false;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("kernel32.dll")]
        private static extern int GetLastError();

        public WindowsHotkeyService(MainWindow window)
        {
            _window = window;
            _viewModel = window.DataContext as MainViewModel ?? throw new ArgumentException("Window must have MainViewModel");
        }

        public bool RegisterHotkeys()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                // Wait for window to be fully initialized
                _ = Task.Run(async () =>
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
                IPlatformHandle? platformHandle = _window.TryGetPlatformHandle();
                if (platformHandle?.HandleDescriptor != "HWND")
                {
                    Console.WriteLine("Could not get HWND for hotkey registration");
                    return;
                }

                _windowHandle = platformHandle.Handle;

                if (_windowHandle == IntPtr.Zero)
                {
                    Console.WriteLine("Invalid window handle for hotkey registration");
                    return;
                }

                // Hook into the window's WndProc to receive WM_HOTKEY messages
                Win32Properties.AddWndProcHookCallback(_window, WndProcHook);

                // MOD_NOREPEAT prevents repeated WM_HOTKEY messages while the key is held down.
                // On Windows 11, Ctrl+Alt is treated as AltGr for some keyboard layouts and may
                // block RegisterHotKey — the WndProc hook handles WM_HOTKEY reliably in all cases.
                bool result1 = RegisterHotKey(_windowHandle, 1, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_C);
                bool result2 = RegisterHotKey(_windowHandle, 2, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_UP);
                bool result3 = RegisterHotKey(_windowHandle, 3, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_DOWN);

                if (result1 || result2 || result3)
                {
                    _isRegistered = true;
                    Console.WriteLine($"Windows hotkeys registered: Ctrl+Alt+C={result1}, Ctrl+Alt+Up={result2}, Ctrl+Alt+Down={result3}");
                }
                else
                {
                    int err = GetLastError();
                    Console.WriteLine($"Failed to register hotkeys (Win32 error {err}). " +
                                      "Ctrl+Alt combinations may be reserved by another app or an IME on this keyboard layout.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during internal hotkey registration: {ex.Message}");
            }
        }

        /// <summary>
        /// Win32 WndProc hook – receives all messages sent to the Avalonia window.
        /// We intercept WM_HOTKEY (0x0312) here to trigger the appropriate action.
        /// </summary>
        private IntPtr WndProcHook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                handled = true;
                _ = Task.Run(async () => await HandleHotkeyMessage(hotkeyId));
            }
            return IntPtr.Zero;
        }

        public void UnregisterHotkeys()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                if (_isRegistered && _windowHandle != IntPtr.Zero)
                {
                    _ = UnregisterHotKey(_windowHandle, 1);
                    _ = UnregisterHotKey(_windowHandle, 2);
                    _ = UnregisterHotKey(_windowHandle, 3);
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

                    case 2: // Ctrl+Alt+Up
                        Console.WriteLine("Ctrl+Alt+Up hotkey detected");
                        await Dispatcher.UIThread.InvokeAsync(_viewModel.SelectPreviousTask);
                        break;

                    case 3: // Ctrl+Alt+Down
                        Console.WriteLine("Ctrl+Alt+Down hotkey detected");
                        await Dispatcher.UIThread.InvokeAsync(_viewModel.SelectNextTask);
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