# 도구

## FileFlux 도구

| 도구 | 설명 |
|------|------|
| `process_document` | 문서를 RAG용 청크로 분할 |
| `analyze_document_structure` | 문서 구조 분석 (섹션, 제목, 계층) |
| `extract_document_metadata` | 메타데이터 추출 (키워드, 언어, 카테고리) |

## WebFlux 도구

| 도구 | 설명 |
|------|------|
| `crawl_web_page` | 웹 페이지 크롤링 |
| `analyze_web_content` | 웹 콘텐츠 분석 |
| `chunk_web_content` | 웹 콘텐츠 청킹 |

## RAG 도구

| 도구 | 설명 |
|------|------|
| `search_knowledge_base` | 지식 베이스 검색 |
| `memorize_document` | 문서 저장 |
| `forget_document` | 문서 삭제 |

## 등록

```csharp
// Agent 도구
services.AddFluxAgentTools(options =>
{
    options.EnableFileFluxTools = true;
    options.EnableWebFluxTools = true;
    options.DefaultChunkingStrategy = "semantic";
    options.DefaultMaxChunkSize = 1000;
});

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
