# 도구

## RAG 도구

| 도구 | 설명 |
|------|------|
| `search_knowledge_base` | 지식 베이스 검색 |
| `memorize_document` | 문서 저장 |
| `forget_document` | 문서 삭제 |

## 등록

```csharp
// RAG 도구
services.AddFluxRagTools(options =>
{
    options.DefaultMaxResults = 5;
    options.DefaultSearchStrategy = "hybrid";
    options.DefaultMinScore = 0.5f;
    options.MaxContextTokens = 4000;
});
```

## RagContextBuilder

```csharp
var contextBuilder = provider.GetRequiredService<RagContextBuilder>();

var context = contextBuilder.BuildContext(searchResults, new RagContextOptions
{
    Query = "검색 쿼리",
    MaxResults = 5,
    MinScore = 0.5f
});

// context.ContextText - LLM에 전달할 컨텍스트
// context.Sources - 소스 목록
// context.TokenCount - 토큰 수
// context.AverageRelevance - 평균 관련성
```
