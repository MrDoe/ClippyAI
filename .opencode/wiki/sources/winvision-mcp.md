---
title: winvision-mcp — Windows GUI automation & QA via MCP
tags: [mcp, windows, gui-testing, uia, qa, tooling]
sourceRefs: [https://pypi.org/project/winvision-mcp/, C:/Temp/opencode/winvision-mcp-venv, opencode.jsonc]
lastReviewed: 2026-11-09
---

# winvision-mcp

Python MCP server (PyPI `winvision-mcp`, MIT, v1.0.1, released 2026-04-28) that lets
MCP clients screenshot, walk UI Automation trees, drive, and QA-assert any Win32 /
WinForms / WPF / WinUI / UWP / Electron desktop app. Explicitly built for opencode /
Claude Code / Cursor. Requires Windows 10 22H2+/11 and Python ≥3.11.

## Why we chose it (2026-11-09)

- Only candidate that names opencode first-class and ships QA-specific tools:
  vision asserts via **MCP sampling** (no API key), SSIM **visual baselines**, and
  **record/replay** JSONL scenarios at zero LLM cost.
- Pure-PyPI wheel install — no .NET SDK requirement (unlike winuse/WinProbe/Drive.NET).
- Safety model: hard blocklist, allowlist modes, dry-run flags, SQLite audit log.

Runner-ups: **WinApp MCP / desktop-pilot-mcp** (npm, 55 tools), **winuse** (.NET 10,
UIA+WebView2+Swing, scenario replay), **WinProbe** (WPF-focused), **windows-gui-mcp**
(pywinauto), AutoGenesis (BDD, heavy).

## Local install (this machine)

- Venv: `C:\Temp\opencode\winvision-mcp-venv` (created with `python -m venv` +
  `ensurepip`; `%APPDATA%` is a roaming network share so `pip --user`/`uv tool` were
  rejected — see quirk). **Run command** (since 2026-11-09):
  `...\Scripts\python.exe -m winvision_compatch` — a site-packages shim that calls
  `ole32.CoInitializeEx(MTA)` on every anyio worker thread before delegating to the
  real CLI (fixes the FastMCP≥3 UIA COM crash; see [[#known-issue-fastmcp--uia--com]]).
- Registered **project-scoped** as `mcp.winvision` in `opencode.jsonc` with
  `timeout: 60000` (per upstream troubleshooting: default 30 s severs stdio on the
  first cold call, and stdio never reconnects in-session).
- **Live-verified 2026-11-09**: config hot-reloaded, server connected via OpenCode,
  44 tools exposed; `get_system_info` + `list_baselines` returned real data
  (Win11 22631; host has 3 monitors incl. an 8480-px ultrawide — `click_at` is
  multi-monitor-safe via VIRTUALDESK, but per README, cross-monitor coordinate
  workflows still need care).
- Health check: `winvision-mcp.exe doctor`. Data/baselines/recordings:
  `%LOCALAPPDATA%\WinVision` (override `WINVISION_DATA_DIR`).
- Not UIAccess-signed → elevated windows are not drivable.

## Operating gotchas (encoded in [[../skills/win-gui-test|win-gui-test skill]])

1. Always pass an explicit `window_title` — walking the IDE foreground times out;
   v1.0.1 refuses it with `ide_foreground_refused`.
2. `assert_visual` requires MCP sampling; the local vLLM client may not support it →
   use `assert_element_visible` / `assert_text_present` / `baseline_compare` for
   replay-safe deterministic checks.
3. Electron/Chromium: rely on WGC screenshots + `click_at`; UIA tree may be empty.
4. `WINVISION_UIA_WALK_TIMEOUT_S` (8 s) bounds tree walks on accessibility-heavy apps.

## Known issue: FastMCP ≥ 3 breaks UIA (COM)

All UIA tools threw `[WinError -2147221008] CoInitialize has not been called` with
fastmcp 4.1.6 (fresh server too) — root cause
[fastmcp#4003](https://github.com/PrefectHQ/fastmcp/issues/4003): sync tools run on
anyio worker threads without COM init; winvision 1.0.1 doesn't use the `run_in_thread`
opt-out. **Pinning `fastmcp<3` does NOT work** (2.14.7/2.13.3 ImportError
`IdentityAssertionParams` against their resolved mcp). Working fix: the
`winvision_compatch` launch shim above (server verified: handshake + full ClippyAI
config-dialog flow PASS 2026-11-09). Watch upstream: a winvision release using
`@tool(run_in_thread=False)` would let us drop the shim.

Related: [[concepts/windows-release-pipeline]] (what we ship and test),
[[entities/llama-swap-router]] (client LLM behind sampling-capable calls).
