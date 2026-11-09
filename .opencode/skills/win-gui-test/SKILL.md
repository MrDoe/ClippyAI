---
name: win-gui-test
description: Test or automate native Windows desktop GUI apps (ClippyAI, WPF/WinForms/WinUI/UWP/Win32/Electron) using the winvision MCP server — launch apps, walk UI Automation trees, click/type, screenshot, run QA assertions, visual-regression baselines, and record/replay E2E scenarios. Use when asked to GUI-test, smoke-test, drive, or verify a Windows desktop application.
---

# Windows GUI Testing (winvision MCP)

The `winvision` MCP server (registered in the project `opencode.jsonc`, exe at
`C:\Temp\opencode\winvision-mcp-venv\Scripts\winvision-mcp.exe`) drives any app that
exposes a UI Automation (UIA) surface. Prefer it over coordinate clicking or WinAppDriver.

## Golden rules (violating these wastes minutes)

1. **Always pass `window_title` / `window_title_regex`** to screenshot/UIA tools.
   Never let them default to the foreground window — while the agent runs, the
   foreground is the IDE/terminal, and walking it times out (server refuses with
   `error=ide_foreground_refused`).
2. **UIA-first, coordinates last.** `find_elements` by `automation_id` → `name` →
   `control_type` → `class_name`. Only fall back to `screenshot_annotated` +
   `click_at(x, y)` when the app exposes no usable UIA tree (some Electron/Skia/Flutter
   builds).
3. **Verify every action** with a post-condition: `assert_element_visible`,
   `assert_text_present`, or a follow-up `get_ui_tree_text`. Re-dump the tree after a
   failed verification instead of retrying blindly.
4. **`assert_visual` may be unavailable** — it needs MCP sampling. If it returns
   `"sampling unavailable"`, use the deterministic asserts or `baseline_compare`.
5. First call can be slow; the config uses `timeout: 60000`. If a timeout severs the
   stdio session, all later calls fail with `Not connected` — the server must be
   restarted (config hot-reload or new session), there is no in-session reconnect.

## Standard workflow

1. `list_windows` → find the target; or `launch_app` with the exe path
   (ClippyAI build output: `ClippyAI/bin/Release/net10.0-windows10.0.17763.0/win-x64/ClippyAI.exe`,
   installed: check `list_processes`/`is_running` for `ClippyAI`).
2. `wait_for_window`/`wait_for_element` before touching anything — never sleep blindly.
3. `get_ui_tree_text(window_title=...)` (cheap, ASCII) to pick stable selectors;
   `screenshot_window` for visual state (WGC capture handles GPU-composited windows
   correctly, unlike PrintWindow on Chromium/Electron).
4. Act: `invoke_element` / `set_value` / `toggle_element` / `select_item` /
   `expand_collapse` / `send_keys` / `type_text`.
5. Assert: `assert_element_visible`, `assert_text_present`; for visual regressions
   `baseline_capture` (first run) then `baseline_compare` (SSIM + annotated diff PNG).
6. Repeat cheaply: `start_recording` / `stop_recording` produce a JSONL script;
   `replay_script` replays it at **zero LLM cost** (skip `assert_visual` in recorded
   flows — use the deterministic asserts so replays work).

## Tool quick reference

| Area | Tools |
|---|---|
| Capture | `screenshot_desktop`, `screenshot_window`, `screenshot_region`, `screenshot_element`, `screenshot_annotated`, `tile_screenshot` |
| UIA | `get_ui_tree`, `get_ui_tree_text`, `find_elements`, `get_element`, `get_focused_element`, `list_windows` |
| Input | `invoke_element`, `set_value`, `toggle_element`, `select_item`, `expand_collapse`, `scroll_element`, `click_at`, `drag`, `type_text`, `send_keys`, `focus_window`, `wait_for_element`, `wait_for_idle` |
| App | `launch_app`, `kill_process` (allowlist-gated, supports `dry_run`), `list_processes`, `is_running` |
| QA | `assert_visual`, `assert_element_visible`, `assert_text_present`, `baseline_capture`, `baseline_compare`, `list_baselines`, `start_recording`, `stop_recording`, `replay_script` |
| System | `get_system_info`, `read_clipboard`, `write_clipboard`, `get_process_memory` |

