# 어댑터

## Embedding — 어댑터 클래스 없음

`AddIronHiveEmbeddingGenerator()`(각 `Add…Adapters` 가 함께 부른다)가 IronHive 의 `IEmbeddingGenerator` 를 컨테이너의
`Microsoft.Extensions.AI` `IEmbeddingGenerator<string, Embedding<float>>` 로 한 번 등록한다(`IronHive.Extensions.AI` 의
`AsEmbeddingGenerator` — 모델은 `EmbeddingModelId`, `EmbeddingDimension` 을 주면 메타데이터의 `DefaultModelDimensions` 로 알리고
벡터 크기를 검사). 이미 등록된 생성기가 있으면 그것을 쓴다. 라이브러리마다 그 생성기를 직접 받는다:

| 라이브러리 | 받는 길 | 차원 |
|--------|------|------------|
| FileFlux (Semantic 청킹) | `AddFileFlux()` 가 컨테이너 생성기로 FluxCurator `EmbeddingGeneratorEmbedder` 를 만든다 | 생성기 메타데이터 — 없으면 Semantic 청킹만 실패 |
| WebFlux (Semantic 청킹) | `AddIronHiveWebFluxAdapters` 가 FluxCurator `EmbeddingGeneratorEmbedder` 를 `IEmbedder` 로 등록 | 생성기 메타데이터 |
| FluxIndex | `AddIronHiveFluxIndexAdapters` 가 FluxIndex 의 `EmbeddingGeneratorEmbeddingService` 를 등록 | `EmbeddingDimension` 필수(해석 시 이름을 대며 실패) |

## TextCompletion 어댑터

| 클래스 | 타겟 | 주요 메서드 |
|--------|------|------------|
| `IronHiveTextCompletionServiceForFileFlux` | `FileFlux.IDocumentAnalysisService` | `GenerateAsync` (both overloads — `GenerationSettings` reach the request; a response cut off at the output limit throws `GenerationTruncatedException`), `IsAvailableAsync`, `ProviderInfo` |
| `IronHiveTextCompletionService` | `Flux.Abstractions.ITextCompletionService` (WebFlux, FluxIndex, FluxImprover, …) | `CompleteAsync`, `CompleteJsonAsync`, `CompleteStreamAsync`, `CompleteBatchAsync` |

## ImageToText 어댑터

| 클래스 | 타겟 | 주요 메서드 |
|--------|------|------------|
| `IronHiveImageToTextServiceForFileFlux` | FileFlux | `ExtractTextAsync` (byte[], Stream, path) |

## 등록

```csharp
services.AddIronHiveFluxCore(options =>
{
    options.EmbeddingModelId = "text-embedding-3-small";
    options.TextCompletionModelId = "gpt-4o";
    options.ImageToTextModelId = "gpt-4o";
    options.EmbeddingDimension = 1536;
    options.DefaultTemperature = 0.7f;
});
```
