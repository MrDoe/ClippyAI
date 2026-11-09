---
title: "Qwen3.8-Flash-Next (NVFP4): why it 'does not work'"
tags: [llm, troubleshooting, qwen3.8, sglang, llama-swap, opencode]
sourceRefs: [~/.config/opencode/opencode.json, C:\Users\doellingerchristoph\.local\share\opencode\log\opencode.log, probe scripts C:\Temp\opencode\probe_qwen*.ps1]
lastReviewed: 2026-11-09
---

# Qwen3.8-Flash-Next (NVFP4): why it "does not work"

**Verdict (2026-11-09): the model itself is healthy.** Four live probes through the
router all returned HTTP 200 — plain completion, `reasoning_effort=xhigh`, streaming
with a forced tool call (`finish_reason: tool_calls`, valid streamed arguments), and
`/v1/models` reports the slot **loaded**. The session that ran this investigation was
itself served by this model. In ~2 weeks of OpenCode logs there is **zero** provider
error for `vllm`/`11437` (only unrelated `starlims` MCP ECONNREFUSED on port 3002).

So "does not work" traces to one of the layers around it, in likelihood order:

## 1. Auth — 401 Unauthorized
The router enforces `Authorization: Bearer sk-llama-local`. A probe or client without
the key (or with the old `sk-vllm-local` seen in 2026-11-06 logs) gets 401 and looks
dead. OpenCode's config carries the right key; hand-made curl/PowerShell tests often
don't.

## 2. Dead PuTTY tunnel — ECONNREFUSED 127.0.0.1:11437
The gateway is a **tunnel**, not a local server. Sleep, NIC switch, or a closed PuTTY
window kills every model call at once. Check:
`Get-NetTCPConnection -LocalPort 11437 -State Listen` (owner should be `putty`).

## 3. Mandatory reasoning eats small token budgets → empty answer
The model **always thinks** before answering. Probe with `max_tokens: 16` and no
`reasoning_effort` returned `content: ""`, `finish_reason: "length"`,
`completion_tokens: 16` — **all in `reasoning_content`**. A client (title generators,
classifiers, STARLIMS AI jobs with small `AI_MAX_TOKENS`) that only reads `content`
sees "the model returns nothing". Give it a ≥ few-hundred token budget and/or send
`reasoning_effort` (OpenCode variants low/medium/xhigh already do).

## 4. Model-id confusion
- OpenCode: `vllm/qwen3.8-flash-next` (the `modelID` setting maps it to the router alias).
- Router accepts `Qwen3.8-Flash-Next-NVFP4` **and** `/mnt/llm/models/Qwen3.8-Flash-Next-NVFP4`, nothing else.
- A separate fallback slot `Qwen3.8-Flash-Next-vLLM` (unloaded, 256k ctx) exists — selecting it in a picker pays a full cold start and can hit client timeouts.
- starlimsvscode ≥ 1.8.32 fails **loudly** on unknown ids instead of silently falling back, so stale `STARLIMS.opencode.*Model` settings now surface as errors.

## 5. Router slot pathologies
- 2026-11-02: the Clef slots answered HTTP **200 with an error body** ("logits computation") — always parse the body for `error`, don't trust status alone.
- 4 concurrent jobs on one slot serialize past minutes (llama-swap swaps by GPU memory; Qwen3.8 TP1 shares GPU 0 with Qwen3.6/embeddings).

## Quick health check (reusable)
1. Listener on 11437 → PuTTY alive?
2. `GET /v1/models` with key → Qwen3.8 slot `loaded`?
3. `POST /v1/chat/completions` `max_tokens: 64`, `reasoning_effort: "low"` → non-empty content?

Related: [[entities/llama-swap-router]]
