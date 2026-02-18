using IronHive.Abstractions.Embedding;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebFlux.Core.Interfaces;

namespace IronHive.Flux.Core.Adapters.Embedding;

/// <summary>
/// IronHive IEmbeddingGenerator를 WebFlux ITextEmbeddingService로 어댑트
/// </summary>
public partial class IronHiveEmbeddingServiceForWebFlux : ITextEmbeddingService
{
    private readonly IEmbeddingGenerator _generator;
    private readonly IronHiveFluxCoreOptions _options;
    private readonly ILogger<IronHiveEmbeddingServiceForWebFlux>? _logger;

    public IronHiveEmbeddingServiceForWebFlux(
        IEmbeddingGenerator generator,
        IOptions<IronHiveFluxCoreOptions> options,
        ILogger<IronHiveEmbeddingServiceForWebFlux>? logger = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    public int MaxTokens => _options.MaxTokens;

    /// <inheritdoc />
    public int EmbeddingDimension => _options.EmbeddingDimension > 0
        ? _options.EmbeddingDimension
        : 1536;

    /// <inheritdoc />
    public async Task<float[]> GetEmbeddingAsync(
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
    public async Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogBatchEmbeddingStarted(_logger, texts.Count);

        var results = await _generator.EmbedBatchAsync(
            _options.EmbeddingModelId,
            texts,
            cancellationToken);

        var embeddings = results.Select(r => r.Embedding ?? Array.Empty<float>()).ToList();
        if (_logger is not null)
            LogBatchEmbeddingCompleted(_logger, embeddings.Count);
        return embeddings;
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Debug, Message = "WebFlux 임베딩 생성 시작 - TextLength: {Length}")]
    private static partial void LogEmbeddingStarted(ILogger logger, int Length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "WebFlux 임베딩 생성 완료 - Dimension: {Dimension}")]
    private static partial void LogEmbeddingCompleted(ILogger logger, int Dimension);

    [LoggerMessage(Level = LogLevel.Debug, Message = "WebFlux 배치 임베딩 생성 시작 - Count: {Count}")]
    private static partial void LogBatchEmbeddingStarted(ILogger logger, int Count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "WebFlux 배치 임베딩 생성 완료 - Count: {Count}")]
    private static partial void LogBatchEmbeddingCompleted(ILogger logger, int Count);

    #endregion
}
