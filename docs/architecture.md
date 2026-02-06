# IronHive.Flux Architecture

**Version:** 0.1.0
**Target Framework:** .NET 10
**License:** MIT

---

## 1. Overview

IronHive.Flux는 **IronHive**(AI/LLM 프레임워크)와 **Flux 생태계**(FileFlux, WebFlux, FluxIndex)를 연결하는 브릿지 SDK입니다. Adapter 패턴을 기반으로 IronHive의 AI 인터페이스를 Flux 생태계의 파일 처리, 웹 크롤링, 벡터 인덱싱 서비스에 적용하여, Agent Worker와 RAG Chatbot 시나리오를 지원합니다.

> **Note:** DeepResearch 모듈은 `ironhive-agent` 리포지토리의 `IronHive.DeepResearch`로 이동되었습니다.

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                         │
│  (Agent Worker / RAG Chatbot / Deep Research)               │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                   IronHive.Flux (Bridge)                     │
│  ┌──────────┐ ┌──────────┐ ┌────────┐ ┌──────────────────┐ │
│  │   Core   │ │  Agent   │ │  Rag   │                       │
│  │ Adapters │ │  Tools   │ │ Tools  │                       │
│  └────┬─────┘ └────┬─────┘ └───┬────┘                       │
└───────┼────────────┼───────────┼────────────────────────────┘
        │            │           │
┌───────▼────────────▼───────────▼────────────────────────────┐
│                  External Dependencies                       │
│  ┌──────────┐ ┌─────────┐ ┌──────────┐ ┌─────────────────┐ │
│  │ IronHive │ │ FileFlux│ │  WebFlux │ │   FluxIndex     │ │
│  │ (LLM)   │ │ (Files) │ │  (Web)   │ │   (Vector DB)   │ │
│  └──────────┘ └─────────┘ └──────────┘ └─────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Package Structure

### 2.1 Dependency Graph

```
IronHive.Flux (metapackage)
├── IronHive.Flux.Core
│   ├── IronHive.Abstractions (0.3.0)
│   ├── FileFlux (0.8.0)
│   ├── WebFlux (0.2.1)
│   └── FluxIndex.Core (0.4.0)
│
├── IronHive.Flux.Agent
│   ├── IronHive.Flux.Core
│   ├── FileFlux (0.8.0)
│   └── WebFlux (0.2.1)
│
├── IronHive.Flux.Rag
│   ├── IronHive.Flux.Core
│   └── FluxIndex.SDK (0.4.0)
│
```

> DeepResearch는 `ironhive-agent`의 `IronHive.DeepResearch`로 이동됨.

### 2.2 Package Responsibilities

| Package | Description | NuGet |
|---------|-------------|-------|
| `IronHive.Flux.Core` | IronHive <-> Flux 어댑터 (Embedding, TextCompletion, ImageToText) | Yes |
| `IronHive.Flux.Agent` | 에이전트 도구 (문서 처리, 웹 크롤링, 분석) | Yes |
| `IronHive.Flux.Rag` | RAG 도구 (벡터 검색, 메모라이즈, 컨텍스트 빌더) | Yes |
| `IronHive.Flux` | 위 3개를 포함하는 메타패키지 | Yes |

> `IronHive.Flux.DeepResearch`는 `ironhive-agent`의 `IronHive.DeepResearch`로 이동됨.

---

## 3. Repository Layout

