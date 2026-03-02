using FluxIndex.Extensions.FileVault.Interfaces;
using IronHive.Core.Tools;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Rag.Tools;

/// <summary>
/// FluxIndex 배치 문서 저장 도구 - 여러 파일 또는 디렉토리 일괄 인덱싱
/// 보안 검증 (경로, 크기, 심링크) 포함
/// </summary>
public partial class FluxIndexBatchMemorizeTool
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Maximum number of files allowed in a single batch operation.
    /// </summary>
    internal const int MaxBatchSize = 100;

    private readonly FluxRagToolsOptions _options;
    private readonly VaultSecurityOptions _securityOptions;
    private readonly IVault _vault;
    private readonly ILogger<FluxIndexBatchMemorizeTool>? _logger;

    public FluxIndexBatchMemorizeTool(
        IVault vault,
        IOptions<FluxRagToolsOptions> options,
        IOptions<VaultSecurityOptions>? securityOptions = null,
        ILogger<FluxIndexBatchMemorizeTool>? logger = null)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _securityOptions = securityOptions?.Value ?? new VaultSecurityOptions();
        _logger = logger;
    }

    /// <summary>
    /// 여러 파일을 지식 베이스에 일괄 저장(인덱싱)합니다.
    /// 최대 100개 파일까지 동시 처리 가능합니다.
    /// </summary>
    /// <param name="filePaths">인덱싱할 파일 경로 목록</param>
    /// <param name="maxConcurrent">동시 처리 수 (기본: 4)</param>
    /// <returns>배치 저장 결과 (JSON 문자열)</returns>
    [FunctionTool("memorize_documents")]
    [Description("여러 파일을 지식 베이스에 일괄 저장합니다. 최대 100개 파일까지 동시 처리합니다.")]
    public async Task<string> MemorizeDocumentsAsync(
        [Description("인덱싱할 파일 경로 목록")] IList<string> filePaths,
        [Description("동시 처리 수. 기본값: 4")] int maxConcurrent = 4,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogBatchMemorizeStarted(_logger, filePaths.Count);

        try
        {
            if (filePaths.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No file paths provided."
                }, s_indentedJsonOptions);
            }

            if (filePaths.Count > MaxBatchSize)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Batch size ({filePaths.Count}) exceeds maximum allowed ({MaxBatchSize})."
                }, s_indentedJsonOptions);
            }

            maxConcurrent = Math.Clamp(maxConcurrent, 1, 16);

            var succeeded = new List<string>();
            var failed = new List<object>();

            using var semaphore = new SemaphoreSlim(maxConcurrent);
            var tasks = filePaths.Select(async filePath =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var error = ValidateFileSecurity(filePath);
                    if (error is not null)
                    {
                        lock (failed)
                        {
                            failed.Add(new { filePath, error });
                        }
                        return;
                    }

                    if (!File.Exists(filePath))
                    {
                        lock (failed)
                        {
                            failed.Add(new { filePath, error = $"File not found: {filePath}" });
                        }
                        return;
                    }

                    await _vault.MemorizeAsync(filePath, cancellationToken);
                    lock (succeeded)
                    {
                        succeeded.Add(filePath);
                    }
                }
                catch (Exception ex)
                {
                    lock (failed)
                    {
                        failed.Add(new { filePath, error = ex.Message });
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            var result = new
            {
                success = failed.Count == 0,
                totalRequested = filePaths.Count,
                succeededCount = succeeded.Count,
                failedCount = failed.Count,
                succeeded = succeeded.OrderBy(p => p).ToList(),
                failed,
                completedAt = DateTime.UtcNow.ToString("O"),
                message = failed.Count == 0
                    ? $"Successfully memorized all {succeeded.Count} documents."
                    : $"Memorized {succeeded.Count}/{filePaths.Count} documents. {failed.Count} failed."
            };

            if (_logger is not null)
                LogBatchMemorizeCompleted(_logger, succeeded.Count, failed.Count);
            return JsonSerializer.Serialize(result, s_indentedJsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogBatchMemorizeFailed(_logger, ex);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, s_indentedJsonOptions);
        }
    }

    /// <summary>
    /// 디렉토리를 스캔하여 매칭되는 파일을 일괄 저장(인덱싱)합니다.
    /// </summary>
    /// <param name="directoryPath">스캔할 디렉토리 경로</param>
    /// <param name="pattern">파일 패턴 (기본: *.*)</param>
    /// <param name="recursive">하위 디렉토리 포함 여부 (기본: true)</param>
    /// <param name="maxConcurrent">동시 처리 수 (기본: 4)</param>
    /// <returns>배치 저장 결과 (JSON 문자열)</returns>
    [FunctionTool("memorize_directory")]
    [Description("디렉토리를 스캔하여 매칭되는 모든 파일을 지식 베이스에 저장합니다.")]
    public async Task<string> MemorizeDirectoryAsync(
        [Description("스캔할 디렉토리 경로")] string directoryPath,
        [Description("파일 패턴. 기본값: *.*")] string pattern = "*.*",
        [Description("하위 디렉토리 포함 여부. 기본값: true")] bool recursive = true,
        [Description("동시 처리 수. 기본값: 4")] int maxConcurrent = 4,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogDirectoryMemorizeStarted(_logger, directoryPath, pattern);

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    directoryPath,
                    error = $"Directory not found: {directoryPath}"
                }, s_indentedJsonOptions);
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(directoryPath, pattern, searchOption);

            if (files.Length == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    directoryPath,
                    pattern,
                    recursive,
                    totalFound = 0,
                    succeededCount = 0,
                    failedCount = 0,
                    message = "No matching files found in directory."
                }, s_indentedJsonOptions);
            }

            if (files.Length > MaxBatchSize)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    directoryPath,
                    pattern,
                    recursive,
                    totalFound = files.Length,
                    error = $"Directory contains {files.Length} matching files, exceeding maximum batch size ({MaxBatchSize}). Use a more specific pattern or reduce scope."
                }, s_indentedJsonOptions);
            }

            // Delegate to batch memorize
            var batchResultJson = await MemorizeDocumentsAsync(files, maxConcurrent, cancellationToken);
            var batchResult = JsonDocument.Parse(batchResultJson);

            // Wrap with directory context
            var succeededCount = batchResult.RootElement.TryGetProperty("succeededCount", out var sc) ? sc.GetInt32() : 0;
            var failedCount = batchResult.RootElement.TryGetProperty("failedCount", out var fc) ? fc.GetInt32() : 0;

            var result = new
            {
                success = failedCount == 0,
                directoryPath,
                pattern,
                recursive,
                totalFound = files.Length,
                succeededCount,
                failedCount,
                succeeded = batchResult.RootElement.TryGetProperty("succeeded", out var succ) ? succ.Clone() : default,
                failed = batchResult.RootElement.TryGetProperty("failed", out var fail) ? fail.Clone() : default,
                completedAt = DateTime.UtcNow.ToString("O"),
                message = failedCount == 0
                    ? $"Successfully memorized all {succeededCount} documents from '{Path.GetFileName(directoryPath)}'."
                    : $"Memorized {succeededCount}/{files.Length} documents from '{Path.GetFileName(directoryPath)}'. {failedCount} failed."
            };

            if (_logger is not null)
                LogDirectoryMemorizeCompleted(_logger, directoryPath, succeededCount, failedCount);
            return JsonSerializer.Serialize(result, s_indentedJsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogDirectoryMemorizeFailed(_logger, ex, directoryPath);
            return JsonSerializer.Serialize(new
            {
                success = false,
                directoryPath,
                error = ex.Message
            }, s_indentedJsonOptions);
        }
    }

    #region Security Validation

    /// <summary>
    /// 파일 보안 검증을 수행합니다. 통과하면 null, 실패하면 에러 메시지를 반환합니다.
    /// FluxIndexMemorizeTool의 동일 로직을 재사용합니다.
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Batch memorize started - FileCount: {FileCount}")]
    private static partial void LogBatchMemorizeStarted(ILogger logger, int FileCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Batch memorize completed - Succeeded: {Succeeded}, Failed: {Failed}")]
    private static partial void LogBatchMemorizeCompleted(ILogger logger, int Succeeded, int Failed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Batch memorize failed")]
    private static partial void LogBatchMemorizeFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Directory memorize started - Path: {DirectoryPath}, Pattern: {Pattern}")]
    private static partial void LogDirectoryMemorizeStarted(ILogger logger, string DirectoryPath, string Pattern);

    [LoggerMessage(Level = LogLevel.Information, Message = "Directory memorize completed - Path: {DirectoryPath}, Succeeded: {Succeeded}, Failed: {Failed}")]
    private static partial void LogDirectoryMemorizeCompleted(ILogger logger, string DirectoryPath, int Succeeded, int Failed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Directory memorize failed - Path: {DirectoryPath}")]
    private static partial void LogDirectoryMemorizeFailed(ILogger logger, Exception ex, string DirectoryPath);

    #endregion
}
