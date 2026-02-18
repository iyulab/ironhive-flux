namespace IronHive.Tools.SystemHarness;

/// <summary>
/// system-harness MCP 서버 연결 옵션
/// </summary>
public class SystemHarnessOptions
{
    /// <summary>
    /// MCP 서버 이름. 기본값: "system-harness"
    /// </summary>
    public string ServerName { get; set; } = "system-harness";

    /// <summary>
    /// dotnet 실행 파일 경로. 기본값: "dotnet"
    /// </summary>
    public string DotnetPath { get; set; } = "dotnet";

    /// <summary>
    /// SystemHarness.Mcp 프로젝트 경로.
    /// null이면 ExecutablePath를 사용합니다.
    /// </summary>
    public string? ProjectPath { get; set; }

    /// <summary>
    /// 미리 빌드된 실행 파일 경로.
    /// ProjectPath가 설정되면 무시됩니다.
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// 서버 프로세스 작업 디렉터리
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 서버 프로세스에 전달할 환경 변수
    /// </summary>
    public Dictionary<string, string?>? EnvironmentVariables { get; set; }

    /// <summary>
    /// 서버 종료 대기 시간. 기본값: 5초
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 안전 가드 활성화 여부 (서버측 CommandPolicy).
    /// true이면 환경 변수로 SYSTEM_HARNESS_SAFETY=true를 전달합니다.
    /// </summary>
    public bool EnableSafetyGuards { get; set; } = true;

    internal string BuildCommand()
    {
        if (ExecutablePath is not null && ProjectPath is null)
            return ExecutablePath;
        return DotnetPath;
    }

    internal IEnumerable<string> BuildArguments()
    {
        if (ProjectPath is not null)
        {
            yield return "run";
            yield return "--project";
            yield return ProjectPath;
        }
        else if (ExecutablePath is not null && DotnetPath != ExecutablePath)
        {
            // ExecutablePath가 별도 지정되었으면 인수 없음
            yield break;
        }
    }

    internal Dictionary<string, string?> BuildEnvironmentVariables()
    {
        var env = EnvironmentVariables is not null
            ? new Dictionary<string, string?>(EnvironmentVariables)
            : new Dictionary<string, string?>();

        if (EnableSafetyGuards)
        {
            env["SYSTEM_HARNESS_SAFETY"] = "true";
        }

        return env;
    }
}