```
ironhive-flux/
├── src/
│   ├── IronHive.Flux/                         # Metapackage (references only)
│   ├── IronHive.Flux.Core/
│   │   ├── Adapters/
│   │   │   ├── Embedding/                     # 3 adapters (FileFlux, WebFlux, FluxIndex)
│   │   │   ├── TextCompletion/                # 3 adapters
│   │   │   └── ImageToText/                   # 2 adapters
│   │   ├── Options/
│   │   └── Extensions/
│   ├── IronHive.Flux.Agent/
│   │   ├── Tools/
│   │   │   ├── FileFlux/                      # DocumentProcessor, Analyzer, MetadataExtractor
│   │   │   └── WebFlux/                       # WebCrawler, ContentAnalyzer, Chunker
│   │   ├── Options/
│   │   └── Extensions/
│   ├── IronHive.Flux.Rag/
│   │   ├── Tools/                             # Search, Memorize, Unmemorise
│   │   ├── Context/                           # RagContext, RagContextBuilder
│   │   ├── Options/
│   │   └── Extensions/
├── samples/
│   ├── AgentWorkerSample/                     # Agent Worker scenario demo
│   ├── RagChatbotSample/                      # RAG Chatbot scenario demo
│   └── DeepResearchSample/                    # Deep Research scenario demo
├── tests/
│   └── IronHive.Flux.Tests/                   # xUnit tests
├── docs/
│   ├── architecture.md                        # This file
│   ├── adapters.md                            # Adapter reference
│   ├── tools.md                               # Tool reference
│   └── design/
│       └── deep-research-architecture.md      # DeepResearch detailed design
├── Directory.Build.props                      # Common build settings (.NET 10)
├── Directory.Packages.props                   # Central package management
└── IronHive.Flux.slnx                         # Solution file
```

---

## 4. Core Adapter Pattern

### 4.1 Design Principle

IronHive.Flux.Core는 **Adapter 패턴**을 사용하여 IronHive의 AI 인터페이스를 Flux 생태계의 서비스 인터페이스로 변환합니다. 이를 통해 Flux 생태계 라이브러리가 IronHive를 직접 참조하지 않고도 LLM 기능을 활용할 수 있습니다.

### 4.2 Adapter Mapping

```
IronHive Interface              Flux Interface
──────────────────────          ──────────────────────────────────
IEmbeddingGenerator    ──┬───>  FileFlux.IEmbeddingService
                         ├───>  WebFlux.ITextEmbeddingService
                         └───>  FluxIndex.IEmbeddingService

IMessageGenerator      ──┬───>  FileFlux.ITextCompletionService
                         ├───>  WebFlux.ITextCompletionService
                         ├───>  FluxIndex.ITextCompletionService
                         ├───>  FileFlux.IImageToTextService
                         └───>  WebFlux.IImageToTextService
```

### 4.3 Adapter Classes

**Embedding Adapters** (3):

| Adapter | Source | Target | Key Methods |
|---------|--------|--------|-------------|
| `IronHiveEmbeddingServiceForFileFlux` | `IEmbeddingGenerator` | `FileFlux.IEmbeddingService` | `GenerateEmbeddingAsync`, `GenerateBatchEmbeddingsAsync` |
| `IronHiveEmbeddingServiceForWebFlux` | `IEmbeddingGenerator` | `WebFlux.ITextEmbeddingService` | `GetEmbeddingAsync`, `GetBatchEmbeddingsAsync` |
| `IronHiveEmbeddingServiceForFluxIndex` | `IEmbeddingGenerator` | `FluxIndex.IEmbeddingService` | `GenerateEmbeddingAsync`, `GetModelName` |

**TextCompletion Adapters** (3):

| Adapter | Source | Target | Key Methods |
|---------|--------|--------|-------------|
| `IronHiveTextCompletionServiceForFileFlux` | `IMessageGenerator` | `FileFlux.ITextCompletionService` | `AnalyzeStructureAsync`, `SummarizeContentAsync`, `ExtractMetadataAsync` |
| `IronHiveTextCompletionServiceForWebFlux` | `IMessageGenerator` | `WebFlux.ITextCompletionService` | `CompleteAsync`, `CompleteStreamAsync` |
| `IronHiveTextCompletionServiceForFluxIndex` | `IMessageGenerator` | `FluxIndex.ITextCompletionService` | `GenerateCompletionAsync`, `GenerateJsonCompletionAsync` |

**ImageToText Adapters** (2):

| Adapter | Source | Target | Key Methods |
|---------|--------|--------|-------------|
| `IronHiveImageToTextServiceForFileFlux` | `IMessageGenerator` | `FileFlux.IImageToTextService` | `ExtractTextAsync` (byte[], Stream, path) |
| `IronHiveImageToTextServiceForWebFlux` | `IMessageGenerator` | `WebFlux.IImageToTextService` | `ConvertImageToTextAsync`, `ExtractTextFromWebImageAsync` |

