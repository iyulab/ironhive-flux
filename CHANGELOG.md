# Changelog

All notable changes to this project are documented in this file. Versions follow
[Semantic Versioning](https://semver.org/); while the major version is 0, a minor release may contain
breaking changes, and each one is marked **Breaking** with a migration note.

## [0.8.10] - 2026-09-25

### Changed
- Re-pinned sibling package(s) `FluxIndex.Core` 0.51.4 -> 0.52.0, `FluxIndex.SDK` 0.51.4 -> 0.52.0, `IronHive.Abstractions` 0.38.0 -> 0.39.0, `IronHive.Core` 0.38.0 -> 0.39.0, `IronHive.Plugins.MCP` 0.38.0 -> 0.39.0, `IronHive.Providers.OpenAI` 0.38.0 -> 0.39.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. The embedding adapters read `EmbeddingResponse.Results` (IronHive 0.39.0 changed `EmbedBatchAsync`'s return type); their behaviour is unchanged.
- Re-pinned sibling package(s) `FileFlux` 0.29.0 -> 0.29.1 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`.
- Re-pinned sibling package(s) `FluxFeed` 0.34.2 -> 0.34.3 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`.

## [0.8.9] - 2026-09-25

### Changed
- Re-pinned sibling package(s) `FileFlux` 0.28.2 -> 0.29.0, `FluxFeed` 0.34.1 -> 0.34.2, `FluxIndex.Core` 0.51.3 -> 0.51.4, `FluxIndex.SDK` 0.51.3 -> 0.51.4, `IronHive.Abstractions` 0.37.0 -> 0.38.0, `IronHive.Core` 0.37.0 -> 0.38.0, `IronHive.Plugins.MCP` 0.37.0 -> 0.38.0, `IronHive.Providers.OpenAI` 0.37.0 -> 0.38.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.8.8] - 2026-09-24

### Changed
- Re-pinned sibling package(s) `IronHive.Abstractions` 0.36.0 -> 0.37.0, `IronHive.Core` 0.36.0 -> 0.37.0, `IronHive.Plugins.MCP` 0.36.0 -> 0.37.0, `IronHive.Providers.OpenAI` 0.36.0 -> 0.37.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.8.7] - 2026-09-24

### Changed
- **`search_knowledge_base` asks the vault to rerank instead of reranking itself.** With an `IReranker` registered
  the tool sets `VaultSearchOptions.UseReranker` (candidate pool `topK * 2`, as before) and FluxFeed reranks; the
  results, their order and scores are the same, and `MinScore` still filters the retrieval score before reranking.
- Re-pinned sibling package(s) `FileFlux` 0.28.1 -> 0.28.2, `FluxFeed` 0.33.19 -> 0.34.1, `FluxIndex.Core` 0.51.2 -> 0.51.3, `FluxIndex.SDK` 0.51.2 -> 0.51.3, `IronHive.Abstractions` 0.35.0 -> 0.36.0, `IronHive.Core` 0.35.0 -> 0.36.0, `IronHive.Plugins.MCP` 0.35.0 -> 0.36.0, `IronHive.Providers.OpenAI` 0.35.0 -> 0.36.0, `WebFlux` 0.15.0 -> 0.16.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.8.6] - 2026-09-24

### Changed
- Re-pinned sibling package(s) `FileFlux` 0.28.0 -> 0.28.1, `FluxFeed` 0.33.18 -> 0.33.19, `FluxIndex.Core` 0.51.1 -> 0.51.2, `FluxIndex.SDK` 0.51.1 -> 0.51.2, `IronHive.Abstractions` 0.34.0 -> 0.35.0, `IronHive.Core` 0.34.0 -> 0.35.0, `IronHive.Plugins.MCP` 0.34.0 -> 0.35.0, `IronHive.Providers.OpenAI` 0.34.0 -> 0.35.0, `TokenMeter` 0.7.7 -> 0.7.8 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.8.5] - 2026-09-24

### Changed
- Re-pinned sibling package(s) `FluxFeed` 0.33.17 -> 0.33.18, `FluxIndex.Core` 0.51.0 -> 0.51.1, `FluxIndex.SDK` 0.51.0 -> 0.51.1, `TokenMeter` 0.7.6 -> 0.7.7, `WebFlux` 0.14.1 -> 0.15.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.8.4] - 2026-09-24

