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
public partial class FluxIndexUnmemorizeTool
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

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
        if (_logger is not null)
            LogUnmemorizeStarted(_logger, documentId);

        try
        {
            var index = indexName ?? _options.DefaultIndexName;
            var deleted = FluxIndexSearchTool.RemoveDocument(index, documentId);

            var result = new
            {
                success = deleted,
                documentId,
                indexName = index,
                deleted,
                deletedAt = deleted ? DateTime.UtcNow.ToString("O") : null,
                message = deleted ? "문서가 성공적으로 삭제되었습니다." : "문서를 찾을 수 없거나 이미 삭제되었습니다."
            };

            if (_logger is not null)
                LogUnmemorizeCompleted(_logger, documentId, deleted);
            return Task.FromResult(JsonSerializer.Serialize(result, s_indentedJsonOptions));
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogUnmemorizeFailed(_logger, ex, documentId);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = false,
                documentId,
                error = ex.Message
            }));
        }
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Information, Message = "문서 삭제 시작 - DocumentId: {DocId}")]
    private static partial void LogUnmemorizeStarted(ILogger logger, string DocId);

    [LoggerMessage(Level = LogLevel.Information, Message = "문서 삭제 완료 - DocumentId: {DocId}, Deleted: {Deleted}")]
    private static partial void LogUnmemorizeCompleted(ILogger logger, string DocId, bool Deleted);

    [LoggerMessage(Level = LogLevel.Error, Message = "문서 삭제 실패 - DocumentId: {DocId}")]
    private static partial void LogUnmemorizeFailed(ILogger logger, Exception ex, string DocId);

    #endregion
}
