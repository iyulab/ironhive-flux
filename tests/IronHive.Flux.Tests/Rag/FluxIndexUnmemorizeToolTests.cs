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

public class FluxIndexUnmemorizeToolTests
{
    private readonly IVault _vault;
    private readonly FluxIndexUnmemorizeTool _tool;

    public FluxIndexUnmemorizeToolTests()
    {
        _vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        _tool = new FluxIndexUnmemorizeTool(_vault, options);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullVault_ShouldThrow()
    {
        var options = Options.Create(new FluxRagToolsOptions());

        var act = () => new FluxIndexUnmemorizeTool(null!, options);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        var vault = Substitute.For<IVault>();

        var act = () => new FluxIndexUnmemorizeTool(vault, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UnmemorizeAsync — Successful Deletion

    [Fact]
    public async Task UnmemorizeAsync_ShouldReturnDeleted()
    {
        var filePath = "/docs/test.md";

        var resultJson = await _tool.UnmemorizeAsync(filePath);
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("deleted").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("filePath").GetString().Should().Be(filePath);
        result.RootElement.GetProperty("deletedAt").GetString().Should().NotBeNullOrEmpty();
        result.RootElement.GetProperty("message").GetString().Should().Contain("Successfully removed");
    }

    [Fact]
    public async Task UnmemorizeAsync_ShouldCallVaultRemove()
    {
        var filePath = "/docs/test.md";

        await _tool.UnmemorizeAsync(filePath);

        await _vault.Received(1).RemoveAsync(filePath, Arg.Any<CancellationToken>());
    }

    #endregion

    #region UnmemorizeAsync — Error Handling

    [Fact]
    public async Task UnmemorizeAsync_VaultThrows_ShouldReturnError()
    {
        _vault.RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Database error"));

        var resultJson = await _tool.UnmemorizeAsync("/docs/test.md");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("deleted").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("Database error");
    }

    [Fact]
    public async Task UnmemorizeAsync_FileNotInVault_VaultThrows_ShouldReturnError()
    {
        _vault.RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new KeyNotFoundException("Entry not found"));

        var resultJson = await _tool.UnmemorizeAsync("/non/existent.md");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("Entry not found");
    }

    #endregion
}