### 4.4 Configuration

```csharp
public class IronHiveFluxCoreOptions
{
    public string EmbeddingModelId { get; set; } = "text-embedding-3-small";
    public string TextCompletionModelId { get; set; } = "gpt-4o";
    public string ImageToTextModelId { get; set; } = "gpt-4o";
    public int EmbeddingDimension { get; set; } = 0;
    public int MaxTokens { get; set; } = 8191;
    public float DefaultTemperature { get; set; } = 0.7f;
    public int DefaultCompletionMaxTokens { get; set; } = 500;
}
```

### 4.5 DI Registration

```csharp
// All adapters
services.AddIronHiveFluxCore(options => { /* ... */ });
services.AddAllIronHiveFluxAdapters();

// Or selective registration
services.AddIronHiveFileFluxAdapters();
services.AddIronHiveWebFluxAdapters();
services.AddIronHiveFluxIndexAdapters();
```

---

## 5. Agent Tools

### 5.1 Overview

IronHive.Flux.Agent는 IronHive/IronBees 에이전트가 사용할 수 있는 **Function Tools**를 제공합니다. FileFlux와 WebFlux의 기능을 에이전트 도구로 래핑하여, LLM이 도구 호출을 통해 문서 처리 및 웹 크롤링을 수행할 수 있습니다.

### 5.2 Available Tools

**FileFlux Tools:**

| Tool | Function Name | Description |
|------|--------------|-------------|
| `DocumentProcessorTool` | `process_document` | 문서를 RAG용 청크로 분할 (PDF, DOCX, TXT, MD) |
| `DocumentAnalyzerTool` | `analyze_document_structure` | 문서 구조 분석 (섹션, 제목, 계층) |
| `MetadataExtractorTool` | `extract_document_metadata` | 메타데이터 추출 (키워드, 언어, 카테고리) |

**WebFlux Tools:**

| Tool | Function Name | Description |
|------|--------------|-------------|
| `WebCrawlerTool` | `crawl_web_page` | 웹 페이지 크롤링 및 콘텐츠 추출 |
| `WebContentAnalyzerTool` | `analyze_web_content` | 웹 콘텐츠 분석 |
| `WebChunkerTool` | `chunk_web_content` | 웹 콘텐츠 청킹 |

### 5.3 Configuration

```csharp
public class FluxAgentToolsOptions
{
    public bool EnableFileFluxTools { get; set; } = true;
    public bool EnableWebFluxTools { get; set; } = true;
    public string DefaultChunkingStrategy { get; set; } = "semantic";  // semantic | fixed | sentence
    public int DefaultMaxChunkSize { get; set; } = 1000;
    public int DefaultChunkOverlap { get; set; } = 100;
    public int DefaultMaxCrawlDepth { get; set; } = 2;
    public bool DefaultExtractImages { get; set; } = false;
    public int ToolTimeout { get; set; } = 120;
}
```

---

## 6. RAG Pipeline

### 6.1 Overview

IronHive.Flux.Rag는 FluxIndex 기반의 RAG(Retrieval-Augmented Generation) 파이프라인을 제공합니다. 벡터 검색, 하이브리드 검색, 키워드 검색을 지원하며, `RagContextBuilder`를 통해 검색 결과를 LLM에 전달할 컨텍스트로 구성합니다.

### 6.2 Data Flow

```
User Query
    |
    v
┌──────────────────┐
│ FluxIndexSearch   │  <- vector / hybrid / keyword search
│ Tool             │
└────────┬─────────┘
         | SearchResults
         v
┌──────────────────┐
│ RagContextBuilder│  <- score filtering, token budget
└────────┬─────────┘
         | RagContext { ContextText, Sources, TokenCount }
         v
┌──────────────────┐
│ IMessageGenerator│  <- LLM with context-augmented prompt
└────────┬─────────┘
         |
         v
    Response with Citations
```

