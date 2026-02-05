using FileFlux;
using IronHive.Abstractions.Embedding;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IronHive.Flux.Core.Adapters.Embedding;

/// <summary>
/// IronHive IEmbeddingGenerator를 FileFlux IEmbeddingService로 어댑트
/// </summary>
public class IronHiveEmbeddingServiceForFileFlux : FileFlux.IEmbeddingService
{
    private readonly IEmbeddingGenerator _generator;
    private readonly IronHiveFluxCoreOptions _options;
    private readonly ILogger<IronHiveEmbeddingServiceForFileFlux>? _logger;

    public IronHiveEmbeddingServiceForFileFlux(
        IEmbeddingGenerator generator,
        IOptions<IronHiveFluxCoreOptions> options,
        ILogger<IronHiveEmbeddingServiceForFileFlux>? logger = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    public int EmbeddingDimension => _options.EmbeddingDimension > 0
        ? _options.EmbeddingDimension
        : 1536; // OpenAI text-embedding-3-small 기본값

    /// <inheritdoc />
    public int MaxTokens => _options.MaxTokens;

    /// <inheritdoc />
    public bool SupportsBatchProcessing => true;

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        EmbeddingPurpose purpose = EmbeddingPurpose.Analysis,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("FileFlux 임베딩 생성 시작 - Purpose: {Purpose}, TextLength: {Length}",
            purpose, text.Length);

        var embedding = await _generator.EmbedAsync(
            _options.EmbeddingModelId,
            text,
            cancellationToken);

        _logger?.LogDebug("FileFlux 임베딩 생성 완료 - Dimension: {Dimension}", embedding.Length);
        return embedding;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<float[]>> GenerateBatchEmbeddingsAsync(
        IEnumerable<string> texts,
        EmbeddingPurpose purpose = EmbeddingPurpose.Analysis,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        _logger?.LogDebug("FileFlux 배치 임베딩 생성 시작 - Purpose: {Purpose}, Count: {Count}",
            purpose, textList.Count);

        var results = await _generator.EmbedBatchAsync(
            _options.EmbeddingModelId,
            textList,
            cancellationToken);

        var embeddings = results.Select(r => r.Embedding ?? Array.Empty<float>()).ToList();
        _logger?.LogDebug("FileFlux 배치 임베딩 생성 완료 - Count: {Count}", embeddings.Count);
        return embeddings;
    }

    /// <inheritdoc />
    public double CalculateSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
            throw new ArgumentException("임베딩 차원이 일치하지 않습니다.");

        double dotProduct = 0;
        double norm1 = 0;
        double norm2 = 0;

        for (int i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            norm1 += embedding1[i] * embedding1[i];
            norm2 += embedding2[i] * embedding2[i];
        }

        var denominator = Math.Sqrt(norm1) * Math.Sqrt(norm2);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }
}
