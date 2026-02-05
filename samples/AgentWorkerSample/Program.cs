using IronHive.Flux.Agent.Extensions;
using IronHive.Flux.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== IronHive.Flux Agent Worker Sample ===\n");

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
    options.ImageToTextModelId = "gpt-4o";
});

// IronHive.Flux.Agent 도구 설정
services.AddFluxAgentTools(options =>
{
    options.EnableFileFluxTools = true;
    options.EnableWebFluxTools = true;
    options.DefaultChunkingStrategy = "semantic";
    options.DefaultMaxChunkSize = 1000;
});

var provider = services.BuildServiceProvider();

Console.WriteLine("서비스 구성 완료!\n");

// FileFlux 도구 테스트
Console.WriteLine("--- FileFlux 도구 테스트 ---\n");

var fileFluxTools = provider.GetFileFluxTools().ToList();
Console.WriteLine($"FileFlux 도구 수: {fileFluxTools.Count}");
foreach (var tool in fileFluxTools)
{
    Console.WriteLine($"  - {tool.UniqueName}: {tool.Description}");
}

// WebFlux 도구 테스트
Console.WriteLine("\n--- WebFlux 도구 테스트 ---\n");

var webFluxTools = provider.GetWebFluxTools().ToList();
Console.WriteLine($"WebFlux 도구 수: {webFluxTools.Count}");
foreach (var tool in webFluxTools)
{
    Console.WriteLine($"  - {tool.UniqueName}: {tool.Description}");
}

// 전체 도구 목록
Console.WriteLine("\n--- 전체 Agent 도구 ---\n");

var allTools = provider.GetAllFluxAgentTools().ToList();
Console.WriteLine($"전체 도구 수: {allTools.Count}");

Console.WriteLine("\n=== 샘플 완료 ===");