### 6.3 RAG Tools

| Tool | Function Name | Description |
|------|--------------|-------------|
| `FluxIndexSearchTool` | `search_knowledge_base` | 벡터/하이브리드/키워드 검색 |
| `FluxIndexMemorizeTool` | `memorize_document` | 문서를 인덱스에 저장 |
| `FluxIndexUnmemorizeTool` | `forget_document` | 문서를 인덱스에서 삭제 |

### 6.4 RagContext Model

```csharp
public record RagContext
{
    public required string ContextText { get; init; }     // LLM에 전달할 컨텍스트
    public IReadOnlyList<RagSearchResult> Sources { get; init; } = [];
    public int TokenCount { get; init; }
    public float AverageRelevance { get; init; }
    public string SearchStrategy { get; init; } = "hybrid";
}
```

### 6.5 Configuration

```csharp
public class FluxRagToolsOptions
{
    public int DefaultMaxResults { get; set; } = 5;
    public string DefaultSearchStrategy { get; set; } = "hybrid";
    public float DefaultMinScore { get; set; } = 0.5f;
    public int MaxContextTokens { get; set; } = 4000;
    public string ChunkSeparator { get; set; } = "\n\n---\n\n";
    public string DefaultIndexName { get; set; } = "default";
    public int ToolTimeout { get; set; } = 60;
}
```

---

## 7. DeepResearch (Moved)

> **DeepResearch 모듈은 `ironhive-agent` 리포지토리의 `IronHive.DeepResearch`로 이동되었습니다.**
>
> - 에이전트 생태계와의 더 긴밀한 통합
> - IChatClient / IMessageGenerator 어댑터 내장
> - 상세 설계: [docs/design/deep-research-architecture.md](design/deep-research-architecture.md) (역사적 참고용)

---

## 8. Three Scenarios

### 8.1 Agent Worker

```
IronHive + IronBees + FileFlux + WebFlux
-> Coding agents, general-purpose agents
```

에이전트가 도구 호출을 통해 문서 처리 및 웹 크롤링을 수행합니다.

```csharp
services.AddIronHiveFluxCore(options => { /* LLM config */ });
services.AddFluxAgentTools(options =>
{
    options.EnableFileFluxTools = true;
    options.EnableWebFluxTools = true;
});

var tools = provider.GetAllFluxAgentTools();
// tools: process_document, analyze_document_structure, extract_document_metadata,
//        crawl_web_page, analyze_web_content, chunk_web_content
```

### 8.2 RAG Chatbot

```
IronHive + IronBees + FluxIndex
-> Knowledge-based chatbot services
```

벡터 인덱스 기반 지식 검색 + LLM 응답 생성 파이프라인입니다.

```csharp
services.AddIronHiveFluxCore(options => { /* LLM config */ });
services.AddFluxRagTools(options =>
{
    options.DefaultMaxResults = 5;
    options.DefaultSearchStrategy = "hybrid";
    options.DefaultMinScore = 0.5f;
    options.MaxContextTokens = 4000;
});

var tools = provider.GetFluxRagTools();
// tools: search_knowledge_base, memorize_document, forget_document
```

### 8.3 Deep Research (Moved to ironhive-agent)

> Deep Research 시나리오는 `ironhive-agent` 리포지토리의 `IronHive.DeepResearch` 패키지를 사용합니다.
> 샘플 코드: `samples/DeepResearchSample/` (로컬 ProjectReference로 연결)

---

## 9. Layer Alignment

기능 구현 시 올바른 레이어에 배치해야 합니다.

