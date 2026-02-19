using FluxIndex.Extensions.FileVault.Interfaces;
using IronHive.Core.Tools;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Rag.Tools;

/// <summary>
/// FluxIndex 문서 삭제 도구 - IVault를 통한 인덱스 제거
/// </summary>
public partial class FluxIndexUnmemorizeTool
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    private readonly FluxRagToolsOptions _options;
    private readonly IVault _vault;
    private readonly ILogger<FluxIndexUnmemorizeTool>? _logger;

    public FluxIndexUnmemorizeTool(
        IVault vault,
        IOptions<FluxRagToolsOptions> options,
        ILogger<FluxIndexUnmemorizeTool>? logger = null)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// 지식 베이스에서 파일을 삭제(인덱스 제거)합니다.
    /// </summary>
    /// <param name="filePath">삭제할 파일 경로</param>
    /// <returns>삭제 결과 (JSON 문자열)</returns>
    [FunctionTool("forget_document")]
    [Description("지식 베이스에서 특정 파일을 삭제(forget)합니다. 파일의 벡터 데이터와 인덱스를 제거합니다.")]
    public async Task<string> UnmemorizeAsync(
        [Description("삭제할 파일의 전체 경로")] string filePath,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogUnmemorizeStarted(_logger, filePath);

        try
        {
            await _vault.RemoveAsync(filePath, cancellationToken);

            var result = new
            {
                success = true,
                filePath,
                deleted = true,
                deletedAt = DateTime.UtcNow.ToString("O"),
                message = $"Successfully removed '{Path.GetFileName(filePath)}' from knowledge base."
            };

            if (_logger is not null)
                LogUnmemorizeCompleted(_logger, filePath, true);
            return JsonSerializer.Serialize(result, s_indentedJsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogUnmemorizeFailed(_logger, ex, filePath);
            return JsonSerializer.Serialize(new
            {
                success = false,
                filePath,
                deleted = false,
                error = ex.Message
            }, s_indentedJsonOptions);
        }
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Information, Message = "Document unmemorize started - FilePath: {FilePath}")]
    private static partial void LogUnmemorizeStarted(ILogger logger, string FilePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Document unmemorize completed - FilePath: {FilePath}, Deleted: {Deleted}")]
    private static partial void LogUnmemorizeCompleted(ILogger logger, string FilePath, bool Deleted);

    [LoggerMessage(Level = LogLevel.Error, Message = "Document unmemorize failed - FilePath: {FilePath}")]
    private static partial void LogUnmemorizeFailed(ILogger logger, Exception ex, string FilePath);

    #endregion
}
