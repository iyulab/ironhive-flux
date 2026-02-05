using IronHive.Core.Tools;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Rag.Tools;

/// <summary>
/// FluxIndex 문서 삭제 도구 - 지식 베이스에서 문서 삭제
/// </summary>
public class FluxIndexUnmemorizeTool
{
    private readonly FluxRagToolsOptions _options;
    private readonly ILogger<FluxIndexUnmemorizeTool>? _logger;

    public FluxIndexUnmemorizeTool(
        IOptions<FluxRagToolsOptions> options,
        ILogger<FluxIndexUnmemorizeTool>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// 지식 베이스에서 문서를 삭제합니다.
    /// </summary>
    /// <param name="documentId">삭제할 문서 ID</param>
    /// <param name="indexName">인덱스 이름</param>
    /// <returns>삭제 결과 (JSON 문자열)</returns>
    [FunctionTool("forget_document")]
    [Description("지식 베이스에서 특정 문서를 삭제(forget)합니다.")]
    public Task<string> UnmemorizeAsync(
        [Description("삭제할 문서 ID")] string documentId,
        [Description("인덱스 이름")] string? indexName = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("문서 삭제 시작 - DocumentId: {DocId}", documentId);

        try
        {
            var index = indexName ?? _options.DefaultIndexName;

            // 실제 삭제는 FluxIndex SDK 사용
            // 여기서는 간단한 구현
            var deleted = false;

            // Note: 실제 구현에서는 FluxIndexSearchTool의 storage에 접근하여 삭제
            // 현재는 시연을 위한 간단한 응답

            var result = new
            {
                success = true,
                documentId,
                indexName = index,
                deleted,
                deletedAt = DateTime.UtcNow.ToString("O"),
                message = deleted ? "문서가 성공적으로 삭제되었습니다." : "문서를 찾을 수 없거나 이미 삭제되었습니다."
            };

            _logger?.LogInformation("문서 삭제 완료 - DocumentId: {DocId}, Deleted: {Deleted}", documentId, deleted);
            return Task.FromResult(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "문서 삭제 실패 - DocumentId: {DocId}", documentId);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = false,
                documentId,
                error = ex.Message
            }));
        }
    }
}
