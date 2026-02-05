using System.Text.Json;
using DeepResearchSample;
using IronHive.Abstractions.Messages;
using IronHive.Flux.DeepResearch;
using IronHive.Flux.DeepResearch.Abstractions;
using IronHive.Flux.DeepResearch.Extensions;
using IronHive.Flux.DeepResearch.Models.Research;
using IronHive.Providers.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== DeepResearch Local Test ===\n");

// .env 파일에서 환경 변수 로드 (있으면)
LoadEnvFile(".env");

// 환경 변수에서 설정 읽기 (GPUStack 또는 OpenAI)
var gpuStackEndpoint = Environment.GetEnvironmentVariable("GPUSTACK_ENDPOINT");
var gpuStackKey = Environment.GetEnvironmentVariable("GPUSTACK_API_KEY");
var gpuStackModel = Environment.GetEnvironmentVariable("GPUSTACK_MODEL");
var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var tavilyKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
var useWebFlux = Environment.GetEnvironmentVariable("USE_WEBFLUX_PACKAGE")?.ToLower() == "true";
var outputDir = Environment.GetEnvironmentVariable("OUTPUT_DIR") ?? "./reports";

// LLM 설정 결정
OpenAIConfig? llmConfig = null;
string modelId;

if (!string.IsNullOrEmpty(gpuStackEndpoint) && !string.IsNullOrEmpty(gpuStackKey))
{
    // GPUStack 사용
    Console.WriteLine($"[CONFIG] GPUStack 사용: {gpuStackEndpoint}");
    llmConfig = new OpenAIConfig
    {
        BaseUrl = gpuStackEndpoint.TrimEnd('/') + "/v1-openai/",
        ApiKey = gpuStackKey
    };
    modelId = gpuStackModel ?? "gpt-oss-20b";
    Console.WriteLine($"[CONFIG] 모델: {modelId}");
}
else if (!string.IsNullOrEmpty(openAiKey))
{
    // OpenAI 사용
    Console.WriteLine("[CONFIG] OpenAI 사용");
    llmConfig = new OpenAIConfig
    {
        ApiKey = openAiKey
    };
    modelId = "gpt-4o-mini";
    Console.WriteLine($"[CONFIG] 모델: {modelId}");
}
else
{
    Console.WriteLine("[ERROR] LLM 설정이 없습니다.");
    Console.WriteLine("다음 중 하나를 설정하세요:");
    Console.WriteLine("\n  [GPUStack]");
    Console.WriteLine("  set GPUSTACK_ENDPOINT=http://...");
    Console.WriteLine("  set GPUSTACK_API_KEY=...");
    Console.WriteLine("  set GPUSTACK_MODEL=... (선택)");
    Console.WriteLine("\n  [OpenAI]");
    Console.WriteLine("  set OPENAI_API_KEY=sk-...");
    Console.WriteLine("\n  [선택사항]");
    Console.WriteLine("  set TAVILY_API_KEY=... (검색 API)");
    Console.WriteLine("  set USE_WEBFLUX_PACKAGE=true (고급 콘텐츠 추출)");
    return;
}

// Tavily 키 확인은 서비스 등록에서 처리

// DI 컨테이너 구성
var services = new ServiceCollection();

// 로깅 설정
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// DeepResearch ITextGenerationService 어댑터 등록
services.AddSingleton<ITextGenerationService>(sp =>
{
    var generator = new OpenAIChatMessageGenerator(llmConfig);
    return new IronHiveTextGenerationAdapter(generator, modelId);
});

// Tavily API 키 확인
if (string.IsNullOrEmpty(tavilyKey))
{
    Console.WriteLine("[ERROR] TAVILY_API_KEY가 설정되지 않았습니다.");
    Console.WriteLine("Tavily API 키를 발급받아 설정하세요: https://tavily.com");
    Console.WriteLine("  set TAVILY_API_KEY=tvly-xxxxxxxx");
    return;
}

// DeepResearch 서비스 등록
services.AddIronHiveFluxDeepResearch(options =>
{
    options.DefaultSearchProvider = "tavily";
    options.SearchApiKeys["tavily"] = tavilyKey;
    Console.WriteLine("[CONFIG] 검색: Tavily");

    // WebFlux 패키지 사용 여부 (고급 콘텐츠 추출)
    options.UseWebFluxPackage = useWebFlux;
    if (useWebFlux)
    {
        Console.WriteLine("[CONFIG] 콘텐츠 추출: WebFlux 패키지 (고급)");
    }
    else
    {
        Console.WriteLine("[CONFIG] 콘텐츠 추출: 기본 (경량)");
    }

    options.DefaultMaxIterations = 2; // 테스트용으로 낮게 설정
    options.SufficiencyThreshold = 0.7m;
    options.HttpTimeout = TimeSpan.FromSeconds(60);
});

var serviceProvider = services.BuildServiceProvider();

Console.WriteLine("[INFO] 서비스 초기화 완료\n");

// 리서치 실행
var researcher = serviceProvider.GetRequiredService<IDeepResearcher>();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

var testQuery = "2026년 AI 에이전트 프레임워크의 주요 트렌드와 발전 방향은 무엇인가?";

Console.WriteLine($"[QUERY] {testQuery}\n");
Console.WriteLine("[INFO] 스트리밍 리서치 시작...\n");

