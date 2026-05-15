# 도구

## RAG 도구

| 도구 클래스 | 설명 |
|------------|------|
| `FluxIndexSearchTool` | 지식 베이스 검색 (vector / hybrid / keyword) |
| `FluxIndexMemorizeTool` | 파일을 지식 베이스에 저장 |
| `FluxIndexUnmemorizeTool` | 지식 베이스에서 파일 삭제 |
| `FluxIndexBatchMemorizeTool` | 여러 파일 또는 디렉토리를 일괄 저장 |
| `FluxIndexWebMemorizeTool` | 웹 페이지 URL 콘텐츠를 저장 |
| `FluxIndexStatusTool` | 지식 베이스 상태 조회 및 문서 목록 확인 |

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
