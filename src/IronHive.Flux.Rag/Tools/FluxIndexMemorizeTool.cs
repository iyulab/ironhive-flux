using FluxIndex.Core.Application.Interfaces;
using IronHive.Core.Tools;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Rag.Tools;

/// <summary>
/// FluxIndex 문서 저장 도구 - 지식 베이스에 문서 저장
/// </summary>
public partial class FluxIndexMemorizeTool
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    private readonly FluxRagToolsOptions _options;
    private readonly IEmbeddingService? _embeddingService;
    private readonly ILogger<FluxIndexMemorizeTool>? _logger;

    public FluxIndexMemorizeTool(
        IOptions<FluxRagToolsOptions> options,
        IEmbeddingService? embeddingService = null,
        ILogger<FluxIndexMemorizeTool>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// 문서를 지식 베이스에 저장합니다.
    /// </summary>
    /// <param name="content">저장할 문서 내용</param>
    /// <param name="documentId">문서 고유 ID (없으면 자동 생성)</param>
    /// <param name="title">문서 제목</param>
    /// <param name="metadata">추가 메타데이터 (JSON 문자열)</param>
    /// <param name="indexName">인덱스 이름</param>
    /// <returns>저장 결과 (JSON 문자열)</returns>
    [FunctionTool("memorize_document")]
    [Description("문서를 지식 베이스에 저장하여 나중에 검색할 수 있도록 합니다. RAG 시스템의 데이터 저장 기능입니다.")]
    public async Task<string> MemorizeAsync(
        [Description("저장할 문서 내용")] string content,
        [Description("문서 고유 ID (없으면 자동 생성)")] string? documentId = null,
        [Description("문서 제목")] string? title = null,
        [Description("추가 메타데이터 (JSON 형식)")] string? metadata = null,
        [Description("인덱스 이름")] string? indexName = null,
        CancellationToken cancellationToken = default)
    {
        var docIdDisplay = documentId ?? "(auto)";
        if (_logger is not null)
            LogMemorizeStarted(_logger, docIdDisplay, content.Length);

        try
        {
            var docId = documentId ?? Guid.NewGuid().ToString();
            var index = indexName ?? _options.DefaultIndexName;

            // 메타데이터 파싱
            Dictionary<string, object>? metadataDict = null;
            if (!string.IsNullOrEmpty(metadata))
            {
                try
                {
                    metadataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(metadata);
                }
                catch
                {
                    if (_logger is not null)
                        LogMetadataParseFailed(_logger);
                }
            }

            metadataDict ??= new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(title))
                metadataDict["title"] = title;
            metadataDict["memorizedAt"] = DateTime.UtcNow.ToString("O");

            // 임베딩 생성 (서비스가 있는 경우)
            float[]? embedding = null;
            if (_embeddingService != null)
            {
                embedding = await _embeddingService.GenerateEmbeddingAsync(content, cancellationToken);
            }

            // 저장
            var document = new StoredDocument
            {
                Id = docId,
                Content = content,
                Embedding = embedding,
                Metadata = metadataDict
            };

            FluxIndexSearchTool.AddDocument(index, document);

            var result = new
            {
                success = true,
                documentId = docId,
                indexName = index,
                contentLength = content.Length,
                hasEmbedding = embedding != null,
                embeddingDimension = embedding?.Length ?? 0,
                metadata = metadataDict,
                memorizedAt = DateTime.UtcNow.ToString("O")
            };

            if (_logger is not null)
                LogMemorizeCompleted(_logger, docId);
            return JsonSerializer.Serialize(result, s_indentedJsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogMemorizeFailed(_logger, ex, documentId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                documentId,
                error = ex.Message
            });
        }
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Information, Message = "문서 저장 시작 - DocumentId: {DocId}, ContentLength: {Length}")]
    private static partial void LogMemorizeStarted(ILogger logger, string DocId, int Length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "메타데이터 파싱 실패, 무시됨")]
    private static partial void LogMetadataParseFailed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "문서 저장 완료 - DocumentId: {DocId}")]
    private static partial void LogMemorizeCompleted(ILogger logger, string DocId);

    [LoggerMessage(Level = LogLevel.Error, Message = "문서 저장 실패 - DocumentId: {DocId}")]
    private static partial void LogMemorizeFailed(ILogger logger, Exception ex, string? DocId);

    #endregion
}
