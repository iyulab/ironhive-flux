# IronHive.Flux

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
| `IronHive.Flux.Core` | 핵심 어댑터 (Embedding, TextCompletion, ImageToText) |
| `IronHive.Flux.Rag` | RAG 도구 (검색, 메모라이즈, 상태 조회) |
| `IronHive.Flux` | 메타패키지 |
| `IronHive.Flux.WebLookup` | WebLookup → WebFlux → FluxIndex RAG 파이프라인 |
| `IronHive.Tools.WebLookup` | 에이전트용 웹 검색/탐색 FunctionTool |
| `IronHive.Tools.SystemHarness` | system-harness MCP 서버 통합 확장 |

## 기능

| 기능 | 진입점 | 켜는 법 |
|---|---|---|
| IronHive 모델을 Flux 포트로 | `AddIronHiveFluxCore(o => …)` — 임베딩·텍스트 완성·이미지→텍스트 어댑터 | opt-in. `AddIronHiveFileFluxAdapters` · `AddIronHiveWebFluxAdapters` · `AddIronHiveFluxIndexAdapters`(또는 `AddAllIronHiveFluxAdapters`)로 각 Flux 패키지의 포트에 연결 |
| 에이전트용 RAG 도구 | `AddFluxRagTools(o => …)` → `provider.GetFluxRagTools()` — `search_knowledge_base` · `memorize_document(s)` · `memorize_directory` · `memorize_web_page` · `forget_document` · `list_documents` · `get_document_info` · `detect_changes` · `knowledge_base_status` | opt-in. FluxFeed `IVault` 가 먼저 등록돼 있어야 한다. `memorize_web_page` 는 WebFlux 로 추출하므로(robots.txt · 요청 타임아웃 · 보일러플레이트 제거) `services.AddWebFlux()` 가 등록돼 있을 때만 포함된다 |
| RAG 컨텍스트 조립 | `AddFluxRagContext(…)` → `RagContextBuilder.BuildContextAsync` | opt-in |
| 웹 검색 → RAG 적재 | `AddWebLookupRagPipeline(…)` → `WebLookupRagPipeline`(`DiscoverUrlsAsync` · `DiscoverSitemapUrlsAsync` · `DiscoverCombinedUrlsAsync`) | opt-in. WebLookup(`services.AddWebLookup(…)`)이 먼저 |
| 에이전트용 웹 도구 | `services.AddWebLookupTools(…)` + `tools.AddWebLookupTools(provider)` — `web_search` · `explore_site` | opt-in. WebLookup 이 먼저 |
| 컴퓨터 제어(MCP) | `McpClientManager.AddSystemHarness(…)` — system-harness MCP 서버의 help/do/get 3 도구 | opt-in. 연결되면 매니저의 `IToolCollection` 에 도구가 채워진다 |

## 시나리오

### RAG Chatbot
```
IronHive + IronBees + FluxIndex
→ 지식기반 챗봇 서비스
```

## Quick Start

```csharp
// Core 설정
services.AddIronHiveFluxCore(options =>
{
    options.EmbeddingModelId = "text-embedding-3-small";
    options.TextCompletionModelId = "gpt-4o";
});

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
