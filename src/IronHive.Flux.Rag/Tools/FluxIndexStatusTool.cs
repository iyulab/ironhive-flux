using FluxFeed.Domain.Enums;
using FluxFeed.Interfaces;
using IronHive.Core.Tools;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Rag.Tools;

/// <summary>
/// FluxIndex Vault 상태 및 관리 도구 - 지식 베이스 상태 조회, 문서 정보, 변경 감지
/// </summary>
public partial class FluxIndexStatusTool
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    private readonly IVault _vault;
    private readonly ILogger<FluxIndexStatusTool>? _logger;

    public FluxIndexStatusTool(
        IVault vault,
        ILogger<FluxIndexStatusTool>? logger = null)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _logger = logger;
    }

    /// <summary>
    /// 지식 베이스의 전체 상태를 조회합니다.
    /// </summary>
    /// <returns>Vault 상태 요약 (JSON 문자열)</returns>
    [FunctionTool("knowledge_base_status")]
    [Description("지식 베이스의 전체 상태를 조회합니다. 문서 수, 처리 상태, 큐 상태 등을 확인합니다.")]
    public async Task<string> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogStatusRequested(_logger);

        try
        {
            var status = await _vault.StatusAsync(cancellationToken);

            var result = new
            {
                success = true,
                // Entry counts by stage
                totalEntries = status.TotalEntries,
                sourceCount = status.SourceCount,
                extractedCount = status.ExtractedCount,
                memorizedCount = status.MemorizedCount,
                // Sync status
                inSyncCount = status.InSyncCount,
                sourceModifiedCount = status.SourceModifiedCount,
                vaultModifiedCount = status.VaultModifiedCount,
                sourceDeletedCount = status.SourceDeletedCount,
                errorCount = status.ErrorCount,
                // Queue status
                queuedCount = status.QueuedCount,
                processingCount = status.ProcessingCount,
                failedCount = status.FailedCount,
                orphanedCount = status.OrphanedCount,
                // Watchers
                activeWatcherCount = status.ActiveWatcherCount,
                pausedWatcherCount = status.PausedWatcherCount,
                // Storage
                totalStorageSizeMb = Math.Round(status.TotalStorageSizeBytes / (1024.0 * 1024.0), 2),
                // Timing
                lastSyncTime = status.LastSyncTime?.ToString("O"),
                statusAsOf = status.StatusAsOf.ToString("O")
            };

            if (_logger is not null)
                LogStatusCompleted(_logger, status.TotalEntries);
            return JsonSerializer.Serialize(result, s_indentedJsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogStatusFailed(_logger, ex);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, s_indentedJsonOptions);
        }
    }

    /// <summary>
    /// 특정 파일의 상세 정보를 조회합니다.
    /// </summary>
    /// <param name="filePath">조회할 파일 경로</param>
    /// <returns>문서 정보 (JSON 문자열)</returns>
    [FunctionTool("get_document_info")]
    [Description("지식 베이스에 등록된 특정 문서의 상세 정보를 조회합니다.")]
    public async Task<string> GetDocumentInfoAsync(
        [Description("조회할 파일의 전체 경로")] string filePath,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogGetDocumentInfoRequested(_logger, filePath);

        try
        {
            var entry = await _vault.GetAsync(filePath, cancellationToken);
            if (entry is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    filePath,
                    error = $"No entry found for: {filePath}"
                }, s_indentedJsonOptions);
            }

            var result = new
            {
                success = true,
                filePath = entry.SourcePath,
                fileName = entry.FileName,
                stage = entry.Stage.ToString(),
                syncStatus = entry.SyncStatus.ToString(),
                chunkCount = entry.ChunkCount,
                createdAt = entry.CreatedAt.ToString("O"),
                lastProcessedAt = entry.LastProcessedAt?.ToString("O"),
                lastError = entry.LastError,
                retryCount = entry.RetryCount,
                sourceExists = entry.SourceExists,
                vaultExists = entry.VaultExists,
                extractedExists = entry.ExtractedExists,
                refinedExists = entry.RefinedExists
            };

            if (_logger is not null)
            {
                var stageName = entry.Stage.ToString();
                LogGetDocumentInfoCompleted(_logger, filePath, stageName);
            }
            return JsonSerializer.Serialize(result, s_indentedJsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogGetDocumentInfoFailed(_logger, ex, filePath);
            return JsonSerializer.Serialize(new
            {
                success = false,
                filePath,
                error = ex.Message
            }, s_indentedJsonOptions);
        }
    }

    /// <summary>
    /// 등록된 모든 문서 목록을 조회합니다.
    /// </summary>
    /// <param name="stageFilter">처리 단계 필터 (Source, Extracted, Refined, Memorized). null이면 전체 조회</param>
    /// <returns>문서 목록 (JSON 문자열)</returns>
    [FunctionTool("list_documents")]
    [Description("지식 베이스에 등록된 모든 문서를 나열합니다. 선택적으로 처리 단계로 필터링할 수 있습니다.")]
    public async Task<string> ListDocumentsAsync(
        [Description("처리 단계 필터 (Source, Extracted, Refined, Memorized). 비워두면 전체 조회")] string? stageFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogListDocumentsRequested(_logger, stageFilter);

        try
        {
            ProcessingStage? stage = null;
            if (!string.IsNullOrWhiteSpace(stageFilter))
            {
                if (!Enum.TryParse<ProcessingStage>(stageFilter, ignoreCase: true, out var parsed))
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Invalid stage filter: '{stageFilter}'. Valid values: Source, Extracted, Refined, Memorized"
                    }, s_indentedJsonOptions);
                }
                stage = parsed;
            }

            var entries = await _vault.ListAsync(stage, cancellationToken);

            var documents = entries.Select(e => new
            {
                filePath = e.SourcePath,
                fileName = e.FileName,
                stage = e.Stage.ToString(),
                syncStatus = e.SyncStatus.ToString(),
                chunkCount = e.ChunkCount,
                createdAt = e.CreatedAt.ToString("O"),
                lastProcessedAt = e.LastProcessedAt?.ToString("O"),
                sourceExists = e.SourceExists
            }).ToList();

            var result = new
            {
                success = true,
                stageFilter = stageFilter ?? "all",
                totalCount = documents.Count,
                documents
            };

            if (_logger is not null)
                LogListDocumentsCompleted(_logger, documents.Count, stageFilter);
            return JsonSerializer.Serialize(result, s_indentedJsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogListDocumentsFailed(_logger, ex);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, s_indentedJsonOptions);
        }
    }

    /// <summary>
    /// 파일의 변경 여부를 감지합니다.
    /// </summary>
    /// <param name="filePath">확인할 파일 경로</param>
    /// <returns>변경 감지 결과 (JSON 문자열)</returns>
    [FunctionTool("detect_changes")]
    [Description("파일이 지식 베이스에 저장된 이후 변경되었는지 확인합니다.")]
    public async Task<string> DetectChangesAsync(
        [Description("변경 감지할 파일의 전체 경로")] string filePath,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogDetectChangesRequested(_logger, filePath);

        try
        {
            var changes = await _vault.DetectChangesAsync(filePath, cancellationToken);

            var result = new
            {
                success = true,
                filePath = changes.FilePath,
                fileName = changes.FileName,
                entryExists = changes.EntryExists,
                sourceExists = changes.SourceExists,
                hasChanges = changes.HasChanges,
                sourceChanged = changes.SourceChanged,
                vaultChanged = changes.VaultChanged,
                recommendedAction = changes.RecommendedAction.ToString(),
                // File metadata
                fileExtension = changes.FileExtension,
                fileSize = changes.FileSize,
                fileModifiedAt = changes.FileModifiedAt?.ToString("O"),
                // Vault status
                stage = changes.Stage?.ToString(),
                syncStatus = changes.SyncStatus?.ToString(),
                chunkCount = changes.ChunkCount,
                lastError = changes.LastError,
                modifiedVaultFiles = changes.ModifiedVaultFiles
            };

            if (_logger is not null)
            {
                var actionName = changes.RecommendedAction.ToString();
                LogDetectChangesCompleted(_logger, filePath, changes.HasChanges, actionName);
            }
            return JsonSerializer.Serialize(result, s_indentedJsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogDetectChangesFailed(_logger, ex, filePath);
            return JsonSerializer.Serialize(new
            {
                success = false,
                filePath,
                error = ex.Message
            }, s_indentedJsonOptions);
        }
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Information, Message = "Knowledge base status requested")]
    private static partial void LogStatusRequested(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Knowledge base status completed - TotalEntries: {TotalEntries}")]
    private static partial void LogStatusCompleted(ILogger logger, int TotalEntries);

    [LoggerMessage(Level = LogLevel.Error, Message = "Knowledge base status failed")]
    private static partial void LogStatusFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Get document info requested - FilePath: {FilePath}")]
    private static partial void LogGetDocumentInfoRequested(ILogger logger, string FilePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Get document info completed - FilePath: {FilePath}, Stage: {Stage}")]
    private static partial void LogGetDocumentInfoCompleted(ILogger logger, string FilePath, string Stage);

    [LoggerMessage(Level = LogLevel.Error, Message = "Get document info failed - FilePath: {FilePath}")]
    private static partial void LogGetDocumentInfoFailed(ILogger logger, Exception ex, string FilePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "List documents requested - StageFilter: {StageFilter}")]
    private static partial void LogListDocumentsRequested(ILogger logger, string? StageFilter);

    [LoggerMessage(Level = LogLevel.Information, Message = "List documents completed - Count: {Count}, StageFilter: {StageFilter}")]
    private static partial void LogListDocumentsCompleted(ILogger logger, int Count, string? StageFilter);

    [LoggerMessage(Level = LogLevel.Error, Message = "List documents failed")]
    private static partial void LogListDocumentsFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Detect changes requested - FilePath: {FilePath}")]
    private static partial void LogDetectChangesRequested(ILogger logger, string FilePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Detect changes completed - FilePath: {FilePath}, HasChanges: {HasChanges}, RecommendedAction: {Action}")]
    private static partial void LogDetectChangesCompleted(ILogger logger, string FilePath, bool HasChanges, string Action);

    [LoggerMessage(Level = LogLevel.Error, Message = "Detect changes failed - FilePath: {FilePath}")]
    private static partial void LogDetectChangesFailed(ILogger logger, Exception ex, string FilePath);

    #endregion
}
