using IronHive.Abstractions.Embedding;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebFlux.Core.Interfaces;

namespace IronHive.Flux.Core.Adapters.Embedding;

/// <summary>
/// IronHive IEmbeddingGenerator를 WebFlux ITextEmbeddingService로 어댑트
/// </summary>
public class IronHiveEmbeddingServiceForWebFlux : ITextEmbeddingService
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
        _logger?.LogDebug("WebFlux 임베딩 생성 시작 - TextLength: {Length}", text.Length);

        var embedding = await _generator.EmbedAsync(
            _options.EmbeddingModelId,
            text,
            cancellationToken);

        _logger?.LogDebug("WebFlux 임베딩 생성 완료 - Dimension: {Dimension}", embedding.Length);
        return embedding;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("WebFlux 배치 임베딩 생성 시작 - Count: {Count}", texts.Count);

        var results = await _generator.EmbedBatchAsync(
            _options.EmbeddingModelId,
            texts,
            cancellationToken);

        var embeddings = results.Select(r => r.Embedding ?? Array.Empty<float>()).ToList();
        _logger?.LogDebug("WebFlux 배치 임베딩 생성 완료 - Count: {Count}", embeddings.Count);
        return embeddings;
    }
}
