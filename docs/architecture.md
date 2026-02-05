# 아키텍처

## 패키지 구조

```
IronHive.Flux/
├── IronHive.Flux.Core      # 핵심 어댑터
├── IronHive.Flux.Agent     # 에이전트 도구
├── IronHive.Flux.Rag       # RAG 도구
└── IronHive.Flux           # 메타패키지
```

## 의존성

```
IronHive.Flux
├── IronHive.Flux.Core
│   ├── IronHive.Abstractions
│   ├── FileFlux
│   ├── WebFlux
│   └── FluxIndex.Core
├── IronHive.Flux.Agent
│   ├── IronHive.Flux.Core
│   ├── FileFlux
│   └── WebFlux
└── IronHive.Flux.Rag
    ├── IronHive.Flux.Core
    └── FluxIndex.SDK
```

## 어댑터 패턴

IronHive 인터페이스 → Flux 인터페이스 변환:

```
IEmbeddingGenerator ──┬── FileFlux.IEmbeddingService
                      ├── WebFlux.ITextEmbeddingService
                      └── FluxIndex.IEmbeddingService

IMessageGenerator ────┬── FileFlux.ITextCompletionService
                      ├── WebFlux.ITextCompletionService
                      ├── FluxIndex.ITextCompletionService
                      ├── FileFlux.IImageToTextService
                      └── WebFlux.IImageToTextService
```

## 데이터 흐름

### Agent Worker
```
User Request → IronHive Agent
             → FunctionTool (FileFlux/WebFlux)
             → 문서 처리 / 웹 크롤링
             → Response
```

### RAG Chatbot
```
User Query → IronHive Agent
           → FluxIndexSearchTool → 벡터 검색
           → RagContextBuilder → 컨텍스트 구성
           → LLM 응답 생성
```
