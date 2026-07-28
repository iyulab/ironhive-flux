using IronHive.Plugins.MCP.Configurations;
using IronHive.Tools.SystemHarness;

namespace IronHive.Plugins.MCP;

/// <summary>
/// system-harness MCP 서버를 IronHive에 연결하는 확장 메서드.
/// </summary>
/// <remarks>
/// 확장 대상이 <see cref="McpClientManager"/>인 이유: MCP 세션은 연결 수명을 가지며 매니저가 그것을 소유한다.
/// 호출자가 매니저를 살려 두는 한 세션이 유지되고, 매니저가 사라지면 세션도 함께 정리된다.
/// (0.6.2 시절의 <c>IHiveServiceBuilder.AddMcpClient</c>는 0.15.0에서 사라졌다 — 도구 합성이 hive 빌더에서 분리됐다.)
/// </remarks>
public static class SystemHarnessMcpExtensions
{
    /// <summary>
    /// system-harness MCP 서버를 등록합니다.
    /// 174개 명령(파일, 앱, 화면, 키보드, OCR 등)이 help/do/get 3-tool dispatch로 제공되며,
    /// 연결이 완료되면 매니저가 생성될 때 받은 <c>IToolCollection</c>에 도구가 채워집니다.
    /// </summary>
    /// <param name="manager">MCP 클라이언트 매니저.</param>
    /// <param name="configure">옵션 구성 액션.</param>
    /// <returns>매니저 인스턴스 (fluent chaining).</returns>
    public static McpClientManager AddSystemHarness(
        this McpClientManager manager,
        Action<SystemHarnessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var options = new SystemHarnessOptions();
        configure?.Invoke(options);

        manager.AddOrUpdate(new McpStdioClientConfig
        {
            ServerName = options.ServerName,
            Command = options.BuildCommand(),
            Arguments = options.BuildArguments().ToArray(),
            WorkingDirectory = options.WorkingDirectory,
            EnvironmentVariables = options.BuildEnvironmentVariables(),
            ShutdownTimeout = options.ShutdownTimeout
        });

        return manager;
    }

    /// <summary>
    /// 미리 빌드된 system-harness 실행 파일로 MCP 서버를 등록합니다.
    /// </summary>
    /// <param name="manager">MCP 클라이언트 매니저.</param>
    /// <param name="executablePath">SystemHarness.Mcp 실행 파일 경로.</param>
    /// <returns>매니저 인스턴스 (fluent chaining).</returns>
    public static McpClientManager AddSystemHarness(
        this McpClientManager manager,
        string executablePath)
        => manager.AddSystemHarness(o => o.ExecutablePath = executablePath);
}
