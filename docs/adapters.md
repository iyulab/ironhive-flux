# 어댑터

## Embedding 어댑터

| 클래스 | 타겟 | 주요 메서드 |
|--------|------|------------|
| `IronHiveEmbeddingServiceForFileFlux` | FileFlux | `GenerateEmbeddingAsync`, `GenerateBatchEmbeddingsAsync` |
| `IronHiveEmbedderForFluxCurator` | FluxCurator `IEmbedder` — WebFlux·FileFlux 의 Semantic 청킹 | `GenerateEmbeddingAsync`, `GenerateEmbeddingsAsync`, `CalculateSimilarity` |
| (none — FluxIndex's own `EmbeddingGeneratorEmbeddingService` over `IronHive.Extensions.AI`'s generator) | FluxIndex | registered by `AddIronHiveFluxIndexAdapters`; needs `IronHiveFluxCoreOptions.EmbeddingDimension` |

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
