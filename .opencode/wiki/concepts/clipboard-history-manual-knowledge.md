---
title: Clipboard history, manual generator, ClickHouse knowledge (RAG)
tags: [clipboard, history, manual, rag, clickhouse, opencoderag, vision]
sourceRefs: [ClippyAI/Services/ClipboardHistoryService.cs, ClippyAI/Services/ClickHouseKnowledgeService.cs, ClippyAI/Services/OpenAIService.cs, ClippyAI/Models/OpenAIModels.cs, ClippyAI/ViewModels/MainViewModel.cs, ClippyAI/Views/ClipboardHistoryWindow.axaml, ClippyAI/Services/OllamaService.cs]
lastReviewed: 2026-11-09
---

# Clipboard history, manual generator, ClickHouse knowledge (v1.7.0)

## Clipboard history (`ClipboardHistoryService`)

SQLite table `ClipboardHistory` (Id, CreatedAt, Kind Text|Image, Content, ImageFile, Hash, Context) inside the main config DB; images as PNG files named `<sha256>.png` under `%APPDATA%\ClippyAI\History`. Consecutive identical payloads only refresh `CreatedAt`. Cap = config `ClipboardHistoryLimit` (200) pruned on write, files deleted when unreferenced. `ClipboardHistoryEnabled=False` disables recording. Image rows capture the foreground window at record time (`Context` = "Product — 'title' — process.exe version", Win32 P/Invokes) — used to pre-fill the manual dialog. UI: `ClipboardHistoryWindow` (ListBox with Extended selection, thumbnails, filter box) reachable from the panel clock button and tray; restore/execute/delete/clear + filter via MainViewModel commands (window DataContext = MainViewModel).

## Monitoring correctness (foundation, same release)

Old flaws fixed in `MainViewModel.UpdateClipboardContent`: image changes are detected via SHA-256 over the PNG encoding (the clipboard returns a fresh Bitmap instance every poll — reference comparison saw false changes every second), gated on Windows by `GetClipboardSequenceNumber` (see [[concepts/global-hotkeys]] sibling plumbing: `ClipboardService.ImagePayloadMayHaveChanged/MarkImageSynced/GetImageSnapshotAsync`); images are now evaluated even when text changed at the same time; old bitmaps are disposed; AutoMode text trigger skips image tasks (loop guard), AutoMode fires `CaptureAndAnalyze` automatically when a *new* clipboard image arrives while a clipboard-sourced image task is selected.

## Manual generator (`MainViewModel.GenerateManualAsync`)

Entry point: History window → select screenshots → Generate Manual → `ManualContextDialog`/`ManualDialogViewModel` (app name/version/notes/audience/language; profiles persisted as `ManualProfile::<name>` keys + `ManualProfileList`). Pipeline: per screenshot one `OllamaService.AnalyzeImage` pass with a dynamic section prompt (`BuildSectionPrompt`, grounding instructions, window context line, screenshot i/N), then one assembly pass (`SendRequestWithConfig` with `BuildAssemblyPrompt`) → Markdown manual → `SetResponse` (output+clipboard) and optional save-file (`StorageProvider.SaveFilePickerAsync`). Model/params come from the default task **Write Application Manual** (`ConfigurationService.EnsureManualTask`, IsImageTask, MaxLength≥4096, NumCtx≥8192 via `CloneForPrompt`).

## Vision provider paths (fixed in v1.7.2)

`AnalyzeImage` exists on both providers. The Ollama provider passes base64 images natively. The OpenAI provider (`OpenAIService.AnalyzeImage`) had two latent bugs, surfaced only by the real GUI + llama-swap (vLLM backend): (1) it computed the base64 image and discarded it (`_ =`), sending `content` as a plain string — the image was never actually transmitted; now it sends proper multimodal parts `content=[{type:"text"},{type:"image_url",url:"data:image/png;base64,…"}]` via `OpenAIVisionMessage`/`OpenAIContentPart` (`OpenAIRequest.Messages` is `object[]`, STJ serializes runtime types). (2) Unset nullable `OpenAIRequest` properties serialized as explicit `null` (`"stream": null`, `"frequency_penalty": null`) which strict pydantic backends reject with HTTP 400 — all request serialization now uses `JsonIgnoreCondition.WhenWritingNull`. Vision `MaxTokens` = `RequestLimits.MaxOutputTokens` (8192). Error surfaced as the app's own `ErrorMessageDialog` (class `ErrorMessageDialog`, UIA title "Window").

Follow-up (v1.7.3): the manual flow still showed an error dialog *after* successfully generating — `SetResponse` updated the response counter via `_responseList[_responseIndex]`, but that list is only filled by the embedding-similarity task flow, so non-embedding flows (manual, plain task) threw `IndexOutOfRangeException` (message: "Index was out of range … (Parameter 'index')") inside the try block after the output was already displayed. Now bounds-guarded. Both fixes verified live on installed MSIs via winvision: two consecutive full manual generations from a real STARLIMS screenshot, second run with zero error dialogs.

## ClickHouse knowledge (`ClickHouseKnowledgeService`)

HTTP interface only (POST SQL to `ClickHouseUrl`, Basic auth, `UseProxy=false` — corporate proxy 403s hostnames; use the IP, see quirk). Reads OpenCodeRAG layout: `chunks_<Project>_<hash>` tables (columns incl. `file_path, kind, tags, content, description, embedding Array(Float32)=4096, is_deleted`); quirks are rows `kind='quirk'`, `file_path='quirk:quirk:<uuid>'`. Retrieval modes: `vector` = `cosineDistance(embedding,[…])` brute force (query embedded via EmbeddingService; dimension probe cached, mismatch → skip), `keyword` = top-6 terms `ILIKE` (works with any embedding setup), `auto` = vector→keyword fallback. Config keys: `ClickHouseEnabled/Url/User/Password/Database/Tables/Mode/TopK/MaxChars`; junk tables `chunks_opencode_rag_*` excluded; results cached 10 min; 2-minute circuit breaker; failures are always non-fatal.

Injection point is central: `OllamaService.SendRequestWithConfig` and `AnalyzeImage` clone the task config with `SystemPrompt += knowledge block` (`WithKnowledgeContextAsync`) — every text task, image analysis and manual pass get RAG without call-site changes. Settings UI: Knowledge tab (test + discover buttons use unsaved form values via method overrides).
