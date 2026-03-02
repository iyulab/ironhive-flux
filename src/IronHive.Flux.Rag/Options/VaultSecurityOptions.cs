namespace IronHive.Flux.Rag.Options;

/// <summary>
/// 보안 옵션 - 파일 인덱싱 시 경로/크기/심링크 검증
/// </summary>
public class VaultSecurityOptions
{
    /// <summary>
    /// 최대 허용 파일 크기 (바이트). 기본값: 100MB
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// 허용된 기본 경로 목록. 비어 있으면 제한 없음.
    /// 설정 시 파일이 이 경로들 중 하나 아래에 있어야만 인덱싱 허용.
    /// </summary>
    public HashSet<string> AllowedBasePaths { get; set; } = [];

    /// <summary>
    /// 심볼릭 링크/junction 포인트 거부 여부. 기본값: true
    /// </summary>
    public bool RejectSymlinks { get; set; } = true;
}
