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
