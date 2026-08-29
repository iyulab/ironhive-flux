using AwesomeAssertions;
using FluxFeed.Domain.Entities;
using FluxFeed.Interfaces;
using IronHive.Flux.Rag.Options;
using IronHive.Flux.Rag.Tools;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;
using Xunit;

namespace IronHive.Flux.Tests.Rag;

public class FluxIndexBatchMemorizeToolTests
{
    private readonly IVault _vault;
    private readonly FluxIndexBatchMemorizeTool _tool;

    public FluxIndexBatchMemorizeToolTests()
    {
        _vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        _tool = new FluxIndexBatchMemorizeTool(_vault, options);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullVault_ShouldThrow()
    {
        var options = Options.Create(new FluxRagToolsOptions());

        var act = () => new FluxIndexBatchMemorizeTool(null!, options);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        var vault = Substitute.For<IVault>();

        var act = () => new FluxIndexBatchMemorizeTool(vault, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidArgs_ShouldNotThrow()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        var tool = new FluxIndexBatchMemorizeTool(vault, options);
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
            RejectSymlinks = false
        });

        var tool = new FluxIndexBatchMemorizeTool(vault, options, security);
        tool.Should().NotBeNull();
    }

    #endregion

    #region MemorizeDocumentsAsync — Success

    [Fact]
    public async Task MemorizeDocumentsAsync_WithExistingFiles_ShouldReturnAllSucceeded()
    {
        var tempFiles = CreateTempFiles(3);
        try
        {
            var resultJson = await _tool.MemorizeDocumentsAsync(tempFiles, cancellationToken: TestContext.Current.CancellationToken);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
            result.RootElement.GetProperty("succeededCount").GetInt32().Should().Be(3);
            result.RootElement.GetProperty("failedCount").GetInt32().Should().Be(0);
            result.RootElement.GetProperty("totalRequested").GetInt32().Should().Be(3);
        }
        finally
        {
            DeleteTempFiles(tempFiles);
        }
    }

    [Fact]
    public async Task MemorizeDocumentsAsync_ShouldCallVaultForEachFile()
    {
        var tempFiles = CreateTempFiles(2);
        try
        {
            await _tool.MemorizeDocumentsAsync(tempFiles, cancellationToken: TestContext.Current.CancellationToken);

            foreach (var file in tempFiles)
            {
                await _vault.Received().MemorizeAsync(file, Arg.Any<CancellationToken>());
            }
        }
        finally
        {
            DeleteTempFiles(tempFiles);
        }
    }

    #endregion

    #region MemorizeDocumentsAsync — Empty/Overflow

    [Fact]
    public async Task MemorizeDocumentsAsync_EmptyList_ShouldReturnError()
    {
        var resultJson = await _tool.MemorizeDocumentsAsync(new List<string>(), cancellationToken: TestContext.Current.CancellationToken);
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("No file paths");
    }

    [Fact]
    public async Task MemorizeDocumentsAsync_ExceedsMaxBatchSize_ShouldReturnError()
    {
        var tooMany = Enumerable.Range(0, FluxIndexBatchMemorizeTool.MaxBatchSize + 1)
            .Select(i => $"/fake/file{i}.txt")
            .ToList();

        var resultJson = await _tool.MemorizeDocumentsAsync(tooMany, cancellationToken: TestContext.Current.CancellationToken);
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("exceeds maximum");
    }

    #endregion

    #region MemorizeDocumentsAsync — Partial Failures

