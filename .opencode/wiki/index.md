# Wiki Index

Content catalog — every page with a link and one-line summary.

## Entities
- [[entities/llama-swap-router]] — the local LLM gateway: PuTTY tunnel :11437 → llama-swap on the GPU server, auth, model aliases, OpenCode wiring

## Concepts
- [[concepts/qwen38-flash-next-troubleshooting]] — why "Qwen3.8-Flash-Next-NVFP4 doesn't work" is usually auth/tunnel/token-budget, not the model; verified probes and failure modes
- [[concepts/vscode-debug-setup]] — F5 was inert because Store/MSIX VS Code Insiders has zero extensions; fixed 2026-11-09 with stable VS Code + C# extension (VSIX route)
- [[concepts/windows-release-pipeline]] — Windows ships MSI only (WiX); ClickOnce profile is stale/unused; deb/rpm/tar on Linux via Packaging.Targets
- [[concepts/corporate-proxy-workaround]] — KRZ proxy: winget/dotnet/git use integrated auth; curl --proxy-ntlm + gunzip for VSIX downloads; never store creds

## Sources
- [[sources/winvision-mcp]] — Windows GUI automation & QA MCP server installed locally (venv + project-scoped `mcp.winvision`); choice rationale, install layout, operating gotchas
