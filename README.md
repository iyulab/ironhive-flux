# IronHive.Flux

IronHive와 Flux 생태계(FileFlux, WebFlux, FluxIndex)를 연결하는 브릿지 SDK.

## 패키지

| Package | Description |
|---------|-------------|
| `IronHive.Flux.Core` | 핵심 어댑터 (Embedding, TextCompletion, ImageToText) |
| `IronHive.Flux.Rag` | RAG 도구 (검색, 메모라이즈) |
| `IronHive.Flux` | 메타패키지 |

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
