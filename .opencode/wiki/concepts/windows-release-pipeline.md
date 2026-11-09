---
title: Windows release pipeline (MSI installer)
tags: [publish, msi, wix, release]
sourceRefs: [.vscode/tasks.json, scripts/publish-windows-msi-release.ps1, Installer/ClippyAI.Installer.wixproj]
lastReviewed: 2026-11-09
---

Windows releases of ClippyAI ship **only as an MSI installer** (user decision
2026-11-09; ClickOnce is not used).

- VS Code task `publish-windows-msi` → `scripts/publish-windows-msi-release.ps1 --check-only`,
  then builds `Installer/ClippyAI.Installer.wixproj` (Release) if needed.
- Task `publish-windows-msi-release` → runs the full release script.
- Underlying folder publish for the payload: task `publish-windows`
  (`dotnet publish ClippyAI/ClippyAI.csproj -c Release`), which works fine —
  the ClickOnce profile in `Properties/PublishProfiles/` is stale; do not use
  `dotnet publish -p:PublishProfile=ClickOnceProfile` (fails with MSB4803,
  ClickOnce needs .NET Framework MSBuild).

Related: [[vscode-debug-setup]]
