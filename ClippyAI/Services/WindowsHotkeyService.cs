using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using ClippyAI.Models;
using ClippyAI.Views;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;
        private const int MOD_NOREPEAT = 0x4000;
        private const int WM_HOTKEY = 0x0312;

        // The hotkeys the user can remap in the configuration dialog. Ids are
        // stable (they arrive as wParam of WM_HOTKEY); the combos are re-read
        // from the configuration database on every (re-)registration.
        private sealed class HotkeyEntry
        {
            public required int Id { get; init; }
            public required string ConfigKey { get; init; }
            public required string DefaultCombo { get; init; }
            public required string ActionLabel { get; init; }
            public GlobalHotkey? Hotkey { get; set; }
            public bool Registered { get; set; }
            public string Display => Hotkey?.ToString() ?? DefaultCombo;
        }

        private readonly HotkeyEntry[] _entries =
        [
            new() { Id = 1, ConfigKey = "HotkeyAskClippy",   DefaultCombo = "Ctrl+Alt+C",   ActionLabel = "AskClippy" },
            new() { Id = 2, ConfigKey = "HotkeyPreviousTask", DefaultCombo = "Ctrl+Alt+Up",  ActionLabel = "SelectPreviousTask" },
            new() { Id = 3, ConfigKey = "HotkeyNextTask",     DefaultCombo = "Ctrl+Alt+Down", ActionLabel = "SelectNextTask" }
        ];

        private readonly MainWindow _window;
        private readonly MainViewModel _viewModel;
        private IntPtr _windowHandle;
        private bool _isRegistered = false;
        private bool _hookAdded = false;

        // Registration is retried until it succeeds: right after startup the
        // Avalonia HWND may not be created yet, and the combinations may still
        // be held by a previous instance that is on its way out (e.g. during
        // an update). Both are transient, so give up only after a while and
        // then tell the user instead of failing silently forever.
        private const int RegistrationRetryLoopDelayMs = 2000;
        private const int MaxRegistrationAttempts = 30;

        // After successful registration a watchdog re-verifies ownership
        // periodically, so hotkeys self-heal even if the registration is ever
        // lost again at runtime (e.g. native window handle recreation).
        private const int WatchdogIntervalMs = 60_000;
        private Timer? _watchdogTimer;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

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
                LoadHotkeyConfiguration();
                StartRegistrationLoop();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during hotkey registration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Re-reads the configured combos and swaps them in: used after the
        /// user saved new hotkeys in the configuration dialog.
        /// </summary>
        public void ApplyConfiguration()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            UnregisterHotkeys();
            LoadHotkeyConfiguration();
            StartRegistrationLoop();
        }

        private void LoadHotkeyConfiguration()
        {
            foreach (HotkeyEntry entry in _entries)
            {
                string raw = ConfigurationService.GetConfigurationValue(entry.ConfigKey, entry.DefaultCombo);
                GlobalHotkey? parsed = GlobalHotkey.TryParse(raw);

                if (parsed == null && !string.IsNullOrWhiteSpace(raw) && !string.Equals(raw, entry.DefaultCombo, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Invalid hotkey configuration {entry.ConfigKey}='{raw}' - using default {entry.DefaultCombo}.");
                    parsed = GlobalHotkey.TryParse(entry.DefaultCombo);
                }

                entry.Hotkey = parsed; // null = disabled
                entry.Registered = false;
            }

            _isRegistered = false;
        }

        private void StartRegistrationLoop()
        {
            // Retry until every configured hotkey is ours (see field comment):
            // the window handle may not exist yet, or the combinations may
            // still be owned by a dying previous instance.
            _ = Task.Run(async () =>
            {
                for (int attempt = 1; attempt <= MaxRegistrationAttempts && !_isRegistered; attempt++)
                {
                    await Task.Delay(RegistrationRetryLoopDelayMs);
                    await Dispatcher.UIThread.InvokeAsync(RegisterHotkeysInternal);
                }

                if (!_isRegistered)
                {
                    ReportRegistrationFailure();
                }
            });
        }

        private void RegisterHotkeysInternal()
        {
            try
            {
                // Get window handle using Avalonia's platform handle
                IPlatformHandle? platformHandle = _window.TryGetPlatformHandle();
                if (platformHandle?.HandleDescriptor != "HWND")
                {
                    // Window is not realized yet - the retry loop will try again.
                    return;
                }

                _windowHandle = platformHandle.Handle;

                if (_windowHandle == IntPtr.Zero)
                {
                    return;
                }

                // The hook receives the WM_HOTKEY messages produced by the
                // successful RegisterHotKey calls below - it is NOT a fallback
                // for failed registrations (no registration means no WM_HOTKEY,
                // ever). Add it only once: a second callback would fire for the
                // same message and trigger the action twice.
                if (!_hookAdded)
                {
                    Win32Properties.AddWndProcHookCallback(_window, WndProcHook);
                    _hookAdded = true;
                }

                bool anyEnabled = false;
                foreach (HotkeyEntry entry in _entries)
                {
                    if (entry.Hotkey == null)
                    {
                        continue; // disabled by configuration
                    }

                    anyEnabled = true;
                    if (!entry.Registered)
                    {
                        entry.Registered = TryRegisterEntry(entry);
                    }
                }

                bool allDone = !anyEnabled || _entries.All(e => e.Hotkey == null || e.Registered);
                if (allDone)
                {
                    _isRegistered = true;

                    if (anyEnabled)
                    {
                        Console.WriteLine("Windows hotkeys registered: " +
                            string.Join(", ", _entries.Where(e => e.Hotkey != null).Select(e => e.Display)));
                    }
                    else
                    {
                        Console.WriteLine("All global hotkeys are disabled by configuration.");
                    }

                    // Self-heal later: re-check ownership periodically so the
                    // hotkeys come back on their own if they are ever lost.
                    _watchdogTimer ??= new Timer(
                        _ => Dispatcher.UIThread.Post(VerifyAndReacquireHotkeys),
                        null,
                        WatchdogIntervalMs,
                        WatchdogIntervalMs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during internal hotkey registration: {ex.Message}");
            }
        }

        private bool TryRegisterEntry(HotkeyEntry entry)
        {
            GlobalHotkey hotkey = entry.Hotkey!;

            if (hotkey.VirtualKey == 0)
            {
                Console.WriteLine($"Hotkey '{entry.Display}' has no Win32 virtual-key mapping.");
                return true; // not retryable - treat as handled to avoid notification spam
            }

            // MOD_NOREPEAT prevents repeated WM_HOTKEY messages while the key is held down.
            // Note: on German/Czech and similar layouts Ctrl+Alt is AltGr (Ctrl+Alt+C = Euro
            // sign), which can make these combinations fail to register or be consumed by the
            // layout - the settings dialog suggests Ctrl+Shift+... or Win+... alternatives.
            int modifiers = hotkey.Win32Modifiers | MOD_NOREPEAT;

            if (RegisterHotKey(_windowHandle, entry.Id, modifiers, hotkey.VirtualKey))
            {
                return true;
            }

            // Marshal.GetLastWin32Error() must be used for DllImports marked
            // with SetLastError; calling kernel32 GetLastError() as an extra
            // P/Invoke can already return a clobbered value.
            int err = Marshal.GetLastWin32Error();
            Console.WriteLine($"Failed to register {entry.Display} (Win32 error {err}, " +
                              $"modifiers=0x{modifiers:X4}, vk=0x{hotkey.VirtualKey:X2}, hWnd=0x{_windowHandle.ToInt64():X}). " +
                              (err == 1420
                                  ? "Another application or another ClippyAI instance already owns this hotkey."
                                  : "The combination may be reserved by another app or by the keyboard layout (AltGr)."));
            return false;
        }

        private void ReportRegistrationFailure()
        {
            string combos = string.Join(" / ", _entries.Where(e => e.Hotkey != null).Select(e => e.Display));
            Console.WriteLine($"Global hotkey registration permanently failed ({combos}). " +
                              "Close other ClippyAI instances or the application using these combinations, " +
                              "or pick different ones in the configuration dialog.");

            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _window.ShowNotification(
                        "ClippyAI",
                        $"Global hotkeys ({combos}) could not be registered. " +
                        "Another ClippyAI instance or application is using them - close it and restart ClippyAI, " +
                        "or choose different hotkeys in the configuration dialog (System tab).",
                        false, false, 15000);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not show hotkey failure notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Watchdog: re-arms the hotkeys on the current window handle. A hotkey
        /// we still own makes RegisterHotKey fail with ERROR_HOTKEY_ALREADY_REGISTERED
        /// (1420); a successful call therefore means it had been lost (e.g. the
        /// native handle was recreated) and was just won back. If a key cannot
        /// be won back (someone else took it), the normal retry loop restarts.
        /// Runs on the UI thread via Dispatcher.UIThread.Post.
        /// </summary>
        private void VerifyAndReacquireHotkeys()
        {
            if (!_isRegistered)
            {
                return;
            }

            try
            {
                IPlatformHandle? platformHandle = _window.TryGetPlatformHandle();
                if (platformHandle?.HandleDescriptor != "HWND" || platformHandle.Handle == IntPtr.Zero)
                {
                    return;
                }

                _windowHandle = platformHandle.Handle;

                bool ok = true;
                foreach (HotkeyEntry entry in _entries)
                {
                    if (entry.Hotkey?.VirtualKey is null or 0)
                    {
                        continue;
                    }

                    ok &= ReverifyEntry(entry);
                }

                if (!ok)
                {
                    foreach (HotkeyEntry entry in _entries)
                    {
                        entry.Registered = false;
                    }

                    _isRegistered = false;
                    Console.WriteLine("Hotkey watchdog: registration lost and not immediately re-acquirable - restarting retry loop.");
                    StartRegistrationLoop();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hotkey watchdog error: {ex.Message}");
            }
        }

        private bool ReverifyEntry(HotkeyEntry entry)
        {
            GlobalHotkey hotkey = entry.Hotkey!;

            if (RegisterHotKey(_windowHandle, entry.Id, hotkey.Win32Modifiers | MOD_NOREPEAT, hotkey.VirtualKey))
            {
                Console.WriteLine($"Hotkey watchdog: {entry.Display} had been lost, re-registered.");
                return true;
            }

            return Marshal.GetLastWin32Error() == 1420; // already registered = still ours
        }

        /// <summary>
        /// Win32 WndProc hook - receives all messages sent to the Avalonia window.
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
                if (_windowHandle != IntPtr.Zero)
                {
                    foreach (HotkeyEntry entry in _entries)
                    {
                        _ = UnregisterHotKey(_windowHandle, entry.Id);
                        entry.Registered = false;
                    }
                }

                _isRegistered = false;
                _watchdogTimer?.Dispose();
                _watchdogTimer = null;

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
                HotkeyEntry? entry = _entries.FirstOrDefault(e => e.Id == hotkeyId);
                if (entry == null)
                {
                    return;
                }

                Console.WriteLine($"{entry.Display} hotkey detected");

                switch (hotkeyId)
                {
                    case 1:
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            await _viewModel.AskClippy(CancellationToken.None);
                        });
                        break;

                    case 2:
                        await Dispatcher.UIThread.InvokeAsync(_viewModel.SelectPreviousTask);
                        break;

                    case 3:
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
