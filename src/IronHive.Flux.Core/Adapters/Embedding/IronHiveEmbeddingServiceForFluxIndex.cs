using FluxIndex.Core.Application.Interfaces;
using IronHive.Abstractions.Embedding;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IronHive.Flux.Core.Adapters.Embedding;

/// <summary>
/// IronHive IEmbeddingGenerator를 FluxIndex IEmbeddingService로 어댑트
/// </summary>
public partial class IronHiveEmbeddingServiceForFluxIndex : FluxIndex.Core.Application.Interfaces.IEmbeddingService
{
    private readonly IEmbeddingGenerator _generator;
    private readonly IronHiveFluxCoreOptions _options;
    private readonly ILogger<IronHiveEmbeddingServiceForFluxIndex>? _logger;

    public IronHiveEmbeddingServiceForFluxIndex(
        IEmbeddingGenerator generator,
        IOptions<IronHiveFluxCoreOptions> options,
        ILogger<IronHiveEmbeddingServiceForFluxIndex>? logger = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogEmbeddingStarted(_logger, text.Length);

        var embedding = await _generator.EmbedAsync(
            _options.EmbeddingModelId,
            text,
            cancellationToken);

        if (_logger is not null)
            LogEmbeddingCompleted(_logger, embedding.Length);
        return embedding;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<float[]>> GenerateEmbeddingsBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (_logger is not null)
            LogBatchEmbeddingStarted(_logger, textList.Count);

        var results = await _generator.EmbedBatchAsync(
            _options.EmbeddingModelId,
            textList,
            cancellationToken);

        var embeddings = results.Select(r => r.Embedding ?? Array.Empty<float>()).ToList();
        if (_logger is not null)
            LogBatchEmbeddingCompleted(_logger, embeddings.Count);
        return embeddings;
    }

    /// <inheritdoc />
    public int GetEmbeddingDimension()
    {
        return _options.EmbeddingDimension > 0
            ? _options.EmbeddingDimension
            : 1536;
    }

    /// <inheritdoc />
    public string GetModelName()
    {
        return _options.EmbeddingModelId;
    }

    /// <inheritdoc />
    public int GetMaxTokens()
    {
        return _options.MaxTokens;
    }

    /// <inheritdoc />
    public async Task<int> CountTokensAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogTokenCountStarted(_logger, text.Length);

        var count = await _generator.CountTokensAsync(
            _options.EmbeddingModelId,
            text,
            cancellationToken);

        if (_logger is not null)
            LogTokenCountCompleted(_logger, count);
        return count;
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxIndex 임베딩 생성 시작 - TextLength: {Length}")]
    private static partial void LogEmbeddingStarted(ILogger logger, int Length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxIndex 임베딩 생성 완료 - Dimension: {Dimension}")]
    private static partial void LogEmbeddingCompleted(ILogger logger, int Dimension);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxIndex 배치 임베딩 생성 시작 - Count: {Count}")]
    private static partial void LogBatchEmbeddingStarted(ILogger logger, int Count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxIndex 배치 임베딩 생성 완료 - Count: {Count}")]
    private static partial void LogBatchEmbeddingCompleted(ILogger logger, int Count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxIndex 토큰 카운트 시작 - TextLength: {Length}")]
    private static partial void LogTokenCountStarted(ILogger logger, int Length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxIndex 토큰 카운트 완료 - Count: {Count}")]
    private static partial void LogTokenCountCompleted(ILogger logger, int Count);

    #endregion
}
