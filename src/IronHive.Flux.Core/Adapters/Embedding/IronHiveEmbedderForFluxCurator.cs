using IronHive.Abstractions.Embedding;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FluxCurator.Core.Core;

namespace IronHive.Flux.Core.Adapters.Embedding;

/// <summary>
/// IronHive <see cref="IEmbeddingGenerator"/> 를 FluxCurator <see cref="IEmbedder"/> 로 어댑트한다 — WebFlux 와 FileFlux 의 Semantic
/// 청킹은 FluxCurator 가 하고, FluxCurator 는 DI 의 <see cref="IEmbedder"/> 를 읽는다.
/// </summary>
public partial class IronHiveEmbedderForFluxCurator : IEmbedder
{
    private readonly IEmbeddingGenerator _generator;
    private readonly IronHiveFluxCoreOptions _options;
    private readonly ILogger<IronHiveEmbedderForFluxCurator>? _logger;

    public IronHiveEmbedderForFluxCurator(
        IEmbeddingGenerator generator,
        IOptions<IronHiveFluxCoreOptions> options,
        ILogger<IronHiveEmbedderForFluxCurator>? logger = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    public int EmbeddingDimension => _options.EmbeddingDimension > 0
        ? _options.EmbeddingDimension
        : 1536;

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
    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var list = texts as IReadOnlyList<string> ?? texts.ToList();
        if (_logger is not null)
            LogBatchEmbeddingStarted(_logger, list.Count);

        var response = await _generator.EmbedBatchAsync(
            _options.EmbeddingModelId,
            list,
            cancellationToken);

        var embeddings = response.Results.Select(r => r.Embedding ?? Array.Empty<float>()).ToList();
        if (_logger is not null)
            LogBatchEmbeddingCompleted(_logger, embeddings.Count);
        return embeddings;
    }

    /// <inheritdoc />
    /// <remarks>Cosine similarity; 0 when either vector has zero length.</remarks>
    public float CalculateSimilarity(float[] embedding1, float[] embedding2)
    {
        ArgumentNullException.ThrowIfNull(embedding1);
        ArgumentNullException.ThrowIfNull(embedding2);
        if (embedding1.Length != embedding2.Length)
            throw new ArgumentException("Embeddings must have the same dimension.", nameof(embedding2));

        double dot = 0, norm1 = 0, norm2 = 0;
        for (var i = 0; i < embedding1.Length; i++)
        {
            dot += embedding1[i] * (double)embedding2[i];
            norm1 += embedding1[i] * (double)embedding1[i];
            norm2 += embedding2[i] * (double)embedding2[i];
        }

        return norm1 == 0 || norm2 == 0 ? 0f : (float)(dot / Math.Sqrt(norm1 * norm2));
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxCurator embedding generation started - TextLength: {Length}")]
    private static partial void LogEmbeddingStarted(ILogger logger, int Length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxCurator embedding generation completed - Dimension: {Dimension}")]
    private static partial void LogEmbeddingCompleted(ILogger logger, int Dimension);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxCurator batch embedding generation started - Count: {Count}")]
    private static partial void LogBatchEmbeddingStarted(ILogger logger, int Count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxCurator batch embedding generation completed - Count: {Count}")]
    private static partial void LogBatchEmbeddingCompleted(ILogger logger, int Count);

    #endregion
}
