# ClippyAI .NET 10 Upgrade Tasks

## Overview

This document tracks the execution of the ClippyAI upgrade from .NET 9 to .NET 10. All 6 projects will be upgraded simultaneously in a single atomic operation, followed by validation.

**Progress**: 0/2 tasks complete (0%) ![0%](https://progress-bar.xyz/0)

---

## Tasks

### [▶] TASK-001: Verify prerequisites
**References**: Plan §1 Executive Summary, Plan §2 Migration Strategy Phase 0

- [▶] (1) Verify .NET 10 SDK is installed and accessible
- [ ] (2) .NET 10 SDK version meets minimum requirements (**Verify**)

---

### [ ] TASK-002: Atomic framework and dependency upgrade
**References**: Plan §2 Phase 1, Plan §4 Project-by-Project Plans, Plan §5 Package Update Reference, Plan §6 Breaking Changes Catalog

- [ ] (1) Update `<TargetFramework>` or `<TargetFrameworks>` in all 6 project files per Plan §4 (DesktopNotifications appends `net10.0;net10.0-windows10.0.17763.0`, EvDevSharp changes to `net10.0`, DesktopNotifications.FreeDesktop appends `net10.0;net10.0-windows10.0.17763.0`, DesktopNotifications.Windows changes to `net10.0-windows10.0.17763.0`, DesktopNotifications.Avalonia changes to `net10.0-windows10.0.17763.0`, ClippyAI updates both OS-conditional blocks to `net10.0` and `net10.0-windows10.0.17763.0`)
- [ ] (2) All project files updated to target .NET 10 (**Verify**)
- [ ] (3) Update `System.Drawing.Common` from `8.0.10` to `10.0.3` in `DesktopNotifications.Windows.csproj` per Plan §5
- [ ] (4) Package reference updated (**Verify**)
- [ ] (5) Restore all dependencies with `dotnet restore`
- [ ] (6) All dependencies restored successfully (**Verify**)
- [ ] (7) Build solution with `dotnet build` and fix all compilation errors per Plan §6 Breaking Changes Catalog (BC-01: verify `System.Configuration.ConfigurationManager` v10.0.3 bridge package resolves `ConfigurationService.cs` compilation; BC-02, BC-03, BC-04: no source changes required for behavioural changes)
- [ ] (8) Solution builds with 0 errors (**Verify**)
- [ ] (9) Commit changes with message: "TASK-002: Upgrade ClippyAI from .NET 9 to .NET 10 - atomic framework and dependency upgrade"

---
