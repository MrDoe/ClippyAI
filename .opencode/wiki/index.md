# Wiki Index

Content catalog — every page with a link and one-line summary.

## concepts
- [[concepts/c-drive-disk-layout]] — C: disk-full audit: where the space lives, what is safely reclaimable
- [[concepts/clipboard-history-manual-knowledge]] — history service/schema, monitoring fixes, screenshot→manual pipeline, ClickHouse RAG, OpenAI vision + SetResponse fixes (v1.7.2/1.7.3)
- [[concepts/corporate-proxy-workaround]] — KRZ proxy auth; --noproxy IP-direct pattern for LAN hosts, --proxy-ntlm for VSIX
- [[concepts/global-hotkeys]] — Ctrl+Alt+C/Up/Down wiring on Windows (RegisterHotKey + WndProc hook) and Linux (evdev), silent-failure modes, probe playbook
- [[concepts/qwen38-flash-next-troubleshooting]] — why the vLLM model "does not work": 4096 ctx vs plugin injection, capabilities.input image fix
- [[concepts/vscode-debug-setup]] — C# Debug configuration root cause (ClippyAI.debug) and working setup
- [[concepts/windows-release-pipeline]] — releases ship MSI-only; docs updated in the MSI, not in the repo

## entities
- [[entities/llama-swap-router]] — local LLM router on :3180 (ollama-native + openai-compat routes, config, health, maintenance)

## sources
_(STARLIMS/Handbuch-Wiki-Seiten leben jetzt im Repo `C:\Daten\SLVSCODE` → `.opencode/wiki` (Home des LIMS-Wikis, 2026-11-09 Migration): `entities/starlims-workspace.md`, `sources/handbuch-review-2026-11-09.md`)_
- [[sources/winvision-mcp]] — Windows desktop automation MCP used for GUI QA in this repo
