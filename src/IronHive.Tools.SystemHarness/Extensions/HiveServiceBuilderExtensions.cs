using IronHive.Plugins.MCP.Configurations;
using IronHive.Tools.SystemHarness;

namespace IronHive.Abstractions;

/// <summary>
/// system-harness MCP 서버를 IronHive에 연결하는 확장 메서드
/// </summary>
public static class SystemHarnessBuilderExtensions
{
    /// <summary>
    /// system-harness MCP 서버를 에이전트 도구로 등록합니다.
    /// 174개 명령 (파일, 앱, 화면, 키보드, OCR 등)이 help/do/get 3-tool dispatch로 제공됩니다.
    /// </summary>
    /// <param name="builder">HiveServiceBuilder 인스턴스</param>
    /// <param name="configure">옵션 구성 액션</param>
    /// <returns>빌더 인스턴스 (fluent chaining)</returns>
    public static IHiveServiceBuilder AddSystemHarness(
        this IHiveServiceBuilder builder,
        Action<SystemHarnessOptions>? configure = null)
    {
        var options = new SystemHarnessOptions();
        configure?.Invoke(options);

        var config = new McpStdioClientConfig
        {
            ServerName = options.ServerName,
            Command = options.BuildCommand(),
            Arguments = options.BuildArguments().ToArray(),
            WorkingDirectory = options.WorkingDirectory,
            EnvironmentVariables = options.BuildEnvironmentVariables(),
            ShutdownTimeout = options.ShutdownTimeout
        };

        builder.AddMcpClient([config]);
        return builder;
    }

    /// <summary>
    /// 미리 빌드된 system-harness 실행 파일로 MCP 서버를 등록합니다.
    /// </summary>
    /// <param name="builder">HiveServiceBuilder 인스턴스</param>
    /// <param name="executablePath">SystemHarness.Mcp 실행 파일 경로</param>
    /// <returns>빌더 인스턴스 (fluent chaining)</returns>
    public static IHiveServiceBuilder AddSystemHarness(
        this IHiveServiceBuilder builder,
        string executablePath)
    {
        return builder.AddSystemHarness(o => o.ExecutablePath = executablePath);
    }
}
