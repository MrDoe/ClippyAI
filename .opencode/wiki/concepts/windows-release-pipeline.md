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

## Build gotcha (learned 2026-11-09)

`ClippyAI.Installer.wixproj` publishes inside the build (`BeforeTargets="Build"`), but WiX's
`Files Include="!(bindpath.PublishDir)**"` harvest runs **before** the publish target fills
the folder on a clean build → 45 KB empty MSI (WIX8601 + ICE71). Also, incremental builds
silently repackage **stale** payloads. Correct flow: build once (publishes), touch
`Package.wxs`, build again (relink with fresh harvest); verify the exe `FileVersion`.
WiX v5 ProductCodes are content-deterministic → every rebuilt MSI needs the old product
uninstalled first. After a .NET SDK band bump, pre-run
`dotnet restore ClippyAI/ClippyAI.csproj --runtime win-x64 --force` (NETSDK1112).
