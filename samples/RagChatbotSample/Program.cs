using IronHive.Flux.Core.Extensions;
using IronHive.Flux.Rag.Context;
using IronHive.Flux.Rag.Extensions;
using IronHive.Flux.Rag.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== IronHive.Flux RAG Chatbot Sample ===\n");

// 서비스 구성
var services = new ServiceCollection();

// 로깅 설정
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// IronHive.Flux.Core 설정
services.AddIronHiveFluxCore(options =>
{
    options.EmbeddingModelId = "text-embedding-3-small";
    options.TextCompletionModelId = "gpt-4o";
});

// IronHive.Flux.Rag 도구 설정
services.AddFluxRagTools(options =>
{
    options.DefaultMaxResults = 5;
    options.DefaultSearchStrategy = "hybrid";
    options.DefaultMinScore = 0.5f;
    options.MaxContextTokens = 4000;
});

var provider = services.BuildServiceProvider();

Console.WriteLine("서비스 구성 완료!\n");

// RAG 도구 테스트
Console.WriteLine("--- RAG 도구 테스트 ---\n");

var ragTools = provider.GetFluxRagTools().ToList();
Console.WriteLine($"RAG 도구 수: {ragTools.Count}");
foreach (var tool in ragTools)
{
    Console.WriteLine($"  - {tool.UniqueName}: {tool.Description}");
}

// RagContextBuilder 테스트
Console.WriteLine("\n--- RagContextBuilder 테스트 ---\n");

var contextBuilder = provider.GetRequiredService<RagContextBuilder>();

// 샘플 검색 결과 생성
var sampleResults = new List<RagSearchResult>
{
    new()
    {
        DocumentId = "doc-001",
        Content = "IronHive는 .NET 기반의 AI 에이전트 프레임워크입니다. 다양한 LLM 프로바이더를 지원하며, 확장 가능한 도구 시스템을 제공합니다.",
        Score = 0.95f,
        Title = "IronHive 소개"
    },
    new()
    {
        DocumentId = "doc-002",
        Content = "Flux 생태계는 FileFlux(문서 처리), WebFlux(웹 크롤링), FluxIndex(벡터 검색)로 구성됩니다. RAG 시스템 구축에 필요한 모든 기능을 제공합니다.",
        Score = 0.88f,
        Title = "Flux 생태계 개요"
    },
    new()
    {
        DocumentId = "doc-003",
        Content = "IronHive.Flux는 IronHive와 Flux 생태계를 연결하는 브릿지 SDK입니다. 어댑터 패턴을 통해 두 시스템을 원활하게 통합합니다.",
        Score = 0.82f,
        Title = "IronHive.Flux 브릿지"
    }
};

var context = contextBuilder.BuildContext(sampleResults, new RagContextOptions
{
    Query = "IronHive와 Flux의 관계는?",
    MaxResults = 5,
    MinScore = 0.5f
});

Console.WriteLine($"컨텍스트 토큰 수: {context.TokenCount}");
Console.WriteLine($"평균 관련성: {context.AverageRelevance:F2}");
Console.WriteLine($"소스 수: {context.Sources.Count}");

Console.WriteLine("\n--- 생성된 컨텍스트 ---\n");
Console.WriteLine(context.ContextText);

// Memorize/Search 시뮬레이션
Console.WriteLine("\n--- Memorize/Search 시뮬레이션 ---\n");

var memorizeTool = provider.GetRequiredService<FluxIndexMemorizeTool>();
var searchTool = provider.GetRequiredService<FluxIndexSearchTool>();

// 문서 저장 (파일 경로 기반)
Console.WriteLine("문서 저장 중...");
var sampleFile = Path.Combine(Path.GetTempPath(), "flux-sample.md");
await File.WriteAllTextAsync(sampleFile, "IronHive.Flux는 AI 에이전트와 RAG 시스템을 위한 통합 SDK입니다.");
var memorizeResult = await memorizeTool.MemorizeAsync(filePath: sampleFile);
Console.WriteLine($"저장 결과: {memorizeResult}\n");

// 검색
Console.WriteLine("검색 중...");
var searchResult = await searchTool.SearchAsync(
    query: "AI 에이전트",
    maxResults: 5
);
Console.WriteLine($"검색 결과: {searchResult}");

Console.WriteLine("\n=== 샘플 완료 ===");
