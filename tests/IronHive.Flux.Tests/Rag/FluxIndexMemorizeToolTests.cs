using FluentAssertions;
using FluxIndex.Extensions.FileVault.Interfaces;
using IronHive.Flux.Rag.Options;
using IronHive.Flux.Rag.Tools;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;
using Xunit;

namespace IronHive.Flux.Tests.Rag;

public class FluxIndexMemorizeToolTests
{
    private readonly IVault _vault;
    private readonly FluxIndexMemorizeTool _tool;

    public FluxIndexMemorizeToolTests()
    {
        _vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        _tool = new FluxIndexMemorizeTool(_vault, options);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullVault_ShouldThrow()
    {
        var options = Options.Create(new FluxRagToolsOptions());

        var act = () => new FluxIndexMemorizeTool(null!, options);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        var vault = Substitute.For<IVault>();

        var act = () => new FluxIndexMemorizeTool(vault, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidArgs_ShouldNotThrow()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        var tool = new FluxIndexMemorizeTool(vault, options);
        tool.Should().NotBeNull();
    }

    #endregion

    #region MemorizeAsync — Success

    [Fact]
    public async Task MemorizeAsync_WithExistingFile_ShouldReturnSuccess()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "Hello world content");

            var resultJson = await _tool.MemorizeAsync(tempFile);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
            result.RootElement.GetProperty("filePath").GetString().Should().Be(tempFile);
            result.RootElement.GetProperty("message").GetString().Should().Contain("Successfully memorized");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task MemorizeAsync_ShouldCallVaultMemorize()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await _tool.MemorizeAsync(tempFile);

            await _vault.Received(1).MemorizeAsync(tempFile, Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region MemorizeAsync — File Not Found

    [Fact]
    public async Task MemorizeAsync_WithNonExistentFile_ShouldReturnError()
    {
        var resultJson = await _tool.MemorizeAsync("/non/existent/file.txt");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("File not found");
    }

    [Fact]
    public async Task MemorizeAsync_WithNonExistentFile_ShouldNotCallVault()
    {
        await _tool.MemorizeAsync("/non/existent/file.txt");

        await _vault.DidNotReceive().MemorizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region MemorizeAsync — Error Handling

    [Fact]
    public async Task MemorizeAsync_VaultThrows_ShouldReturnError()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _vault.MemorizeAsync(tempFile, Arg.Any<CancellationToken>())
                .Throws(new InvalidOperationException("Extraction failed"));

            var resultJson = await _tool.MemorizeAsync(tempFile);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            result.RootElement.GetProperty("error").GetString().Should().Contain("Extraction failed");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion
}
