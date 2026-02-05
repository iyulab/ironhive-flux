# 어댑터

## Embedding 어댑터

| 클래스 | 타겟 | 주요 메서드 |
|--------|------|------------|
| `IronHiveEmbeddingServiceForFileFlux` | FileFlux | `GenerateEmbeddingAsync`, `GenerateBatchEmbeddingsAsync` |
| `IronHiveEmbeddingServiceForWebFlux` | WebFlux | `GetEmbeddingAsync`, `GetBatchEmbeddingsAsync` |
| `IronHiveEmbeddingServiceForFluxIndex` | FluxIndex | `GenerateEmbeddingAsync`, `GetModelName` |

## TextCompletion 어댑터

| 클래스 | 타겟 | 주요 메서드 |
|--------|------|------------|
| `IronHiveTextCompletionServiceForFileFlux` | FileFlux | `AnalyzeStructureAsync`, `SummarizeContentAsync`, `ExtractMetadataAsync` |
| `IronHiveTextCompletionServiceForWebFlux` | WebFlux | `CompleteAsync`, `CompleteStreamAsync` |
| `IronHiveTextCompletionServiceForFluxIndex` | FluxIndex | `GenerateCompletionAsync`, `GenerateJsonCompletionAsync` |

## ImageToText 어댑터

| 클래스 | 타겟 | 주요 메서드 |
|--------|------|------------|
| `IronHiveImageToTextServiceForFileFlux` | FileFlux | `ExtractTextAsync` (byte[], Stream, path) |
| `IronHiveImageToTextServiceForWebFlux` | WebFlux | `ConvertImageToTextAsync`, `ExtractTextFromWebImageAsync` |

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
