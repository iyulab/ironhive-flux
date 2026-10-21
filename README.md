# IronHive.Flux

> **Deprecated (2026-10-21).** These packages are no longer developed or released. The last published version (0.17.1) stays on
> nuget.org. To combine IronHive with the Flux packages, reference FileFlux, WebFlux, FluxIndex and FluxFeed directly; WebLookup and
> SystemHarness are consumed directly as well.
>
> **더 이상 개발·게시하지 않는다(2026-10-21).** 마지막 게시판 0.17.1 은 nuget.org 에 남는다. IronHive 와 Flux 패키지를 함께 쓰려면 FileFlux ·
> WebFlux · FluxIndex · FluxFeed 를 직접 참조한다. WebLookup · SystemHarness 도 직접 소비한다.

[![NuGet](https://img.shields.io/nuget/v/IronHive.Flux?label=IronHive.Flux)](https://www.nuget.org/packages/IronHive.Flux)
[![NuGet](https://img.shields.io/nuget/v/IronHive.Flux.Core?label=IronHive.Flux.Core)](https://www.nuget.org/packages/IronHive.Flux.Core)
[![NuGet](https://img.shields.io/nuget/v/IronHive.Flux.Rag?label=IronHive.Flux.Rag)](https://www.nuget.org/packages/IronHive.Flux.Rag)
[![Build](https://github.com/iyulab/ironhive-flux/actions/workflows/nuget-publish.yml/badge.svg)](https://github.com/iyulab/ironhive-flux/actions/workflows/nuget-publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

IronHive와 Flux 생태계(FileFlux, WebFlux, FluxIndex)를 연결하는 브릿지 SDK.
Flux 통합 외에, ironhive 에이전트를 위한 외부 도구/파이프라인 통합(WebLookup, system-harness)도 이 레포가 소유한다 — 별도 통합 지점을 신설하지 않고 기존 브릿지 레이어에 얹는다.

## 패키지

| Package | Description |
|---------|-------------|
| `IronHive.Flux.Core` | 핵심 어댑터 (Embedding, TextCompletion, ImageToText) — IronHive 모델을 FileFlux · WebFlux · FluxCurator · FluxIndex 포트로 |
| `IronHive.Flux.Rag` | RAG 도구 (검색, 메모라이즈, 상태 조회) |
| `IronHive.Flux` | 메타패키지 — `IronHive.Flux.Core` + `IronHive.Flux.Rag` 만 묶는다(WebLookup · Tools 패키지는 따로 설치) |
| `IronHive.Flux.WebLookup` | WebLookup → WebFlux → FluxIndex RAG 파이프라인 |
| `IronHive.Tools.WebLookup` | 에이전트용 웹 검색/탐색 FunctionTool |
| `IronHive.Tools.SystemHarness` | system-harness MCP 서버 통합 확장 |

## 기능

| 기능 | 진입점 | 켜는 법 |
|---|---|---|
| IronHive 모델을 Flux 포트로 | `AddIronHiveFluxCore(o => …)`(모델 id 등 옵션만 등록) + 임베딩·텍스트 완성·이미지→텍스트 어댑터 | opt-in. `AddIronHiveFileFluxAdapters` · `AddIronHiveWebFluxAdapters` · `AddIronHiveFluxIndexAdapters`(또는 `AddAllIronHiveFluxAdapters`)로 각 Flux 패키지의 포트에 연결 — 텍스트 완성은 공용 `Flux.Abstractions.ITextCompletionService` 하나(`IronHiveTextCompletionService`)를 모든 패키지가 함께 받는다 — 임베딩은 어댑터 없이 `AddIronHiveEmbeddingGenerator()`(각 `Add…Adapters` 가 부른다)가 IronHive 생성기를 컨테이너의 `Microsoft.Extensions.AI` 생성기로 한 번 등록하고(`EmbeddingDimension` 을 주면 그 크기를 메타데이터로 알린다), FileFlux · WebFlux 의 시맨틱 청킹과 FluxIndex 가 그것을 받는다. 어댑터는 IronHive `IEmbeddingGenerator` · `IMessageGenerator` 를 컨테이너에서 받으므로 그 둘을 먼저 등록한다(예: `services.AddSingleton(hive.GetEmbeddingGenerator("openai"))`) |
| 에이전트용 RAG 도구 | `AddFluxRagTools(o => …)` → `provider.GetFluxRagTools()` — `search_knowledge_base` · `memorize_document(s)` · `memorize_directory` · `memorize_web_page` · `forget_document` · `list_documents` · `get_document_info` · `detect_changes` · `knowledge_base_status` | opt-in. FluxFeed `IVault` 가 먼저 등록돼 있어야 한다. `memorize_web_page` 는 WebFlux 로 추출하므로(robots.txt · 요청 타임아웃 · 보일러플레이트 제거) `services.AddWebFlux()` 가 등록돼 있을 때만 포함된다 |
| 적재 경로 보안 | `ConfigureVaultSecurity(o => …)` — `AllowedBasePaths`(비면 제한 없음) · `MaxFileSizeBytes`(기본 100 MB) · `RejectSymlinks`(기본 true) | 기본값으로 켜져 있다. `AddFluxRagTools` 앞이든 뒤든 호출하면 memorize 도구가 그 값을 쓴다 |
| RAG 컨텍스트 조립 | `AddFluxRagContext(…)` → `RagContextBuilder.BuildContextAsync` | opt-in |
| 웹 검색 → RAG 적재 | `AddWebLookupRagPipeline(…)` → `WebLookupRagPipeline`(`DiscoverUrlsAsync` · `DiscoverSitemapUrlsAsync` · `DiscoverCombinedUrlsAsync`) | opt-in. WebLookup(`services.AddWebLookup(…)`)이 먼저 |
| 에이전트용 웹 도구 | `services.AddWebLookupTools(…)` + `tools.AddWebLookupTools(provider)` — `web_search` · `explore_site` | opt-in. WebLookup 이 먼저 |
| 컴퓨터 제어(MCP) | `McpClientManager.AddSystemHarness(…)` — system-harness MCP 서버의 help/do/get 3 도구 | opt-in. 연결되면 매니저의 `IToolCollection` 에 도구가 채워진다 |

## 시나리오

### RAG Chatbot
```
IronHive + FluxIndex (FluxFeed IVault)
→ 지식기반 챗봇 서비스
```

예제: [samples/RagChatbotSample](samples/RagChatbotSample/Program.cs) — RAG 도구 목록과 `RagContextBuilder` 컨텍스트 조립.

## Quick Start

```csharp
// Core 설정 (옵션)
services.AddIronHiveFluxCore(options =>
{
    options.EmbeddingModelId = "text-embedding-3-small";
    options.TextCompletionModelId = "gpt-4o";
});

// Flux 포트 어댑터 (IronHive IEmbeddingGenerator / IMessageGenerator 가 등록돼 있어야 한다)
services.AddAllIronHiveFluxAdapters();

// RAG 도구
services.AddFluxRagTools(options =>
{
    options.DefaultMaxResults = 5;
    options.DefaultSearchStrategy = "hybrid";
});
```

## Build

```bash
dotnet build IronHive.Flux.slnx
dotnet test
```

## Docs

- [아키텍처](docs/architecture.md)
- [어댑터](docs/adapters.md)
- [도구](docs/tools.md)

## License

MIT
