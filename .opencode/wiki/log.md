# Wiki Log

## [2026-11-09] ingest | Llama-Swap router (entity) + Qwen3.8-Flash-Next troubleshooting
Diagnosed user report "model Qwen3.8-Flash-Next-NVFP4 does not work". Live probes (2026-11-09): router reachable, model LOADED, HTTP 200 for plain/streaming/tool-call/xhigh-reasoning requests — model is currently working (this very session runs on it). Root causes for past/future "does not work": missing/wrong API key (401; old key sk-vllm-local seen in 2026-11-06 logs, now sk-llama-local), dead PuTTY tunnel (ECONNREFUSED 127.0.0.1:11437), mandatory reasoning consuming small max_tokens → empty content, model-id confusion (OpenCode id `vllm/qwen3.8-flash-next` vs router aliases), llama-swap GPU-0 contention → swap-cold-start timeouts. Created entities/llama-swap-router.md and concepts/qwen38-flash-next-troubleshooting.md.
## [2026-11-09] ingest | ClickOnce publishing requires full MSBuild
## [2026-11-09] ingest | VS Code debugging setup (and why F5 failed)
## [2026-11-09] op | deleted clickonce-publishing (ClickOnce deprecated, MSI only)
## [2026-11-09] ingest | Windows release pipeline (MSI installer)
## [2026-11-09] ingest | Corporate proxy (KRZ) network workaround
## [2026-11-09] op | VS Code debugging setup: RESOLVED (stable 1.143.1 + C# ext via VSIX)
## [2026-11-09] op | .NET updated: SDK 10.0.403, 9.0.320, DR 10.0.14; SDK 8.0.427 installed then REMOVED per user
## [2026-11-09] op | Built ClippyAI 1.6.0 MSI from HEAD + installed to Program Files; documented wixproj harvest/publish ordering gotcha
