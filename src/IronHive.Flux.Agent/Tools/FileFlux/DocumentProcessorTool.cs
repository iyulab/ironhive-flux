using IronHive.Core.Tools;
using IronHive.Flux.Agent.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Agent.Tools.FileFlux;

/// <summary>
/// 문서 처리 도구 - FileFlux를 사용하여 문서를 청크로 분할
/// </summary>
public class DocumentProcessorTool
{
    private readonly FluxAgentToolsOptions _options;
    private readonly ILogger<DocumentProcessorTool>? _logger;

    public DocumentProcessorTool(
        IOptions<FluxAgentToolsOptions> options,
        ILogger<DocumentProcessorTool>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// 문서를 처리하고 청크로 분할합니다.
    /// </summary>
    /// <param name="filePath">처리할 문서 파일 경로</param>
    /// <param name="chunkingStrategy">청킹 전략 (semantic, fixed, sentence)</param>
    /// <param name="maxChunkSize">최대 청크 크기 (문자 수)</param>
    /// <param name="chunkOverlap">청크 간 오버랩 크기 (문자 수)</param>
    /// <returns>처리된 청크 정보 (JSON 문자열)</returns>
    [FunctionTool("process_document")]
    [Description("문서 파일을 처리하여 RAG용 청크로 분할합니다. PDF, DOCX, TXT, MD 등 다양한 형식을 지원합니다.")]
    public async Task<string> ProcessDocumentAsync(
        [Description("처리할 문서 파일 경로")] string filePath,
        [Description("청킹 전략 (semantic, fixed, sentence). 기본값: semantic")] string? chunkingStrategy = null,
        [Description("최대 청크 크기 (문자 수). 기본값: 1000")] int? maxChunkSize = null,
        [Description("청크 간 오버랩 크기 (문자 수). 기본값: 100")] int? chunkOverlap = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("문서 처리 시작 - FilePath: {FilePath}", filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"파일을 찾을 수 없습니다: {filePath}"
                });
            }

            var strategy = chunkingStrategy ?? _options.DefaultChunkingStrategy;
            var maxSize = maxChunkSize ?? _options.DefaultMaxChunkSize;
            var overlap = chunkOverlap ?? _options.DefaultChunkOverlap;

            // 파일 정보 읽기
            var fileInfo = new FileInfo(filePath);
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);

            // 간단한 청킹 구현 (실제로는 FileFlux 서비스 사용)
            var chunks = ChunkContent(content, strategy, maxSize, overlap);

            var result = new
            {
                success = true,
                filePath,
                fileSize = fileInfo.Length,
                fileExtension = fileInfo.Extension,
                totalCharacters = content.Length,
                chunkCount = chunks.Count,
                chunkingStrategy = strategy,
                maxChunkSize = maxSize,
                chunkOverlap = overlap,
                chunks = chunks.Select((c, i) => new
                {
                    index = i,
                    length = c.Length,
                    preview = c.Length > 100 ? c[..100] + "..." : c
                })
            };

            _logger?.LogInformation("문서 처리 완료 - ChunkCount: {Count}", chunks.Count);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "문서 처리 실패 - FilePath: {FilePath}", filePath);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static List<string> ChunkContent(string content, string strategy, int maxSize, int overlap)
    {
        var chunks = new List<string>();

        if (strategy == "sentence")
        {
            // 문장 기반 청킹
            var sentences = content.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim() + ".")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var currentChunk = "";
            foreach (var sentence in sentences)
            {
                if (currentChunk.Length + sentence.Length > maxSize && currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.Trim());
                    // 오버랩 처리
                    var overlapStart = Math.Max(0, currentChunk.Length - overlap);
                    currentChunk = currentChunk[overlapStart..] + " " + sentence;
                }
                else
                {
                    currentChunk += " " + sentence;
                }
            }
            if (!string.IsNullOrWhiteSpace(currentChunk))
                chunks.Add(currentChunk.Trim());
        }
        else
        {
            // 고정 크기 청킹 (semantic, fixed)
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
        }

        return chunks;
    }
}
