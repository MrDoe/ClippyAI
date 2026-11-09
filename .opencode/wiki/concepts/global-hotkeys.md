---
title: Global hotkeys (Windows & Linux)
tags: [hotkeys, windows, linux, avalonia, win32, input, configuration]
sourceRefs: [ClippyAI/Services/WindowsHotkeyService.cs, ClippyAI/Services/LinuxHotkeyService.cs, ClippyAI/Models/GlobalHotkey.cs, ClippyAI/Views/MainWindow.axaml.cs, ClippyAI/ViewModels/ConfigurationDialogViewModel.cs]
lastReviewed: 2026-11-09
---

# Global hotkeys

ClippyAI listens system-wide for three **configurable** hotkeys:
**Ask Clippy** (default Ctrl+Alt+C), **previous task** (Ctrl+Alt+Up),
**next task** (Ctrl+Alt+Down). Since 1.6.1 they are remappable in the
configuration dialog → System tab → *Global Hotkeys* (click field, press combo,
Save applies instantly — no restart). Empty field = disabled.

## Configuration

Stored in the `Configuration` table ([[concepts/global-hotkeys]] keys
`HotkeyAskClippy`, `HotkeyPreviousTask`, `HotkeyNextTask`) as canonical
`Ctrl+Alt+C` strings via `ConfigurationService`. `Models/GlobalHotkey.cs` is the
single source of truth: `TryParse` (order-insensitive, aliases, **at least one
modifier required** so bare keys can never be swallowed system-wide), canonical
`ToString`, Win32 VK/modifier mapping, evdev key mapping (both hands of every
modifier), and `NameFromAvaloniaKey` for the UI capture handler.
`ConfigurationDialogViewModel.Save()` validates (parse + uniqueness across the
three slots) *before* writing anything, and returns false on error; the dialog
then calls `MainWindow.ApplyGlobalHotkeyConfiguration()`, which re-registers on
Windows and hot-swaps the combo list on Linux.

## Windows flow

`MainWindow` ctor → `WindowsHotkeyService.RegisterHotkeys()` → `LoadHotkeyConfiguration()`
→ background retry loop (2 s × 30 attempts; window may not be realized yet, or a
dying previous instance may still own the combos) → `RegisterHotKey(hwnd, id 1..3,
mods|MOD_NOREPEAT, vk)` → `WM_HOTKEY` via `Win32Properties.AddWndProcHookCallback`
(added exactly once — duplicates double-fire) → `MainViewModel` actions.
On permanent failure the user gets a `ShowNotification`. A **watchdog** (60 s)
re-arms lost registrations: re-`RegisterHotKey` succeeding (not 1420) means the
key was lost and just reclaimed; that is how handle-recreation losses self-heal.
`ApplyConfiguration()` unregisters, reloads config and restarts the loop.

The panel hides itself in `MainWindow_Loaded` (`Hide()`); this is harmless — the
HWND survives hiding and still receives `WM_HOTKEY` (same mechanism tray apps use).

### Historical failure (outage 2026-11-09)

The pre-1.6.1 code made ONE RegisterHotKey attempt ~1 s after the ctor and logged
failures only to invisible stdout; a not-yet-realized HWND or a competing instance
(no single-instance guard exists) permanently killed hotkeys for that process.
The retry loop, watchdog and failure notification fix this class of loss.

### Layout caveat

On German/Czech layouts Ctrl+Alt is AltGr (Ctrl+Alt+C = €), which can make those
combos unregistrable or typed instead. Registration succeeded on the affected
de-DE machine, but the settings hint now steers users to Ctrl+Shift+… / Win+… —
configurable hotkeys turn this from a bug into a preference.
