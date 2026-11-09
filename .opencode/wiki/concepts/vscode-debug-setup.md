---
title: VS Code debugging setup (and why F5 failed)
tags: [vscode, csharp, debugging]
sourceRefs: [.vscode/launch.json, .vscode/tasks.json, ClippyAI/ClippyAI.csproj]
lastReviewed: 2026-11-09
---

## Root cause (diagnosed 2026-11-09)

F5 did nothing because the installed VS Code was the **Store/MSIX "VS Code - Insiders"**
build with **zero extensions** — no C# extension means no `coreclr` debug provider.
MSIX-packaged VS Code additionally can't launch the C# extension's server processes properly.
The repo itself always had a correct, tracked `.vscode/launch.json` + `tasks.json`
(glob/grep miss dotfolders — check `git ls-files` before assuming they're absent!).

## Resolution (2026-11-09)

- Installed stable VS Code 1.143.1 (user scope) via winget.
- Installed `ms-dotnettools.csharp` + `ms-dotnettools.vscode-dotnet-runtime` from
  marketplace VSIX (corporate proxy blocks the CLI gallery download; see [[corporate-proxy-workaround]]).
- Nothing changed in `.vscode/*` — original launch configs (Windows/Linux, preLaunchTask
  `build` = full solution) and packaging tasks remain as committed.
- **Open the project in "Visual Studio Code" (stable), not the Store Insiders.**

Debug target per launch.json: `ClippyAI/bin/Debug/net10.0-windows10.0.17763.0/ClippyAI.dll`.
Windows releases: [[windows-release-pipeline]] (MSI only, no ClickOnce).
