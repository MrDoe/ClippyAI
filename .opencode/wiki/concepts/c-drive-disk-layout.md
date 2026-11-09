---
title: C: Drive Disk Layout & Cleanup Candidates
tags: [environment, disk, cleanup, windows]
sourceRefs: [C:/Temp/opencode/scan1.txt, C:/Temp/opencode/diskscan2.out.txt, C:/Temp/opencode/drilldown.out.txt, C:/Temp/opencode/drilldown2.out.txt, C:/Temp/opencode/drilldown3.out.txt]
lastReviewed: 2026-11-09
---

# C: disk-full audit (2026-11-09)

C: = 237.4 GB total, **233.7 GB used, 3.7 GB free**. Measured read-only via
`robocopy /L /E /XJ /BYTES` (scripts in `C:\Temp\opencode\diskscan*.ps1` / `drilldown*.ps1`).
Nothing was deleted. Hibernation is off (no hiberfil.sys). Shell runs non-elevated,
so shadow-copy storage and DISM component-store size could not be measured.

## Where the bytes are

| Location | GB | Notes |
|---|---|---|
| C:\pagefile.sys | 29.7 | real peak commit 27.6 GB on 16 GB RAM — genuine usage |
| C:\Daten | 48 | Entwicklung 19.6, VSCode_Insiders 9.4, NuGet 4.6, Outlook 4.5, Pentaho 2.6, Firefox-Profile 2.0, nodejs 1.9, STARLIMS 1.7 |
| C:\Windows | 36 | WinSxS 11.1, assembly 7.8, System32 7.4, Installer 5.1 |
| C:\Users (profile) | 30 | AppData\Local: uv 4.8, Programs 3.0, NuGet 2.5, MailStore Home 2.3, MS 2.2, npm-cache 1.9, nvm 1.4, Postman 1.2, tree-sitter 1.2, Packages 1.0 |
| C:\Temp | 26 | pure junk, 3645 dirs (see below) |
| C:\Program Files | 25 | VS 6.7, dotnet 5.9, SSMS 22 2.9 |
| C:\Program Files (x86) | 21 | Microsoft 7.3, VS 3.6, Office 3.2, Kits 1.7, SSMS 20 1.1 |
| C:\ProgramData | 12 | Microsoft 7.4, Package Cache 2.2, MModal 1.8 |
| C:\$Recycle.Bin | 0.8 | |

Gotcha: `C:\Users\All Users` (12 GB) is a junction to `C:\ProgramData` — do not double-count.

### C:\Temp composition (26 GB)
- 284 VS-style 8.3 temp dirs (`raj25rsj.y54`…) = **13.6 GB**, dated 10/27–11/09 (Visual Studio installer/build scratch)
- `nxqwh4ka` 3.6 + `rwk143o0` 1.3 GB — VS installer staging, all 10/26
- `Nuget` 3.6 GB — NuGet extraction cache
- `opencode` 1.5 GB — this tool's own scratch (incl. config-backup)
- `rust_mozprofile*` ~0.6 GB — geckodriver/Firefox automation profiles
- 2731 of 4374 entries older than 7 days

## Reclaim tiers

**Tier 1 — EXECUTED 2026-11-09 (user-approved items 1–7): C: free went 3.9 → 38.5 GB (~34.6 GB reclaimed).**
Log: `C:\Users\doellingerchristoph\cleanup.log`. Done: C:\Temp mirror-wiped (24.4 GB),
`dotnet nuget locals all --clear` + stale `C:\Daten\NuGet` (4.6 GB), uv cache dir wiped (4.8 GB),
npm/pip caches, Recycle Bin, 5 old VSCode-Insiders hash builds, `-updater` installer caches,
balena_etcher/GitHubDesktop `.nupkg` release caches (apps intact). Config backups preserved at
`C:\Users\doellingerchristoph\opencode-config-backup` (10 files).
Left over: `C:\Daten\VSCode_Insiders\17523c000e` (0.24 GB, Explorer-locked shell-ext DLL — delete after reboot).
IMPORTANT: `NUGET_PACKAGES` env var = `C:\Temp\Nuget` (active global-packages folder lives in temp —
gets wiped by any Temp cleanup, rebuilds on next restore).

**Tier 2 — safe after confirmation (~20 GB + unknowns):**
regeneratable dev artifacts in repos: bin 7.2 + node_modules 2.9 + obj 1.3 + dist 0.7 ≈ **12 GB** (`git clean -xfd` per repo, costs rebuild time) ·
elevated: `Dism /Online /Cleanup-Image /StartComponentCleanup /ResetBase` (WinSxS usually −2…4) ·
elevated: check `vssadmin list shadowstorage` (unmeasured) ·
`nvm` old node versions ~1 · tree-sitter cache 1.2 (rebuilds) · Postman cache 1.2 ·
uninstall **SSMS 20** (SSMS 22 already installed) ~1.

**Do NOT delete:**
pagefile (commit demand is real — add RAM instead) · `C:\Windows\Installer` 5.1 (patch chain) ·
`ProgramData\Package Cache` 2.2 (burned-in uninstallers) · `ProgramData\Microsoft` 7.4 (Defender/WU) ·
`C:\Daten\Outlook` 4.5 (PST/OST user data — archive/move instead) · MailStore Home 2.3 (user data — move) ·
WinSxS only via DISM, never manually · **`C:\Tools\opencode\winvision-mcp-venv` — live winvision MCP
server venv (moved there 2026-11-09 after the Temp copy was destroyed by a Temp wipe; DO-NOT-DELETE)**.

**Lesson learned:** C:\Temp on this machine held *live infrastructure* — the winvision MCP venv
(exit 106 crash during the wipe) and NuGet global-packages (`NUGET_PACKAGES` env). Before any
`C:\Temp` wipe: enumerate venvs/services pointing into it. MCP venves belong under `C:\Tools\opencode\`.

Duplicate to review: `C:\Daten\Entwicklung\OpenCodeRAG-main` (0.48) looks like an extract duplicate of `OpenCodeRAG`.
