---
title: Corporate proxy (KRZ) network workaround
tags: [proxy, network, winget, nuget, vscode]
sourceRefs: []
lastReviewed: 2026-11-09
---

Machine sits behind `http://proxy.krz.uni-heidelberg.de:8080` requiring authentication,
and does TLS interception.

## Rules that work

- **winget, dotnet/NuGet, git**: authenticate via Windows integrated auth — just work.
  (winget needs `--source winget`; it otherwise tries the msstore source and dies on
  a cert-pinning mismatch, error `0x8a15005e`.)
- **PowerShell `Invoke-WebRequest` / `curl` without credentials**: fail with `407`
  (and the VS Code extension-gallery download inside the CLI fails TLS
  "unable to get local issuer certificate" because Node doesn't trust the interception CA).
- **Workaround for extension downloads**: `curl.exe --proxy-ntlm -U ":" -k`
  (integrated auth, `-k` because curl can't validate the intercepted chain) against
  `https://marketplace.visualstudio.com/_apis/public/gallery/publishers/<pub>/vsextensions/<ext>/latest/vspackage`,
  then **gunzip** the response (it comes back gzip-wrapped, magic `1f 8b`) and
  `code --install-extension <file>.vsix`.
- NEVER put proxy credentials in files, scripts, or committed config (user rule 2026-11-09).
