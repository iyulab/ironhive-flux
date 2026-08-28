using AwesomeAssertions;
using FluxFeed.Interfaces;
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

    [Fact]
    public void Constructor_WithSecurityOptions_ShouldNotThrow()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        var security = Options.Create(new VaultSecurityOptions
        {
            MaxFileSizeBytes = 50 * 1024 * 1024,
            RejectSymlinks = true
        });

        var tool = new FluxIndexMemorizeTool(vault, options, security);
        tool.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullSecurityOptions_ShouldUseDefaults()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());

        var tool = new FluxIndexMemorizeTool(vault, options, securityOptions: null);
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

    #region Security — File Size Validation

    [Fact]
    public async Task MemorizeAsync_FileTooLarge_ShouldReject()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Create a file and use a very small MaxFileSizeBytes limit
            await File.WriteAllTextAsync(tempFile, "Some content that is within a tiny limit");

            var vault = Substitute.For<IVault>();
            var options = Options.Create(new FluxRagToolsOptions());
            var security = Options.Create(new VaultSecurityOptions
            {
                MaxFileSizeBytes = 10, // 10 bytes - smaller than content
                RejectSymlinks = false
            });
            var tool = new FluxIndexMemorizeTool(vault, options, security);

            var resultJson = await tool.MemorizeAsync(tempFile);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            result.RootElement.GetProperty("error").GetString().Should().Contain("exceeds maximum allowed size");

            await vault.DidNotReceive().MemorizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task MemorizeAsync_FileWithinSizeLimit_ShouldSucceed()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "OK");

            var vault = Substitute.For<IVault>();
            var options = Options.Create(new FluxRagToolsOptions());
            var security = Options.Create(new VaultSecurityOptions
            {
                MaxFileSizeBytes = 1024 * 1024, // 1MB
                RejectSymlinks = false
            });
            var tool = new FluxIndexMemorizeTool(vault, options, security);

            var resultJson = await tool.MemorizeAsync(tempFile);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region Security — AllowedBasePaths ACL

    [Fact]
    public async Task MemorizeAsync_FileOutsideAllowedPaths_ShouldReject()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "Content");

            var vault = Substitute.For<IVault>();
            var options = Options.Create(new FluxRagToolsOptions());
            var security = Options.Create(new VaultSecurityOptions
            {
                AllowedBasePaths = ["/allowed/path/only"],
                RejectSymlinks = false
            });
            var tool = new FluxIndexMemorizeTool(vault, options, security);

            var resultJson = await tool.MemorizeAsync(tempFile);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            result.RootElement.GetProperty("error").GetString().Should().Contain("not within any allowed base path");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task MemorizeAsync_FileInsideAllowedPath_ShouldSucceed()
    {
        var tempFile = Path.GetTempFileName();
        var tempDir = Path.GetDirectoryName(tempFile)!;
        try
        {
            await File.WriteAllTextAsync(tempFile, "Content");

            var vault = Substitute.For<IVault>();
            var options = Options.Create(new FluxRagToolsOptions());
            var security = Options.Create(new VaultSecurityOptions
            {
                AllowedBasePaths = [tempDir],
                RejectSymlinks = false
            });
            var tool = new FluxIndexMemorizeTool(vault, options, security);

            var resultJson = await tool.MemorizeAsync(tempFile);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task MemorizeAsync_EmptyAllowedPaths_ShouldAllowAll()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "Content");

            var vault = Substitute.For<IVault>();
            var options = Options.Create(new FluxRagToolsOptions());
            var security = Options.Create(new VaultSecurityOptions
            {
                AllowedBasePaths = [], // empty = no restriction
                RejectSymlinks = false
            });
            var tool = new FluxIndexMemorizeTool(vault, options, security);

            var resultJson = await tool.MemorizeAsync(tempFile);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region Security — Path Validation

    [Fact]
    public void ValidateFileSecurity_InvalidPath_ShouldReturnError()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        var security = Options.Create(new VaultSecurityOptions());
        var tool = new FluxIndexMemorizeTool(vault, options, security);

        // Paths with null chars are invalid
        var error = tool.ValidateFileSecurity("file\0path.txt");

        error.Should().Contain("Invalid file path");
    }

    [Fact]
    public void ValidateFileSecurity_NonExistentFile_ShouldReturnNull()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        var security = Options.Create(new VaultSecurityOptions());
        var tool = new FluxIndexMemorizeTool(vault, options, security);

        // Non-existent file should return null (let caller handle file-not-found)
        var error = tool.ValidateFileSecurity("/definitely/not/a/real/file.txt");

        error.Should().BeNull();
    }

    [Fact]
    public void ValidateFileSecurity_ValidFile_ShouldReturnNull()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "OK");

            var vault = Substitute.For<IVault>();
            var options = Options.Create(new FluxRagToolsOptions());
            var security = Options.Create(new VaultSecurityOptions
            {
                RejectSymlinks = false
            });
            var tool = new FluxIndexMemorizeTool(vault, options, security);

            var error = tool.ValidateFileSecurity(tempFile);
            error.Should().BeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region VaultSecurityOptions Defaults

    [Fact]
    public void VaultSecurityOptions_DefaultValues_ShouldBeReasonable()
    {
        var opts = new VaultSecurityOptions();

        opts.MaxFileSizeBytes.Should().Be(100 * 1024 * 1024); // 100MB
        opts.AllowedBasePaths.Should().BeEmpty();
        opts.RejectSymlinks.Should().BeTrue();
    }

    #endregion
}
