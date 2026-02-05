using IronHive.Core.Tools;
using IronHive.Flux.Agent.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Agent.Tools.WebFlux;

/// <summary>
/// 웹 콘텐츠 청킹 도구 - WebFlux를 사용하여 웹 콘텐츠 청킹
/// </summary>
public class WebChunkerTool
{
    private readonly FluxAgentToolsOptions _options;
    private readonly ILogger<WebChunkerTool>? _logger;

    public WebChunkerTool(
        IOptions<FluxAgentToolsOptions> options,
        ILogger<WebChunkerTool>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// 웹 콘텐츠를 청크로 분할합니다.
    /// </summary>
    /// <param name="content">청킹할 웹 콘텐츠</param>
    /// <param name="strategy">청킹 전략 (paragraph, semantic, fixed)</param>
    /// <param name="maxChunkSize">최대 청크 크기 (문자 수)</param>
    /// <returns>청킹 결과 (JSON 문자열)</returns>
    [FunctionTool("chunk_web_content")]
    [Description("웹 콘텐츠를 RAG용 청크로 분할합니다. 문단, 의미 단위, 고정 크기 등의 전략을 지원합니다.")]
    public Task<string> ChunkAsync(
        [Description("청킹할 웹 콘텐츠")] string content,
        [Description("청킹 전략 (paragraph, semantic, fixed). 기본값: paragraph")] string? strategy = null,
        [Description("최대 청크 크기 (문자 수). 기본값: 1000")] int? maxChunkSize = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("웹 콘텐츠 청킹 시작 - ContentLength: {Length}", content.Length);

        try
        {
            var chunkStrategy = strategy ?? "paragraph";
            var maxSize = maxChunkSize ?? _options.DefaultMaxChunkSize;

            var chunks = chunkStrategy.ToLower() switch
            {
                "paragraph" => ChunkByParagraph(content, maxSize),
                "semantic" => ChunkBySemantic(content, maxSize),
                "fixed" => ChunkByFixed(content, maxSize),
                _ => ChunkByParagraph(content, maxSize)
            };

            var result = new
            {
                success = true,
                strategy = chunkStrategy,
                maxChunkSize = maxSize,
                originalLength = content.Length,
                chunkCount = chunks.Count,
                averageChunkSize = chunks.Count > 0 ? chunks.Average(c => c.Length) : 0,
                chunks = chunks.Select((c, i) => new
                {
                    index = i,
                    length = c.Length,
                    content = c.Length > 200 ? c[..200] + "..." : c
                })
            };

            _logger?.LogInformation("웹 콘텐츠 청킹 완료 - ChunkCount: {Count}", chunks.Count);
            return Task.FromResult(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "웹 콘텐츠 청킹 실패");
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }));
        }
    }

    private static List<string> ChunkByParagraph(string content, int maxSize)
    {
        var chunks = new List<string>();
        var paragraphs = content.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = "";
        foreach (var para in paragraphs)
        {
            var trimmedPara = para.Trim();
            if (string.IsNullOrEmpty(trimmedPara)) continue;

            if (currentChunk.Length + trimmedPara.Length + 2 > maxSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.Trim());
                currentChunk = trimmedPara;
            }
            else
            {
                currentChunk += (currentChunk.Length > 0 ? "\n\n" : "") + trimmedPara;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
            chunks.Add(currentChunk.Trim());

        // 너무 긴 청크는 추가로 분할
        var finalChunks = new List<string>();
        foreach (var chunk in chunks)
        {
            if (chunk.Length > maxSize)
            {
                finalChunks.AddRange(ChunkByFixed(chunk, maxSize));
            }
            else
            {
                finalChunks.Add(chunk);
            }
        }

        return finalChunks;
    }

    private static List<string> ChunkBySemantic(string content, int maxSize)
    {
        // 의미 단위 청킹 (헤딩, 리스트 기반)
        var chunks = new List<string>();
        var lines = content.Split('\n');

        var currentChunk = "";
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // 새로운 섹션 시작 감지 (헤딩, 숫자 리스트 등)
            var isNewSection = trimmedLine.StartsWith('#') ||
                              (trimmedLine.Length > 0 && char.IsDigit(trimmedLine[0]) && trimmedLine.Contains('.')) ||
                              trimmedLine.StartsWith("- ") ||
                              trimmedLine.StartsWith("* ");

            if (isNewSection && currentChunk.Length > maxSize / 2)
            {
                chunks.Add(currentChunk.Trim());
                currentChunk = trimmedLine;
            }
            else if (currentChunk.Length + trimmedLine.Length + 1 > maxSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.Trim());
                currentChunk = trimmedLine;
            }
            else
            {
                currentChunk += (currentChunk.Length > 0 ? "\n" : "") + trimmedLine;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
            chunks.Add(currentChunk.Trim());

        return chunks;
    }

    private static List<string> ChunkByFixed(string content, int maxSize)
    {
        var chunks = new List<string>();
        var overlap = maxSize / 10; // 10% 오버랩

        var position = 0;
        while (position < content.Length)
        {
            var end = Math.Min(position + maxSize, content.Length);

            // 단어 경계에서 자르기
            if (end < content.Length)
            {
                var lastSpace = content.LastIndexOf(' ', end, Math.Min(100, end - position));
                if (lastSpace > position)
                    end = lastSpace;
            }

            chunks.Add(content[position..end].Trim());
            position = end - overlap;
            if (position < 0) position = 0;
            if (position >= content.Length - overlap) break;
        }

        return chunks;
    }
}
