# .NET 10 Upgrade Plan — ClippyAI

## Table of Contents

- [1. Executive Summary](#1-executive-summary)
- [2. Migration Strategy](#2-migration-strategy)
- [3. Detailed Dependency Analysis](#3-detailed-dependency-analysis)
- [4. Project-by-Project Migration Plans](#4-project-by-project-migration-plans)
  - [4.1 DesktopNotifications](#41-desktopnotifications)
  - [4.2 EvDevSharp](#42-evdevsharp)
  - [4.3 DesktopNotifications.FreeDesktop](#43-desktopnotificationsfreedesktop)
  - [4.4 DesktopNotifications.Windows](#44-desktopnotificationswindows)
  - [4.5 DesktopNotifications.Avalonia](#45-desktopnotificationsavalonia)
  - [4.6 ClippyAI (main application)](#46-clippyai-main-application)
- [5. Package Update Reference](#5-package-update-reference)
- [6. Breaking Changes Catalog](#6-breaking-changes-catalog)
- [7. Testing & Validation Strategy](#7-testing--validation-strategy)
- [8. Risk Management](#8-risk-management)
- [9. Complexity & Effort Assessment](#9-complexity--effort-assessment)
- [10. Source Control Strategy](#10-source-control-strategy)
- [11. Success Criteria](#11-success-criteria)

---

## 1. Executive Summary

### Scenario
Upgrade ClippyAI — a cross-platform desktop AI assistant built with Avalonia UI — from **.NET 9** to **.NET 10 (LTS)**.

### Scope

| Dimension | Value |
|---|---|
| Projects | 6 (1 application + 5 library dependencies) |
| Current TFM | `net9.0` / `net9.0-windows10.0.17763.0` |
| Target TFM | `net10.0` / `net10.0-windows10.0.17763.0` |
| Total LOC | 8,795 |
| Affected files | 13 |
| NuGet packages | 22 total — 21 compatible, 1 update recommended |
| Security vulnerabilities | None |

### Selected Strategy
**All-At-Once Strategy** — All 6 projects upgraded simultaneously in a single atomic operation.

**Rationale**:
- Small solution (6 projects, ~8.8k LOC)
- All projects already on .NET 9 (modern SDK-style, no legacy frameworks)
- All NuGet packages compatible with .NET 10; only one minor version bump required
- No circular dependencies; clear 4-level dependency chain
- No security vulnerabilities requiring staged remediation
- No test projects to sequence around

### Complexity Classification
**Simple** — qualifies for the All-At-Once approach with maximum consolidation into a single upgrade task followed by build verification.

### Critical Issues
- **No blocking issues** — zero binary-incompatible APIs
- **6 mandatory** `Project.0002` items: target framework strings must be updated in all project files
- **34 source-incompatible** occurrences of `System.Configuration.ConfigurationManager` — already bridged by the `System.Configuration.ConfigurationManager` NuGet package (v10.0.3); expected to compile without code changes
- **25 behavioural-change** flags on `HttpContent`, `Uri` — runtime review recommended but no recompilation required

---

## 2. Migration Strategy

### Approach: All-At-Once

All 6 projects are upgraded **simultaneously** in a single atomic operation. No intermediate framework states are introduced.

### Justification

| Factor | Value | Implication |
|---|---|---|
| Project count | 6 | Well below 30-project threshold |
| Total LOC | 8,795 | Small codebase |
| Dependency depth | 4 levels | Clear, acyclic graph |
| Current TFM | All `net9.0` variants | Modern SDK-style, no legacy .NET Framework |
| Package compatibility | 21/22 compatible | Minimal package churn |
| Security vulnerabilities | 0 | No risk-first sequencing required |
| Test projects | None | No test sequencing needed |

### Execution Sequence

```
Phase 0: Prerequisites
  └─ Verify .NET 10 SDK installed  ✅ (confirmed during assessment)

Phase 1: Atomic Upgrade
  ├─ Update TargetFramework in all 6 project files
  ├─ Update System.Drawing.Common 8.0.10 → 10.0.3 in DesktopNotifications.Windows.csproj
  ├─ Restore dependencies (dotnet restore)
  ├─ Build solution and fix all compilation errors
  └─ Verify: solution builds with 0 errors
```

### Parallel vs Sequential
Within Phase 1 all project file edits are performed as a batch. There is no benefit to sequential per-project iteration since all compilation errors only surface after the full solution build.

### Multi-targeting note
Projects that currently multi-target (`DesktopNotifications`, `DesktopNotifications.FreeDesktop`) will have new .NET 10 TFMs appended to their existing `<TargetFrameworks>` list. This preserves .NET 9 compatibility while adding .NET 10 support.

---

## 3. Detailed Dependency Analysis

### Dependency Graph

```
Level 0 – Foundation (no project dependencies)
  ├─ DesktopNotifications.csproj          ← used by 4 projects
  └─ EvDevSharp.csproj                    ← used by ClippyAI only

Level 1 – depends on Level 0 only
  ├─ DesktopNotifications.FreeDesktop.csproj   ← depends on DesktopNotifications
  └─ DesktopNotifications.Windows.csproj       ← depends on DesktopNotifications

Level 2 – depends on Levels 0–1
  └─ DesktopNotifications.Avalonia.csproj      ← depends on Windows + FreeDesktop + DesktopNotifications

Level 3 – top-level application
  └─ ClippyAI.csproj                           ← depends on all 5 library projects
```

### Migration Phase Assignment (All-At-Once)

All 6 projects are upgraded in a **single coordinated operation**. The dependency levels above are informational — they do not imply sequential execution. Because all projects move from .NET 9 to .NET 10 simultaneously there are no intermediate states where one project targets .NET 10 while its dependency still targets .NET 9.

### Critical Path
`DesktopNotifications` → `FreeDesktop` + `Windows` → `Avalonia` → `ClippyAI`

All nodes on this path must compile cleanly before the solution build succeeds. No circular dependencies exist.

### Conditional Target Framework Logic
`ClippyAI.csproj` uses two OS-conditioned `<PropertyGroup>` blocks to set `<TargetFramework>`:

```xml
<PropertyGroup Condition=" '$(OS)' != 'Windows_NT' ">
  <TargetFramework>net9.0</TargetFramework>
</PropertyGroup>
<PropertyGroup Condition=" '$(OS)' == 'Windows_NT' ">
  <TargetFramework>net9.0-windows10.0.17763.0</TargetFramework>
</PropertyGroup>
```

Both conditions must be updated. The Windows condition retains the `windows10.0.17763.0` platform suffix.

`DesktopNotifications.csproj` and `DesktopNotifications.FreeDesktop.csproj` use multi-targeting (`net9.0;net9.0-windows10.0.17763.0`). Per the All-At-Once strategy, the new .NET 10 targets are **appended** to the existing list rather than replacing them, resulting in `net9.0;net9.0-windows10.0.17763.0;net10.0;net10.0-windows10.0.17763.0`.

---

## 4. Project-by-Project Migration Plans

### 4.1 DesktopNotifications

**File**: `ClippyAI/Libs/DesktopNotificationsNet8/DesktopNotifications/DesktopNotifications.csproj`

| Property | Value |
|---|---|
| Current TFM | `net9.0;net9.0-windows10.0.17763.0` |
| Target TFM | `net9.0;net9.0-windows10.0.17763.0;net10.0;net10.0-windows10.0.17763.0` |
| Project type | ClassLibrary |
| LOC | 225 |
| Dependencies | 0 |
| Dependants | 4 |
| Risk | 🟢 Low |
| Package issues | None |
| API issues | None |

**Migration Steps**:
1. Update `<TargetFrameworks>` — append `net10.0;net10.0-windows10.0.17763.0` to the existing list
2. No package updates required
3. No code changes expected — 0 source-incompatible or behavioural-change flags

**Validation**:
- [ ] Project compiles for all four target frameworks

---

### 4.2 EvDevSharp

**File**: `ClippyAI/Libs/evdev-sharp/EvDevSharp/EvDevSharp.csproj`

| Property | Value |
|---|---|
| Current TFM | `net9.0` |
| Target TFM | `net10.0` |
| Project type | ClassLibrary |
| LOC | 1,207 |
| Dependencies | 0 |
| Dependants | 1 (ClippyAI) |
| Risk | 🟢 Low |
| Package issues | None |
| API issues | None |

**Migration Steps**:
1. Update `<TargetFramework>net9.0</TargetFramework>` → `net10.0`
2. No package updates required
3. No code changes expected — 0 API issues flagged

**Validation**:
- [ ] Project compiles cleanly for `net10.0`

---

### 4.3 DesktopNotifications.FreeDesktop

**File**: `ClippyAI/Libs/DesktopNotificationsNet8/DesktopNotifications.FreeDesktop/DesktopNotifications.FreeDesktop.csproj`

| Property | Value |
|---|---|
| Current TFM | `net9.0;net9.0-windows10.0.17763.0` |
| Target TFM | `net9.0;net9.0-windows10.0.17763.0;net10.0;net10.0-windows10.0.17763.0` |
| Project type | ClassLibrary |
| LOC | 298 |
| Dependencies | 1 (`DesktopNotifications`) |
| Dependants | 2 |
| Risk | 🟢 Low |
| Package issues | None |
| API issues | None |

**Migration Steps**:
1. Update `<TargetFrameworks>` — append `net10.0;net10.0-windows10.0.17763.0`
2. No package updates required
3. No code changes expected — 0 API issues flagged

**Validation**:
- [ ] Project compiles for all four target frameworks

---

### 4.4 DesktopNotifications.Windows

**File**: `ClippyAI/Libs/DesktopNotificationsNet8/DesktopNotifications.Windows/DesktopNotifications.Windows.csproj`

| Property | Value |
|---|---|
| Current TFM | `net9.0-windows10.0.17763.0` |
| Target TFM | `net10.0-windows10.0.17763.0` |
| Project type | ClassLibrary |
| LOC | 787 |
| Dependencies | 1 (`DesktopNotifications`) |
| Dependants | 2 |
| Risk | 🟢 Low |
| Package issues | 1 (`System.Drawing.Common` update) |
| API issues | 2 behavioural (low severity) |

**Migration Steps**:
1. Update `<TargetFramework>net9.0-windows10.0.17763.0</TargetFramework>` → `net10.0-windows10.0.17763.0`
2. Update `System.Drawing.Common` from `8.0.10` → `10.0.3`
3. Review `WindowsNotificationManager.cs` line 210: `new Uri($"file:///{img}")` — `Uri(string)` constructor has a behavioural change in .NET 10. Verify file paths with special characters are still handled correctly at runtime.

**Validation**:
- [ ] Project compiles cleanly
- [ ] `System.Drawing.Common` resolves to 10.0.3
- [ ] `Uri` construction with file paths works as expected at runtime

---

### 4.5 DesktopNotifications.Avalonia

**File**: `ClippyAI/Libs/DesktopNotificationsNet8/DesktopNotifications.Avalonia/DesktopNotifications.Avalonia.csproj`

| Property | Value |
|---|---|
| Current TFM | `net9.0-windows10.0.17763.0` |
| Target TFM | `net10.0-windows10.0.17763.0` |
| Project type | ClassLibrary |
| LOC | 56 |
| Dependencies | 3 |
| Dependants | 1 (ClippyAI) |
| Risk | 🟢 Low |
| Package issues | None |
| API issues | 2 behavioural (low severity) |

> ⚠️ Note: Assessment lists the older `Avalonia 11.1.4` in this project's package list. The main `ClippyAI.csproj` uses `11.3.12`. Verify which version is declared in the Avalonia lib's `.csproj` file and ensure consistency is maintained after upgrade.

**Migration Steps**:
1. Update `<TargetFramework>net9.0-windows10.0.17763.0</TargetFramework>` → `net10.0-windows10.0.17763.0`
2. No additional package updates required (`Avalonia 11.1.4` is marked compatible)
3. 2 behavioural-change flags — no source changes required; review at runtime

**Validation**:
- [ ] Project compiles cleanly for `net10.0-windows10.0.17763.0`

---

### 4.6 ClippyAI (main application)

**File**: `ClippyAI/ClippyAI.csproj`

| Property | Value |
|---|---|
| Current TFM (Linux) | `net9.0` |
| Current TFM (Windows) | `net9.0-windows10.0.17763.0` |
| Target TFM (Linux) | `net10.0` |
| Target TFM (Windows) | `net10.0-windows10.0.17763.0` |
| Project type | Exe / WinExe (OS-conditional) |
| LOC | 6,222 |
| Dependencies | 5 library projects + 17 NuGet packages |
| Dependants | None (top-level) |
| Risk | 🟡 Medium |
| Package issues | None — all 17 packages compatible |
| API issues | 34 source-incompatible + 21 behavioural |

**Migration Steps**:

1. **Update conditional `<TargetFramework>` properties**:
   - Linux condition (`OS != Windows_NT`): `net9.0` → `net10.0`
   - Windows condition (`OS == Windows_NT`): `net9.0-windows10.0.17763.0` → `net10.0-windows10.0.17763.0`

2. **No NuGet package version changes required** — all 17 packages in this project are already compatible with .NET 10.

3. **`System.Configuration.ConfigurationManager` usage** (`ConfigurationService.cs`):
   - 34 source-incompatible flags on `ConfigurationManager` and `ConfigurationManager.AppSettings`
   - The `System.Configuration.ConfigurationManager` NuGet package (v10.0.3) is already referenced and provides the legacy API as a bridge
   - Expected to compile without code changes — verify after build
   - ⚠️ Long-term: consider migrating to `Microsoft.Extensions.Configuration` (JSON/environment variables), but this is out of scope for the current upgrade

4. **`OllamaService.cs` — `HttpContent` behavioural changes**:
   - Lines 123, 171, 224, 276, 316, 341, 353: `ReadAsStringAsync()` / `ReadAsStreamAsync()` have runtime behavioural changes in .NET 10
   - No source changes needed; review responses from Ollama API after upgrade to confirm content is read correctly
   - Affected patterns: streaming responses, error content reading, delete request body construction

5. **`OpenAIService.cs` — `HttpContent` behavioural changes**:
   - Lines 110, 117, 139, 213, 220: same `ReadAsStringAsync()` behavioural change
   - Review API responses at runtime

6. **`OllamaService.cs` line 52 — `new Uri(baseUrl)` behavioural change**:
   - `Uri(string)` constructor parsing may differ for certain edge-case URL formats in .NET 10
   - Verify URL parsing works correctly for configured Ollama endpoint

**Validation**:
- [ ] Both Linux and Windows TFM configurations build without errors
- [ ] `ConfigurationService` loads settings from `App.config` correctly
- [ ] Ollama and OpenAI service calls return expected results at runtime
- [ ] Application starts and UI renders correctly

---

## 5. Package Update Reference

### Packages Requiring Update

| Package | Current Version | Target Version | Project | Reason |
|---|---|---|---|---|
| `System.Drawing.Common` | 8.0.10 | **10.0.3** | `DesktopNotifications.Windows.csproj` | Align with target .NET 10 runtime |

### Packages Confirmed Compatible (no action required)

All packages in `ClippyAI.csproj` are already at .NET 10-aligned versions or confirmed compatible:

| Package | Version | Projects |
|---|---|---|
| `Avalonia` | 11.3.12 | `ClippyAI.csproj` |
| `Avalonia` | 11.1.4 | `DesktopNotifications.Avalonia.csproj` |
| `Avalonia.Desktop` | 11.3.12 | `ClippyAI.csproj` |
| `Avalonia.Diagnostics` | 11.3.12 | `ClippyAI.csproj` (Debug only) |
| `Avalonia.Fonts.Inter` | 11.3.12 | `ClippyAI.csproj` |
| `Avalonia.Themes.Fluent` | 11.3.12 | `ClippyAI.csproj` |
| `CommunityToolkit.Mvvm` | 8.4.0 | `ClippyAI.csproj` |
| `DirectShowLib.Net` | 3.0.0 | `ClippyAI.csproj` |
| `Emgu.CV` | 4.12.0.5764 | `ClippyAI.csproj` |
| `Emgu.CV.runtime.mini.ubuntu-x64` | 4.12.0.5764 | `ClippyAI.csproj` |
| `Emgu.CV.runtime.mini.windows` | 4.12.0.5764 | `ClippyAI.csproj` |
| `Microsoft.Data.Sqlite` | 10.0.3 | `ClippyAI.csproj` |
| `Microsoft.Extensions.DependencyInjection` | 10.0.3 | `ClippyAI.csproj` |
| `Microsoft.Toolkit.Uwp.Notifications` | 7.1.3 | `DesktopNotifications.Windows.csproj` |
| `Npgsql` | 10.0.1 | `ClippyAI.csproj` |
| `Packaging.Targets` | 0.1.232 | All lib projects |
| `ReactiveUI` | 23.1.1 | `ClippyAI.csproj` |
| `ReactiveUI.Avalonia` | 11.4.3 | `ClippyAI.csproj` |
| `SSH.NET` | 2025.1.0 | `ClippyAI.csproj` |
| `System.Configuration.ConfigurationManager` | 10.0.3 | `ClippyAI.csproj` |
| `Tmds.DBus` | 0.20.0 | `DesktopNotifications.FreeDesktop.csproj` |

---

## 6. Breaking Changes Catalog

### Summary

| Category | Count | Severity | Action Required |
|---|---|---|---|
| Mandatory (Project.0002) | 6 | 🔴 Mandatory | Update TFM in all 6 project files |
| Source Incompatible (Api.0002) | 34 | 🟡 Medium | Compile & verify — bridged by NuGet package |
| Behavioural Change (Api.0003) | 25 | 🔵 Low | Runtime review only |
| Binary Incompatible | 0 | — | None |

---

### BC-01: `System.Configuration.ConfigurationManager` / `AppSettings` — Source Incompatible

**Rule**: `Api.0002`  
**File**: `ClippyAI/Services/ConfigurationService.cs` (line 536, 34 total occurrences)  
**API**: `System.Configuration.ConfigurationManager` type and `AppSettings` property  
**Impact**: The legacy `System.Configuration` namespace is not part of the .NET 10 BCL. However, the `System.Configuration.ConfigurationManager` NuGet package (already referenced at v10.0.3) provides the full legacy API surface as a supported bridge library.  
**Resolution**: No code changes expected. Build and confirm compilation succeeds. If compilation errors appear, verify the package reference to `System.Configuration.ConfigurationManager` is present and at version `10.0.3`.  
**Long-term note**: Consider migrating `ConfigurationService.cs` to `Microsoft.Extensions.Configuration` in a future release for alignment with modern .NET configuration patterns.

Reference: [Breaking Changes in .NET](https://go.microsoft.com/fwlink/?linkid=2262679)

---

### BC-02: `HttpContent.ReadAsStringAsync()` / `ReadAsStreamAsync()` — Behavioural Change

**Rule**: `Api.0003`  
**Files**:
- `ClippyAI/Services/OllamaService.cs` — lines 123, 171, 224, 276, 316, 341, 353
- `ClippyAI/Services/OpenAIService.cs` — lines 110, 117, 139, 213, 220

**API**: `System.Net.Http.HttpContent` and `HttpContent.ReadAsStreamAsync(CancellationToken)`  
**Impact**: Runtime behaviour of HTTP content reading may differ in .NET 10 (e.g., buffering behaviour, encoding defaults, or cancellation propagation). Binaries compile and load correctly; the difference is observable only at runtime.  
**Resolution**: No source changes required. After upgrade, exercise the Ollama and OpenAI integration paths and verify response content is parsed correctly. Key scenarios to test:
- Streaming text generation responses (`ReadAsStreamAsync`)
- Non-streaming responses (`ReadAsStringAsync`)
- Error response reading
- Delete request body serialisation

---

### BC-03: `Uri(string)` constructor — Behavioural Change

**Rule**: `Api.0003`  
**Files**:
- `ClippyAI/Services/OllamaService.cs` — line 52
- `ClippyAI/Libs/DesktopNotificationsNet8/DesktopNotifications.Windows/WindowsNotificationManager.cs` — line 210

**API**: `System.Uri` and `System.Uri.#ctor(System.String)`  
**Impact**: URI parsing rules may have tightened in .NET 10. Certain edge-case inputs (e.g., URLs with unescaped characters, file paths on Windows with backslashes) could be parsed differently.  
**Resolution**: No source changes required. Verify at runtime:
- `OllamaService.cs` line 52: Ollama base URL (`http://localhost:11434` by default) parses correctly
- `WindowsNotificationManager.cs` line 210: `file:///` URI constructed from image paths — test with paths containing spaces or non-ASCII characters

---

### BC-04: `System.Environment.OSVersion` — Behavioural Change

**Rule**: `Api.0003`  
**Occurrences**: 4 across the solution  
**Impact**: `OSVersion` may return different platform version strings in .NET 10. Any code that parses or compares the version string may behave differently.  
**Resolution**: Review usages. If only used for informational logging, no action needed. If used for conditional logic, verify the expected values in .NET 10.

---

## 7. Testing & Validation Strategy

> There are no automated test projects in this solution. Validation relies on build success and manual runtime smoke-testing.

### Level 1: Build Validation (automated)

- `dotnet restore` completes without dependency conflicts
- `dotnet build` compiles all 6 projects with **0 errors**
- Build warnings are reviewed but do not block progression

### Level 2: Startup Smoke Test (manual)

- Application launches (`dotnet run --project ClippyAI/ClippyAI.csproj`)
- Main window renders with Avalonia UI
- Configuration loads from `App.config` without exceptions
- Hotkey registration succeeds (Linux: evdev; Windows: Win32 hooks)
- Desktop notifications initialise without errors

### Level 3: Integration Smoke Test (manual)

- Connect to Ollama service and list available models
- Submit a text prompt and verify streaming response is received and written to clipboard
- Submit an image capture prompt (if Ollama vision model available)
- Submit a prompt via OpenAI-compatible endpoint (if configured)
- Verify SSH tunnel feature starts and stops correctly (if configured)

### Level 4: Platform Matrix

| Platform | TFM | Validation required |
|---|---|---|
| Linux (Ubuntu x64) | `net10.0` | Startup, evdev hotkeys, FreeDesktop notifications, Ollama integration |
| Windows 10/11 | `net10.0-windows10.0.17763.0` | Startup, Win32 hotkeys, Windows toast notifications, Ollama integration |

### Validation Checklist

- [ ] `dotnet build` exits with code 0
- [ ] No new `NU` NuGet warnings introduced
- [ ] Application starts on Linux
- [ ] Application starts on Windows
- [ ] Configuration values load from `App.config`
- [ ] Ollama API calls complete successfully
- [ ] Clipboard write/read works after AI response
- [ ] Desktop notifications appear on both platforms

---

## 8. Risk Management

### Risk Register

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R1 | `ConfigurationManager.AppSettings` fails to compile despite NuGet bridge | Low | Medium | Verify `System.Configuration.ConfigurationManager` 10.0.3 is resolved; check `app.config` is found at runtime |
| R2 | `HttpContent` behavioural change causes malformed Ollama/OpenAI responses | Low | High | Run integration smoke tests against live Ollama instance; compare responses before/after |
| R3 | `Uri` parsing change breaks Ollama URL or file-path notification images | Low | Medium | Test with default config (`http://localhost:11434`) and file paths containing spaces |
| R4 | Avalonia 11.1.4 in `DesktopNotifications.Avalonia` incompatible with .NET 10 at runtime | Low | Medium | Assessment confirms compatibility; if issues arise, upgrade to 11.3.12 to match main project |
| R5 | Platform-specific TFM suffix (`windows10.0.17763.0`) causes build failure on new SDK | Very Low | Low | .NET 10 SDK supports `windows10.0.17763.0`; validate on both platforms |
| R6 | `evdev-sharp` submodule behaviour differs on .NET 10 (Linux only) | Very Low | Medium | Test hotkey registration on Linux after upgrade |

### Contingency Plans

**If ConfigurationManager breaks (R1)**:  
Add explicit `<PackageReference Include="System.Configuration.ConfigurationManager" Version="10.0.3" />` if missing, or migrate affected settings to `appsettings.json` with `Microsoft.Extensions.Configuration`.

**If HttpContent responses are malformed (R2)**:  
Review .NET 10 release notes for `HttpContent` breaking changes. If streaming is affected, switch to `ReadAsStreamAsync` patterns without `CancellationToken` overload as a temporary workaround.

**If build fails unexpectedly**:  
Rollback by switching back to the `main` branch. The `upgrade-to-NET10` branch is isolated and `main` remains untouched.

---

## 9. Complexity & Effort Assessment

### Per-Project Complexity

| Project | Complexity | LOC | API Issues | Package Changes | Notes |
|---|---|---|---|---|---|
| `DesktopNotifications` | 🟢 Low | 225 | 0 | 0 | Multi-target append only |
| `EvDevSharp` | 🟢 Low | 1,207 | 0 | 0 | Single TFM change |
| `DesktopNotifications.FreeDesktop` | 🟢 Low | 298 | 0 | 0 | Multi-target append only |
| `DesktopNotifications.Windows` | 🟢 Low | 787 | 2 (behavioural) | 1 (`System.Drawing.Common`) | Minor package bump + runtime review |
| `DesktopNotifications.Avalonia` | 🟢 Low | 56 | 2 (behavioural) | 0 | Smallest lib; runtime review only |
| `ClippyAI` | 🟡 Medium | 6,222 | 55 (34 source + 21 behavioural) | 0 | Large file count; API flags are bridged or runtime-only |

### Overall Assessment
**Low-to-Medium** overall. The 34 source-incompatible flags in `ClippyAI` are expected to be resolved automatically by the existing NuGet bridge package — they do not represent manual code edits. The 25 behavioural-change flags require runtime verification but no source modifications.

### Upgrade Effort Distribution

| Activity | Relative Effort |
|---|---|
| Project file TFM updates (6 files) | Very Low |
| Package version update (`System.Drawing.Common`) | Very Low |
| Build and error resolution | Low |
| Runtime smoke testing | Medium |
| Total | **Low** |

---

## 10. Source Control Strategy

### Branching

| Branch | Purpose |
|---|---|
| `main` | Source branch — untouched during upgrade |
| `upgrade-to-NET10` | All upgrade changes are made here |

The `upgrade-to-NET10` branch has been created and checked out as part of the assessment phase.

### Commit Strategy (All-At-Once)

Following the All-At-Once strategy, a **single commit** is preferred for the entire atomic upgrade:

```
chore: upgrade solution from .NET 9 to .NET 10

- Update TargetFramework in all 6 project files
- Append net10.0 targets to multi-targeted projects
- Update System.Drawing.Common 8.0.10 → 10.0.3
- Fix all compilation errors from framework upgrade
```

If compilation errors require non-trivial code changes, a second commit may be used:

```
fix: resolve compilation errors after .NET 10 upgrade
```

### Merge Process

1. All changes committed on `upgrade-to-NET10`
2. Build passes with 0 errors on both Linux and Windows
3. Smoke tests pass on at least one platform
4. Create PR: `upgrade-to-NET10` → `main`
5. Review diff focused on: project file TFM changes, package version, any code fixes
6. Merge after approval

---

## 11. Success Criteria

### Technical Criteria

- [ ] All 6 projects target `.NET 10` (with correct platform suffixes where applicable)
- [ ] `System.Drawing.Common` updated to `10.0.3` in `DesktopNotifications.Windows.csproj`
- [ ] `dotnet build` exits with **0 errors** on Linux and Windows
- [ ] No new NuGet incompatibility warnings introduced
- [ ] Application starts successfully on at least one target platform
- [ ] Ollama API integration functions correctly at runtime
- [ ] Desktop notifications work on both Linux (FreeDesktop) and Windows (toast)
- [ ] Configuration loads from `App.config` without exceptions

### Quality Criteria

- [ ] No unintended code logic changes introduced during upgrade
- [ ] `ConfigurationService.cs` reads `AppSettings` correctly via NuGet bridge
- [ ] `HttpContent` stream/string reading produces correct AI responses
- [ ] No regression in clipboard write/read functionality

### Process Criteria

- [ ] All changes made on `upgrade-to-NET10` branch
- [ ] Single atomic commit covers all project file and package updates
- [ ] PR created for `upgrade-to-NET10` → `main` and approved before merge
- [ ] All-At-Once strategy principles applied: no intermediate .NET 9/.NET 10 mixed states
