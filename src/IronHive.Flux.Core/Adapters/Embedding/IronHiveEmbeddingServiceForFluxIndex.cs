using FluxIndex.Core.Application.Interfaces;
using IronHive.Abstractions.Embedding;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IronHive.Flux.Core.Adapters.Embedding;

/// <summary>
/// IronHive IEmbeddingGenerator를 FluxIndex IEmbeddingService로 어댑트
/// </summary>
public class IronHiveEmbeddingServiceForFluxIndex : FluxIndex.Core.Application.Interfaces.IEmbeddingService
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
        _logger?.LogDebug("FluxIndex 임베딩 생성 시작 - TextLength: {Length}", text.Length);

        var embedding = await _generator.EmbedAsync(
            _options.EmbeddingModelId,
            text,
            cancellationToken);

        _logger?.LogDebug("FluxIndex 임베딩 생성 완료 - Dimension: {Dimension}", embedding.Length);
        return embedding;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<float[]>> GenerateEmbeddingsBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        _logger?.LogDebug("FluxIndex 배치 임베딩 생성 시작 - Count: {Count}", textList.Count);

        var results = await _generator.EmbedBatchAsync(
            _options.EmbeddingModelId,
            textList,
            cancellationToken);

        var embeddings = results.Select(r => r.Embedding ?? Array.Empty<float>()).ToList();
        _logger?.LogDebug("FluxIndex 배치 임베딩 생성 완료 - Count: {Count}", embeddings.Count);
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
        _logger?.LogDebug("FluxIndex 토큰 카운트 시작 - TextLength: {Length}", text.Length);

        var count = await _generator.CountTokensAsync(
            _options.EmbeddingModelId,
            text,
            cancellationToken);

        _logger?.LogDebug("FluxIndex 토큰 카운트 완료 - Count: {Count}", count);
        return count;
    }
}