## Safety & environment

- Destructive tools are allowlist-gated (`WINVISION_ALLOWLIST_MODE`, default
  `permissive_with_confirm`, audit-logged to SQLite under `%LOCALAPPDATA%\WinVision`).
  Data/baselines/recordings also live there — set `WINVISION_DATA_DIR` to relocate.
- Elevated target windows (running as Admin) need the signed UIAccess build — not
  installed; only normal-integrity apps are drivable.
- Health check: `C:\Temp\opencode\winvision-mcp-venv\Scripts\winvision-mcp.exe doctor`
  (DPI, COM, WGC, monitors). Upgrade: `...python.exe -m pip install -U winvision-mcp`.
- Screenshots are absolute-coordinate; single-monitor optimized; locked desktop
  (Win+L) breaks raw input injection but UIA-pattern actions and PrintWindow/WGC
  capture still work.

## ClippyAI-specific hints

- ClippyAI is a tray/notification app: its main UI may be a tray icon or a flyout
  window — enumerate with `list_windows` after interaction; UIA tree of tray flyouts
  can be lazy, open them first (e.g. via hotkey `send_keys`).
- Config DB lives at `%APPDATA%\ClippyAI\clippyai_config.db` (note: `%APPDATA%` here
  is a roaming network share — reading it is fine, don't write).
- Smoke-test recipe: build MSI or run the exe → `is_running ClippyAI` →
  `assert_element_visible` on a known automation_id → `screenshot_window` for evidence.

## Verified run book (2026-11-09, installed 1.6.0 MSI, Avalonia → UIA)

**Startup:** the app ALWAYS launches hidden to the tray (`MainWindow_Loaded` →
`Hide()`); global hotkeys Ctrl+Alt+C/↑/↓ run tasks, they do NOT show the window.
Raw Win32 `ShowWindow` unhides the HWND but leaves Avalonia `IsVisible=false` →
**UIA tree comes back with 0 nodes**. Show it properly via the tray:
1. `find_elements {name_regex:"(?i)hidden|ausgeblendet", control_type:"Button", window_title_regex:".*"}`
   → Win11 "Show hidden icons" chevron (AutomationId `SystemTrayIcon`) → `invoke_element`.
2. `find_elements {name:"ClippyAI", control_type:"Button", window_title_regex:".*"}`
   → invoke the EXACT name (beware look-alikes: VS Code's "ClippyAI (Git)" tray icons).
   Filter `matches[].info.process_id`. Main window then docks at screen-left, 536px wide.

**Selectors (verified):**
- Header icon trio (top-right, y≈16, w=30, **no x:Name/AutomationId**): leftmost =
  gear → opens ConfigurationDialog; then minimize; then close (close asks a Yes/No
  InputDialog). Add `x:Name` to the gear in MainWindow.axaml to stop geometry probing.
- ConfigurationDialog: stable ids `SaveButton`, `CloseButton`, `SaveStatusPanel`.
  Title is LOCALIZED ("Konfiguration" on de-DE) and winvision title-matching resolves
  the owning window — do NOT filter dialog elements by dialog title; use
  `{automation_id: "...", window_title_regex: ".*"}` and filter by `process_id`.
- Close the dialog via `CloseButton` in tests (Save writes to the roaming config DB).

**Tool constraints (hit in practice):**
- `get_ui_tree`/`get_ui_tree_text`: `depth ≤ 12`.
- `find_elements` matches can have `rect: null` (scrollbar children) — null-filter before `.x`.
- Screenshots are useless with a non-vision client model ("Cannot read image") — use
  `assert_element_visible` / `assert_text_present` / tree dumps for evidence.
- Debug sessions (F5) spawn extra ClippyAI instances — always pin one `pid`.

**Speed rules:**
- Batch a whole flow into ONE Code Mode `execute` block (8–10 calls per inference);
  never one MCP call per assistant turn.
- Recorded replays reference runtime element HANDLES → cross-run failures (observed
  13/28). Record invokes with `{automation_id: ...}` refs for replay-stable scripts
  (=> requires the app-side `x:Name` additions).
- Search known issues before debugging an MCP server beyond one restart.
