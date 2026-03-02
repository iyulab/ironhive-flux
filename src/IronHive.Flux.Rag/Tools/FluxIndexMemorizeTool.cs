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
/// 보안 검증 (경로, 크기, 심링크) 포함
/// </summary>
public partial class FluxIndexMemorizeTool
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    private readonly FluxRagToolsOptions _options;
    private readonly VaultSecurityOptions _securityOptions;
    private readonly IVault _vault;
    private readonly ILogger<FluxIndexMemorizeTool>? _logger;

    public FluxIndexMemorizeTool(
        IVault vault,
        IOptions<FluxRagToolsOptions> options,
        IOptions<VaultSecurityOptions>? securityOptions = null,
        ILogger<FluxIndexMemorizeTool>? logger = null)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _securityOptions = securityOptions?.Value ?? new VaultSecurityOptions();
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
            // Security validation
            var validationError = ValidateFileSecurity(filePath);
            if (validationError is not null)
            {
                if (_logger is not null)
                    LogSecurityValidationFailed(_logger, filePath, validationError);

                return JsonSerializer.Serialize(new
                {
                    success = false,
                    filePath,
                    error = validationError
                }, s_indentedJsonOptions);
            }

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

    #region Security Validation

    /// <summary>
    /// 파일 보안 검증을 수행합니다. 통과하면 null, 실패하면 에러 메시지를 반환합니다.
    /// </summary>
    internal string? ValidateFileSecurity(string filePath)
    {
        // 1. Path normalization
        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            return $"Invalid file path: {ex.Message}";
        }

        // 2. Check if file exists before further checks
        if (!File.Exists(normalizedPath))
        {
            // Let the caller handle file-not-found separately
            return null;
        }

        // 3. Symlink/junction rejection
        if (_securityOptions.RejectSymlinks)
        {
            var fileInfo = new FileInfo(normalizedPath);
            if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return $"Symlinks and junction points are not allowed: {normalizedPath}";
            }

            // Also check parent directories for symlinks
            var directory = fileInfo.Directory;
            while (directory is not null)
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return $"File path contains a symlink or junction directory: {directory.FullName}";
                }
                directory = directory.Parent;
            }
        }

        // 4. File size check
        {
            var fileInfo = new FileInfo(normalizedPath);
            if (fileInfo.Length > _securityOptions.MaxFileSizeBytes)
            {
                var sizeMb = fileInfo.Length / (1024.0 * 1024.0);
                var maxMb = _securityOptions.MaxFileSizeBytes / (1024.0 * 1024.0);
                return $"File size ({sizeMb:F1}MB) exceeds maximum allowed size ({maxMb:F1}MB)";
            }
        }

        // 5. AllowedBasePaths ACL
        if (_securityOptions.AllowedBasePaths.Count > 0)
        {
            var isAllowed = false;
            foreach (var basePath in _securityOptions.AllowedBasePaths)
            {
                var normalizedBase = Path.GetFullPath(basePath);
                if (normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
                {
                    isAllowed = true;
                    break;
                }
            }

            if (!isAllowed)
            {
                return $"File path is not within any allowed base path: {normalizedPath}";
            }
        }

        return null;
    }

    #endregion

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Information, Message = "Document memorize started - FilePath: {FilePath}")]
    private static partial void LogMemorizeStarted(ILogger logger, string FilePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Document memorize completed - FilePath: {FilePath}")]
    private static partial void LogMemorizeCompleted(ILogger logger, string FilePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Document memorize failed - FilePath: {FilePath}")]
    private static partial void LogMemorizeFailed(ILogger logger, Exception ex, string FilePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Security validation failed for {FilePath}: {Reason}")]
    private static partial void LogSecurityValidationFailed(ILogger logger, string FilePath, string Reason);

    #endregion
}
