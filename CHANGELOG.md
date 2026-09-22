# Changelog

All notable changes to this project are documented in this file. Versions follow
[Semantic Versioning](https://semver.org/); while the major version is 0, a minor release may contain
breaking changes, and each one is marked **Breaking** with a migration note.

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
