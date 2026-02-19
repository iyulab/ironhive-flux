using FluxIndex.Extensions.FileVault.Interfaces;
using IronHive.Core.Tools;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Rag.Tools;

/// <summary>
/// FluxIndex 문서 저장 도구 - IVault를 통한 파일 인덱싱
/// </summary>
public partial class FluxIndexMemorizeTool
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    private readonly FluxRagToolsOptions _options;
    private readonly IVault _vault;
    private readonly ILogger<FluxIndexMemorizeTool>? _logger;

    public FluxIndexMemorizeTool(
        IVault vault,
        IOptions<FluxRagToolsOptions> options,
        ILogger<FluxIndexMemorizeTool>? logger = null)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// 파일을 지식 베이스에 저장(인덱싱)합니다.
    /// 파일 추출 → 청킹 → 임베딩 → 벡터 저장 파이프라인을 실행합니다.
    /// </summary>
    /// <param name="filePath">인덱싱할 파일 경로</param>
    /// <returns>저장 결과 (JSON 문자열)</returns>
    [FunctionTool("memorize_document")]
    [Description("파일을 지식 베이스에 저장하여 나중에 검색할 수 있도록 합니다. 파일 추출, 청킹, 임베딩 파이프라인을 자동 실행합니다.")]
    public async Task<string> MemorizeAsync(
        [Description("인덱싱할 파일의 전체 경로")] string filePath,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogMemorizeStarted(_logger, filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    filePath,
                    error = $"File not found: {filePath}"
                }, s_indentedJsonOptions);
            }

            await _vault.MemorizeAsync(filePath, cancellationToken);

            var result = new
            {
                success = true,
                filePath,
                fileName = Path.GetFileName(filePath),
                memorizedAt = DateTime.UtcNow.ToString("O"),
                message = $"Successfully memorized '{Path.GetFileName(filePath)}'"
            };

            if (_logger is not null)
                LogMemorizeCompleted(_logger, filePath);
            return JsonSerializer.Serialize(result, s_indentedJsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogMemorizeFailed(_logger, ex, filePath);
            return JsonSerializer.Serialize(new
            {
                success = false,
                filePath,
                error = ex.Message
            }, s_indentedJsonOptions);
        }
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Information, Message = "Document memorize started - FilePath: {FilePath}")]
    private static partial void LogMemorizeStarted(ILogger logger, string FilePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Document memorize completed - FilePath: {FilePath}")]
    private static partial void LogMemorizeCompleted(ILogger logger, string FilePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Document memorize failed - FilePath: {FilePath}")]
    private static partial void LogMemorizeFailed(ILogger logger, Exception ex, string FilePath);

    #endregion
}