| Layer | Repository | Responsibility |
|-------|-----------|----------------|
| **Core AI** | ironhive | LLM 추상화, 메시지/임베딩 생성, 도구 실행 |
| **File Processing** | FileFlux | 문서 파싱, 청킹, 메타데이터 추출 |
| **Web Processing** | WebFlux | 웹 크롤링, 콘텐츠 추출, HTML to Markdown |
| **Vector Index** | FluxIndex | 임베딩 저장, 유사도 검색, 인덱스 관리 |
| **Bridge/Integration** | IronHive.Flux.Core | 어댑터, 서비스 등록 |
| **Agent Tools** | IronHive.Flux.Agent | FileFlux/WebFlux 도구 래핑 |
| **RAG Tools** | IronHive.Flux.Rag | FluxIndex 도구 래핑, 컨텍스트 빌더 |
| **Autonomous Research** | ironhive-agent/IronHive.DeepResearch | 자율 리서치 루프, 검색 조율, 보고서 생성 (이동됨) |

**Dependency Direction:**

```
Agent ---------> Core --> FileFlux, WebFlux
Rag -----------> Core --> FluxIndex
```

상위 레이어에서 하위 레이어 방향으로만 의존합니다. 역방향 의존은 금지입니다.

**Anti-patterns:**
- IronHive.Flux에서 직접 PDF 파싱 -> FileFlux의 책임
- WebFlux에서 LLM 호출 -> ironhive의 책임
- FluxIndex에서 웹 크롤링 -> WebFlux의 책임

---

## 10. Design Patterns

| Pattern | Where | Purpose |
|---------|-------|---------|
| **Adapter** | `Core/Adapters/` | IronHive <-> Flux 인터페이스 변환 |
| **Factory** | Various | 동적 인스턴스 생성 |
| **Strategy** | Chunking strategies | semantic / fixed / sentence 청킹 |
| **Orchestrator** | (moved to ironhive-agent) | 파이프라인 단계 조율 |
| **Options** | `*Options.cs` | Microsoft.Extensions.Options 기반 설정 |
| **Async Streaming** | `IAsyncEnumerable<T>` | 실시간 진행 상황 전달 |

---

## 11. Build Configuration

### 11.1 Common Settings (Directory.Build.props)

- **Target Framework:** .NET 10
- **Language:** C# latest
- **Nullable:** enabled
- **Implicit Usings:** enabled
- **Version:** 0.1.0
- `src/` 하위 프로젝트: `IsPackable=true` (NuGet 배포)
- `tests/`, `samples/`: `IsPackable=false`

### 11.2 Central Package Management (Directory.Packages.props)

| Category | Packages | Version |
|----------|----------|---------|
| IronHive | Abstractions, Core, Providers.OpenAI, Ironbees.Ironhive | 0.3.0 |
| Flux | FileFlux, FileFlux.Core | 0.8.0 |
| Flux | WebFlux | 0.2.1 |
| Flux | FluxIndex.Core, FluxIndex.SDK | 0.4.0 |
| Microsoft | Extensions.DI, Logging, Options, Http, Caching.Memory | 10.0.2 |
| Resilience | Polly, Polly.Extensions | 8.6.5 |
| Testing | xUnit, FluentAssertions, Moq, coverlet | various |

### 11.3 Build Commands

```bash
dotnet build IronHive.Flux.slnx      # Build all
dotnet test                            # Run tests
dotnet test --filter "FullyQualifiedName~TestMethodName"  # Run single test
```

---

## 12. Commit History

| Commit | Description |
|--------|-------------|
| `5b0939d` | Initial commit |
| `0f63401` | Add IronHive.Flux project structure with core libraries and samples |
| `e409b1b` | Implement Deep Research Models and Options |
| `ee73dfa` | Complete DeepResearch orchestration pipeline with WebFlux integration |
| `fa80344` | Restructure ResearchResult for consuming apps and add search retry mechanism |
| `713b4e5` | Improve DuckDuckGo bot protection handling with retry and sequential execution |
| `ed68aba` | Remove DuckDuckGo search provider (bot protection makes it unusable) |

**Key Decisions:**
- DuckDuckGo 검색 프로바이더는 봇 방어로 인해 제거됨 (`ed68aba`)
- `ResearchResult`가 소비 앱 친화적으로 재구조화됨 - `CitedSources`/`UncitedSources` 분리, `ThinkingProcess` 추가 (`fa80344`)
- 검색 재시도 메커니즘 추가로 안정성 향상 (`fa80344`)