### Changed
- Re-pinned sibling package(s) `FileFlux` 0.27.1 -> 0.28.0, `FluxFeed` 0.33.16 -> 0.33.17, `FluxIndex.Core` 0.50.6 -> 0.51.0, `FluxIndex.SDK` 0.50.6 -> 0.51.0, `IronHive.Abstractions` 0.33.1 -> 0.34.0, `IronHive.Core` 0.33.1 -> 0.34.0, `IronHive.Plugins.MCP` 0.33.1 -> 0.34.0, `IronHive.Providers.OpenAI` 0.33.1 -> 0.34.0, `TokenMeter` 0.7.5 -> 0.7.6 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.8.3] - 2026-09-23

### Added
- **The FluxIndex and WebFlux completion adapters report truncation when asked.** With
  `TextCompletionOptions.ThrowOnTruncation` set, `CompleteAsync` and `CompleteJsonAsync` throw
  `Flux.Abstractions.TextCompletionTruncatedException` (carrying the request's `MaxTokens`) when IronHive reports that the
  model stopped at the output limit, instead of returning the cut-off text. Without the option nothing changes. This is
  what makes WebFlux 0.14.1's rewrite protection and FluxIndex's FileFlux adapter reach an IronHive model. The WebFlux
  adapter's streaming path does not apply it (the text is already out when the stream says why it stopped).

### Changed
- Re-pinned sibling package(s) `FileFlux` 0.26.1 -> 0.27.1, `FluxFeed` 0.33.14 -> 0.33.16, `FluxIndex.Core` 0.50.4 -> 0.50.6, `FluxIndex.SDK` 0.50.4 -> 0.50.6, `WebFlux` 0.14.0 -> 0.14.1 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`.

## [0.8.2] - 2026-09-23

### Changed
- Re-pinned sibling package(s) `FileFlux` 0.26.0 -> 0.26.1, `FluxFeed` 0.33.13 -> 0.33.14, `FluxIndex.Core` 0.50.2 -> 0.50.4, `FluxIndex.SDK` 0.50.2 -> 0.50.4 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.8.1] - 2026-09-23

### Fixed
- **The two `Flux.Abstractions.ITextCompletionService` adapters send every option IronHive can carry.**
  `IronHiveTextCompletionServiceForWebFlux` and `…ForFluxIndex` sent only temperature and output budget: a caller's
  `SystemPrompt`, `TopP`, `StopSequences`, `ResponseFormat` ("json") and `ResponseSchema` were dropped. They now reach the
  request (`ResponseSchema` → `OutputFormat.For(schema)`, `ResponseFormat = "json"` → `OutputFormat.Json`).
  `FrequencyPenalty` / `PresencePenalty` have no IronHive counterpart and are still not sent.
- **`IronHiveTextCompletionServiceForWebFlux.CompleteJsonAsync` returns JSON.** It fell through to the interface
  default — a plain completion returned as it came, code fence and surrounding prose included. It now does what the
  FluxIndex adapter does: the JSON instruction in the system prompt (after the caller's own), temperature 0.1, and the
  outermost JSON object or array extracted from the answer. Provider JSON mode is used only when the options ask for it,
  since some providers' JSON mode admits only a top-level object.

## [0.8.0] - 2026-09-23

### Fixed
- **`GetWebLookupTools()` / `IToolCollection.AddWebLookupTools(provider)` attach `web_search` and `explore_site`.** Same
  defect as below: the provider's `Type` was passed where the factory expects the instance, so both returned or attached
  nothing. The resolved (singleton) provider is now bound directly.
- **`GetFluxRagTools()` returns the tools.** It passed each tool's `Type` object where `FunctionToolFactory.CreateFrom`
  expects the tool instance, so the factory looked for `[FunctionTool]` methods on `System.Type` and the method always
  returned an empty list. Tools are now created with `CreateFrom<T>(provider)` and resolved from the provider on each
  call (the tool classes are scoped; nothing is built from the root provider).
- **FileFlux LLM refinement through `IronHiveTextCompletionServiceForFileFlux` no longer adopts a cut-off rewrite and
  gets the output budget it asks for.** The adapter did not implement `GenerateAsync(prompt, GenerationSettings, ct)`,
  so FileFlux's temperature and output budget were dropped and every pass ran on `DefaultCompletionMaxTokens` (500) —
  a document past a couple of kilobytes came back cut off. The settings now reach the request (unset values fall back
  to `DefaultTemperature` / `DefaultCompletionMaxTokens`), and a response that stopped at the output limit
  (`MessageDoneReason.MaxTokens`) throws FileFlux's `GenerationTruncatedException`, which the refiner treats as "not
  applied" and reports in `LlmRefinementInfo.Warnings`.

### Changed
- **Breaking**: `IronHiveTextCompletionServiceForFileFlux.GenerateAsync` throws `GenerationTruncatedException` when the
  model stops at the output token limit, instead of returning the partial text. Its `ProviderInfo.MaxContextLength` is
  0 (not declared) instead of a fixed 128000 that did not describe the configured model — FileFlux skips its context
  check for 0.
- **Breaking**: `memorize_web_page` (`FluxIndexWebMemorizeTool`) extracts through WebFlux's `IContentExtractService`
  instead of its own `HttpClient` and regex tag stripping. robots.txt, the per-request timeout, boilerplate removal
  and Markdown conversion now follow WebFlux (its defaults and configuration) — previously this tool ignored
  robots.txt, set no timeout of its own beyond 30 s, and could not render dynamic pages. The page's own title is used
  when no title is passed. The constructor takes `IContentExtractService` in place of `HttpClient?`, and the class is no
  longer `IDisposable`. The tool is offered by `GetFluxRagTools()` only when the host registered WebFlux
  (`services.AddWebFlux()`).
- Re-pinned sibling package(s) `FileFlux` 0.25.1 -> 0.26.0, `IronHive.Abstractions` 0.33.0 -> 0.33.1, `IronHive.Core` 0.33.0 -> 0.33.1, `IronHive.Plugins.MCP` 0.33.0 -> 0.33.1, `IronHive.Providers.OpenAI` 0.33.0 -> 0.33.1 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.
- Re-pinned sibling package(s) `FluxIndex.Core` 0.50.1 -> 0.50.2, `FluxIndex.SDK` 0.50.1 -> 0.50.2 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.
- Re-pinned sibling package(s) `FluxFeed` 0.33.12 -> 0.33.13 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.7.0] - 2026-09-23

### Changed
- Re-pinned sibling package(s) `WebFlux` 0.13.0 -> 0.14.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.
- Re-pinned sibling package(s) `FileFlux` 0.25.0 -> 0.25.1, `FluxIndex.Core` 0.49.0 -> 0.50.0, `FluxIndex.SDK` 0.49.0 -> 0.50.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.
- Re-pinned sibling package(s) `FluxIndex.Core` 0.50.0 -> 0.50.1, `FluxIndex.SDK` 0.50.0 -> 0.50.1 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.
- Re-pinned sibling package(s) `FluxFeed` 0.33.11 -> 0.33.12 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

### Removed
- **Breaking**: `IronHiveImageToTextServiceForWebFlux` and its registration in `AddIronHiveWebFluxAdapters`. WebFlux
  0.14.0 removes the `IImageToTextService` port it implemented — no WebFlux crawl, extraction or chunking path ever
  called it, so the adapter never received a request through WebFlux. Nothing to migrate: code that constructed the
  adapter directly can call `IMessageGenerator` itself. The FileFlux image-to-text adapter
  (`IronHiveImageToTextServiceForFileFlux`) is unchanged.

## [0.6.54] - 2026-09-22

### Changed
- Re-pinned sibling package(s) `FileFlux` 0.24.1 -> 0.25.0, `FluxFeed` 0.33.10 -> 0.33.11, `FluxIndex.Core` 0.48.1 -> 0.49.0, `FluxIndex.SDK` 0.48.1 -> 0.49.0, `WebFlux` 0.12.0 -> 0.13.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.53] - 2026-09-22

### Changed
- Re-pinned sibling package(s) `FileFlux` 0.23.20 -> 0.24.1, `FluxFeed` 0.33.9 -> 0.33.10, `FluxIndex.Core` 0.48.0 -> 0.48.1, `FluxIndex.SDK` 0.48.0 -> 0.48.1 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.52] - 2026-09-22

### Changed
- Re-pinned sibling package(s) `FluxFeed` 0.33.8 -> 0.33.9, `FluxIndex.Core` 0.47.0 -> 0.48.0, `FluxIndex.SDK` 0.47.0 -> 0.48.0, `WebFlux` 0.11.0 -> 0.12.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.51] - 2026-09-21

### Changed
- Re-pinned sibling package(s) `FileFlux` 0.23.19 -> 0.23.20, `FluxFeed` 0.33.7 -> 0.33.8, `FluxIndex.Core` 0.46.4 -> 0.47.0, `FluxIndex.SDK` 0.46.4 -> 0.47.0, `WebFlux` 0.10.0 -> 0.11.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.50] - 2026-09-21

### Changed
- Re-pinned sibling package(s) `FileFlux` 0.23.18 -> 0.23.19, `FluxFeed` 0.33.6 -> 0.33.7, `FluxIndex.Core` 0.46.3 -> 0.46.4, `FluxIndex.SDK` 0.46.3 -> 0.46.4, `WebFlux` 0.9.0 -> 0.10.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.49] - 2026-09-21

### Changed
- Re-pinned sibling package(s) `FluxFeed` 0.33.5 -> 0.33.6, `FluxIndex.Core` 0.46.2 -> 0.46.3, `FluxIndex.SDK` 0.46.2 -> 0.46.3 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.48] - 2026-09-20

### Changed
- Re-pinned sibling package(s) `FluxFeed` 0.33.4 -> 0.33.5, `FluxIndex.Core` 0.46.1 -> 0.46.2, `FluxIndex.SDK` 0.46.1 -> 0.46.2, `WebFlux` 0.8.0 -> 0.9.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.47] - 2026-09-20

### Changed
- Re-pinned sibling package(s) `FluxFeed` 0.33.3 -> 0.33.4, `FluxIndex.Core` 0.46.0 -> 0.46.1, `FluxIndex.SDK` 0.46.0 -> 0.46.1 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.46] - 2026-09-20

### Changed
- Re-pinned sibling package(s) `FluxFeed` 0.33.2 -> 0.33.3, `FluxIndex.Core` 0.45.0 -> 0.46.0, `FluxIndex.SDK` 0.45.0 -> 0.46.0, `IronHive.Abstractions` 0.32.0 -> 0.33.0, `IronHive.Core` 0.32.0 -> 0.33.0, `IronHive.Plugins.MCP` 0.32.0 -> 0.33.0, `IronHive.Providers.OpenAI` 0.32.0 -> 0.33.0, `WebFlux` 0.7.4 -> 0.8.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.45] - 2026-09-20

### Changed
- Re-pinned sibling package(s) `FluxFeed` 0.33.1 -> 0.33.2, `FluxIndex.Core` 0.44.7 -> 0.45.0, `FluxIndex.SDK` 0.44.7 -> 0.45.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.44] - 2026-09-20

### Changed
- Re-pinned sibling package(s) `FluxFeed` 0.33.0 -> 0.33.1, `FluxIndex.Core` 0.44.6 -> 0.44.7, `FluxIndex.SDK` 0.44.6 -> 0.44.7 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.43] - 2026-09-20

### Changed
- Re-pinned sibling package(s) `FluxFeed` 0.32.0 -> 0.33.0, `FluxIndex.Core` 0.44.5 -> 0.44.6, `FluxIndex.SDK` 0.44.5 -> 0.44.6 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.42] - 2026-09-19

### Changed
- Re-pinned sibling package(s) `IronHive.Abstractions` 0.31.0 -> 0.32.0, `IronHive.Core` 0.31.0 -> 0.32.0, `IronHive.Plugins.MCP` 0.31.0 -> 0.32.0, `IronHive.Providers.OpenAI` 0.31.0 -> 0.32.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.
- Re-pinned sibling package(s) `FluxFeed` 0.31.4 -> 0.32.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.41] - 2026-09-19

### Changed
- Re-pinned sibling package(s) `IronHive.Abstractions` 0.30.0 -> 0.31.0, `IronHive.Core` 0.30.0 -> 0.31.0, `IronHive.Plugins.MCP` 0.30.0 -> 0.31.0, `IronHive.Providers.OpenAI` 0.30.0 -> 0.31.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.40] - 2026-09-19

### Changed
- Re-pinned sibling package(s) `IronHive.Abstractions` 0.29.1 -> 0.29.2, `IronHive.Core` 0.29.1 -> 0.29.2, `IronHive.Plugins.MCP` 0.29.1 -> 0.29.2, `IronHive.Providers.OpenAI` 0.29.1 -> 0.29.2 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.
- Re-pinned sibling package(s) `IronHive.Abstractions` 0.29.2 -> 0.30.0, `IronHive.Core` 0.29.2 -> 0.30.0, `IronHive.Plugins.MCP` 0.29.2 -> 0.30.0, `IronHive.Providers.OpenAI` 0.29.2 -> 0.30.0 — re-consumption of already-consumed iyulab packages via `check-pin-drift.ps1 -Fix`. No source changes.

## [0.6.39] - 2026-09-19

This file starts at 0.6.39. Changes in earlier releases were not recorded here; the commit history is
the record for them.
