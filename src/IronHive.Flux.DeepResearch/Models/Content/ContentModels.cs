namespace IronHive.Flux.DeepResearch.Models.Content;

/// <summary>
/// 추출된 콘텐츠
/// </summary>
public record ExtractedContent
{
    public required string Url { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public string? Author { get; init; }
    public DateTimeOffset? PublishedDate { get; init; }
    public required DateTimeOffset ExtractedAt { get; init; }
    public int ContentLength { get; init; }
    public bool Success { get; init; } = true;
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 콘텐츠 청크
/// </summary>
public record ContentChunk
{
    public required string SourceId { get; init; }
    public required string Content { get; init; }
    public required int ChunkIndex { get; init; }
    public int TokenCount { get; init; }
}

/// <summary>
/// 소스 문서
/// </summary>
public record SourceDocument
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public string? Author { get; init; }
    public DateTimeOffset? PublishedDate { get; init; }
    public required DateTimeOffset ExtractedAt { get; init; }
    public required string Provider { get; init; }
    public double RelevanceScore { get; init; }
    public double TrustScore { get; init; }
}