    [Fact]
    public async Task MemorizeDocumentsAsync_MixedExistingAndMissing_ShouldReportPartialSuccess()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "Content");
        try
        {
            var files = new List<string> { tempFile, "/definitely/missing/file.txt" };

            var resultJson = await _tool.MemorizeDocumentsAsync(files, cancellationToken: TestContext.Current.CancellationToken);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            result.RootElement.GetProperty("succeededCount").GetInt32().Should().Be(1);
            result.RootElement.GetProperty("failedCount").GetInt32().Should().Be(1);
            result.RootElement.GetProperty("message").GetString().Should().Contain("1/2");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task MemorizeDocumentsAsync_VaultThrowsForOneFile_ShouldReportPartialSuccess()
    {
        var tempFiles = CreateTempFiles(2);
        try
        {
            _vault.MemorizeAsync(tempFiles[1], Arg.Any<CancellationToken>())
                .Throws(new InvalidOperationException("Pipeline error"));

            var resultJson = await _tool.MemorizeDocumentsAsync(tempFiles, cancellationToken: TestContext.Current.CancellationToken);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            result.RootElement.GetProperty("succeededCount").GetInt32().Should().Be(1);
            result.RootElement.GetProperty("failedCount").GetInt32().Should().Be(1);
        }
        finally
        {
            DeleteTempFiles(tempFiles);
        }
    }

    #endregion

    #region MemorizeDocumentsAsync — Concurrency

    [Fact]
    public async Task MemorizeDocumentsAsync_MaxConcurrentIsClamped()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "OK");
        try
        {
            // Should not throw with extreme values
            var resultJson = await _tool.MemorizeDocumentsAsync(new List<string> { tempFile }, maxConcurrent: 0, cancellationToken: TestContext.Current.CancellationToken);
            var result = JsonDocument.Parse(resultJson);
            result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

            resultJson = await _tool.MemorizeDocumentsAsync(new List<string> { tempFile }, maxConcurrent: 999, cancellationToken: TestContext.Current.CancellationToken);
            result = JsonDocument.Parse(resultJson);
            result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region MemorizeDirectoryAsync — Success

    [Fact]
    public async Task MemorizeDirectoryAsync_WithExistingDirectory_ShouldMemorizeFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"flux-batch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "a.txt"), "AAA");
            File.WriteAllText(Path.Combine(tempDir, "b.txt"), "BBB");

            var resultJson = await _tool.MemorizeDirectoryAsync(tempDir, "*.txt", cancellationToken: TestContext.Current.CancellationToken);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
            result.RootElement.GetProperty("totalFound").GetInt32().Should().Be(2);
            result.RootElement.GetProperty("succeededCount").GetInt32().Should().Be(2);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MemorizeDirectoryAsync_DirectoryNotFound_ShouldReturnError()
    {
        var resultJson = await _tool.MemorizeDirectoryAsync("/non/existent/directory", cancellationToken: TestContext.Current.CancellationToken);
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("Directory not found");
    }

    [Fact]
    public async Task MemorizeDirectoryAsync_EmptyDirectory_ShouldReturnSuccessWithZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"flux-batch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var resultJson = await _tool.MemorizeDirectoryAsync(tempDir, "*.pdf", cancellationToken: TestContext.Current.CancellationToken);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
            result.RootElement.GetProperty("totalFound").GetInt32().Should().Be(0);
            result.RootElement.GetProperty("message").GetString().Should().Contain("No matching files");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MemorizeDirectoryAsync_NonRecursive_ShouldNotIncludeSubdirs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"flux-batch-test-{Guid.NewGuid():N}");
        var subDir = Path.Combine(tempDir, "sub");
        Directory.CreateDirectory(subDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "root.txt"), "ROOT");
            File.WriteAllText(Path.Combine(subDir, "child.txt"), "CHILD");

            var resultJson = await _tool.MemorizeDirectoryAsync(tempDir, "*.txt", recursive: false, cancellationToken: TestContext.Current.CancellationToken);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("totalFound").GetInt32().Should().Be(1);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MemorizeDirectoryAsync_Recursive_ShouldIncludeSubdirs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"flux-batch-test-{Guid.NewGuid():N}");
        var subDir = Path.Combine(tempDir, "sub");
        Directory.CreateDirectory(subDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "root.txt"), "ROOT");
            File.WriteAllText(Path.Combine(subDir, "child.txt"), "CHILD");

            var resultJson = await _tool.MemorizeDirectoryAsync(tempDir, "*.txt", recursive: true, cancellationToken: TestContext.Current.CancellationToken);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("totalFound").GetInt32().Should().Be(2);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region Security Validation

    [Fact]
    public async Task MemorizeDocumentsAsync_FileOutsideAllowedPaths_ShouldFail()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "Content");
        try
        {
            var vault = Substitute.For<IVault>();
            var options = Options.Create(new FluxRagToolsOptions());
            var security = Options.Create(new VaultSecurityOptions
            {
                AllowedBasePaths = ["/allowed/only"],
                RejectSymlinks = false
            });
            var tool = new FluxIndexBatchMemorizeTool(vault, options, security);

            var resultJson = await tool.MemorizeDocumentsAsync(new List<string> { tempFile }, cancellationToken: TestContext.Current.CancellationToken);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            result.RootElement.GetProperty("failedCount").GetInt32().Should().Be(1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task MemorizeDocumentsAsync_FileTooLarge_ShouldFail()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "Content that exceeds tiny limit");
        try
        {
            var vault = Substitute.For<IVault>();
            var options = Options.Create(new FluxRagToolsOptions());
            var security = Options.Create(new VaultSecurityOptions
            {
                MaxFileSizeBytes = 5, // 5 bytes
                RejectSymlinks = false
            });
            var tool = new FluxIndexBatchMemorizeTool(vault, options, security);

            var resultJson = await tool.MemorizeDocumentsAsync(new List<string> { tempFile }, cancellationToken: TestContext.Current.CancellationToken);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            result.RootElement.GetProperty("failedCount").GetInt32().Should().Be(1);

            await vault.DidNotReceive().MemorizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ValidateFileSecurity_InvalidPath_ShouldReturnError()
    {
        var error = _tool.ValidateFileSecurity("file\0path.txt");
        error.Should().Contain("Invalid file path");
    }

    [Fact]
    public void ValidateFileSecurity_NonExistentFile_ShouldReturnNull()
    {
        var error = _tool.ValidateFileSecurity("/definitely/not/a/real/file.txt");
        error.Should().BeNull();
    }

    #endregion

    #region Helpers

    private static List<string> CreateTempFiles(int count)
    {
        var files = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, $"Content {i}");
            files.Add(path);
        }
        return files;
    }

    private static void DeleteTempFiles(List<string> files)
    {
        foreach (var file in files)
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    #endregion
}