try
{
    var request = new ResearchRequest
    {
        Query = testQuery,
        Depth = ResearchDepth.Quick,
        MaxIterations = 2,
        Language = "ko",
        OutputFormat = OutputFormat.Markdown
    };

    // 스트리밍 리서치 실행
    await foreach (var progress in researcher.ResearchStreamAsync(request))
    {
        switch (progress.Type)
        {
            case ProgressType.Started:
                Console.WriteLine($"[PROGRESS] 리서치 시작 - 최대 반복: {progress.MaxIterations}회");
                break;

            case ProgressType.PlanGenerated:
                Console.WriteLine($"[PROGRESS] 계획 생성 완료 - 쿼리 {progress.Plan?.GeneratedQueries?.Count ?? 0}개");
                break;

            case ProgressType.SearchStarted:
                Console.WriteLine($"[PROGRESS] 검색 시작 (반복 {progress.CurrentIteration})");
                break;

            case ProgressType.SearchCompleted:
                Console.WriteLine($"[PROGRESS] 검색 완료 - {progress.Search?.Provider}: {progress.Search?.ResultCount}개 결과");
                break;

            case ProgressType.ContentExtractionStarted:
                Console.WriteLine($"[PROGRESS] 콘텐츠 추출 시작");
                break;

            case ProgressType.AnalysisStarted:
                Console.WriteLine($"[PROGRESS] 분석 시작");
                break;

            case ProgressType.AnalysisCompleted:
                Console.WriteLine($"[PROGRESS] 분석 완료 - Finding {progress.Analysis?.FindingsCount ?? 0}개, 충분성 점수: {progress.Analysis?.Score?.OverallScore:P0}");
                break;

            case ProgressType.IterationCompleted:
                Console.WriteLine($"[PROGRESS] 반복 {progress.CurrentIteration} 완료");
                break;

            case ProgressType.ReportGenerationStarted:
                Console.WriteLine($"[PROGRESS] 보고서 생성 시작");
                break;

            case ProgressType.ReportSection:
                Console.WriteLine($"[PROGRESS] 섹션 생성됨");
                break;

            case ProgressType.Completed:
                Console.WriteLine($"\n[COMPLETED] 리서치 완료!");
                Console.WriteLine($"  - 세션 ID: {progress.Result?.SessionId}");
                Console.WriteLine($"  - 소스 수: {progress.Result?.Sources?.Count ?? 0}");
                Console.WriteLine($"  - 반복 횟수: {progress.Result?.Metadata?.IterationCount}");
                Console.WriteLine($"  - 소요 시간: {progress.Result?.Metadata?.Duration.TotalSeconds:F1}초");

                // 보고서 파일 저장
                if (progress.Result != null)
                {
                    var savedPath = await SaveReportAsync(progress.Result, outputDir);
                    if (savedPath != null)
                    {
                        Console.WriteLine($"  - 저장 위치: {savedPath}");
                    }

                    // 사고 과정 출력
                    if (progress.Result.ThinkingProcess.Count > 0)
                    {
                        Console.WriteLine($"  - 사고 과정: {progress.Result.ThinkingProcess.Count}개 단계");
                    }
                }

                Console.WriteLine("\n=== 보고서 ===\n");
                Console.WriteLine(progress.Result?.Report ?? "(보고서 없음)");
                break;

            case ProgressType.Failed:
                Console.WriteLine($"[ERROR] 실패: {progress.Error?.Message}");
                break;
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] 예외 발생: {ex.Message}");
    logger.LogError(ex, "리서치 실행 중 오류");
}

Console.WriteLine("\n=== 테스트 완료 ===");

// .env 파일 로드 헬퍼
static void LoadEnvFile(string path)
{
    if (!File.Exists(path)) return;

    foreach (var line in File.ReadAllLines(path))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

        var idx = trimmed.IndexOf('=');
        if (idx <= 0) continue;

        var key = trimmed[..idx].Trim();
        var value = trimmed[(idx + 1)..].Trim();

        // 기존 환경 변수가 없을 때만 설정
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

// 보고서 파일 저장 헬퍼
static async Task<string?> SaveReportAsync(ResearchResult result, string outputDir)
{
    try
    {
        // 출력 디렉토리 생성
        Directory.CreateDirectory(outputDir);

        // 파일명 생성 (타임스탬프 + 세션ID 앞 8자리)
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var sessionPrefix = result.SessionId[..Math.Min(8, result.SessionId.Length)];
        var baseFileName = $"report_{timestamp}_{sessionPrefix}";

        // 마크다운 보고서 저장 (본문만)
        var mdPath = Path.Combine(outputDir, $"{baseFileName}.md");
        await File.WriteAllTextAsync(mdPath, result.Report);

        // 전체 결과를 JSON으로 저장 (소비 앱이 활용)
        var fullResult = new
        {
            result.SessionId,
            result.Query,
            GeneratedAt = DateTime.Now,
            Duration = result.Metadata.Duration.TotalSeconds,
            IterationCount = result.Metadata.IterationCount,

            // 보고서에 사용된 소스
            CitedSources = result.CitedSources.Select(s => new
            {
                s.Id, s.Url, s.Title, s.Author, s.PublishedDate, s.Provider
            }).ToList(),

            // 읽었지만 사용되지 않은 소스
            UncitedSources = result.UncitedSources.Select(s => new
            {
                s.Id, s.Url, s.Title, s.Author, s.PublishedDate, s.Provider
            }).ToList(),

            // 인용 정보
            result.Citations,

            // 사고 과정
            ThinkingProcess = result.ThinkingProcess.Select(t => new
            {
                t.Type, t.Title, t.Description, t.Timestamp, t.Duration
            }).ToList(),

            // 에러
            result.Errors
        };

        var jsonPath = Path.Combine(outputDir, $"{baseFileName}.json");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(fullResult, jsonOptions));

        Console.WriteLine($"[FILE] 보고서 저장됨: {mdPath}");
        Console.WriteLine($"[FILE] 메타데이터 저장됨: {jsonPath}");

        return mdPath;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] 파일 저장 실패: {ex.Message}");
        return null;
    }
}
