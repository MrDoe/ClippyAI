---
title: Llama-Swap Router (local LLM gateway)
tags: [infrastructure, llm, llama-swap, vllm, sglang, putty]
sourceRefs: [~/.config/opencode/opencode.json, C:\Temp\opencode\probe_qwen2.ps1 output 2026-11-09]
lastReviewed: 2026-11-09
---

# Llama-Swap Router (local LLM gateway)

Single gateway for all local/virtual models on this machine.

## Topology

- A **PuTTY tunnel** forwards local `127.0.0.1:11437` (and `::1:11437`) to a **llama-swap** router on the remote GPU server ("Pennyroyal", 2× GPUs).
- llama-swap fronts **SGLang** and **vLLM/llama.cpp** backends and swaps them in/out of GPU memory on demand. Verify the listener before anything else: `Get-NetTCPConnection -LocalPort 11437 -State Listen`.
- OpenAI-compatible API at `http://127.0.0.1:11437/v1`.

## Auth

- All `/v1/*` endpoints require `Authorization: Bearer sk-llama-local`.
  Without a key → **HTTP 401** (easy to misread as "model broken").
- Historical: an older key `sk-vllm-local` is still referenced in 2026-11-06 session logs; current valid one is `sk-llama-local`.

## Models relevant to OpenCode

| Router id | Backend | Status pattern |
|---|---|---|
| `Qwen3.8-Flash-Next-NVFP4` (alias `/mnt/llm/models/Qwen3.8-Flash-Next-NVFP4`) | SGLang, NVFP4, TP1 GPU 0, 512k ctx | primary chat model |
| `Qwen3.8-Flash-Next-vLLM` | vLLM NVFP4 TP=2, 256k ctx | fallback, normally unloaded |
| `Clef` / `Clef-Flash` (alias `ggml-org/...`) | decision models, `/v1/systemone` | seen returning HTTP 200 **with an error body** on 2026-11-02 |

Backend chat responses echo `"model":"pennyroyal"` (the SGLang served-model-name), not the requested id — strict clients validating the response model field will choke.

## OpenCode wiring (`~/.config/opencode/opencode.json`)

- Two provider entries share the same baseURL: `llamacpp` (Qwen3.6-35B-A3B-MTP, Ornith-1.5-9B) and `vllm` (`qwen3.8-flash-next` → `modelID: /mnt/llm/models/Qwen3.8-Flash-Next-NVFP4`, apiKey `sk-llama-local`, context 524288 / output 131072, variants low/medium/xhigh mapped to `reasoningEffort`).
- OpenCode model reference: **`vllm/qwen3.8-flash-next`** — never the raw router id.
- `/v1/models` `status.value` (loaded/unloaded) is the quickest health check; swapping an unloaded 125B model in costs tens of seconds and can trip client timeouts.

Related: [[concepts/qwen38-flash-next-troubleshooting]]
