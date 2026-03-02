using FluentAssertions;
using FluxIndex.Extensions.FileVault.Domain.Entities;
using FluxIndex.Extensions.FileVault.Domain.Enums;
using FluxIndex.Extensions.FileVault.Interfaces;
using IronHive.Flux.Rag.Tools;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;
using Xunit;

namespace IronHive.Flux.Tests.Rag;

public class FluxIndexStatusToolTests
{
    private readonly IVault _vault;
    private readonly FluxIndexStatusTool _tool;

    public FluxIndexStatusToolTests()
    {
        _vault = Substitute.For<IVault>();
        _tool = new FluxIndexStatusTool(_vault);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullVault_ShouldThrow()
    {
        var act = () => new FluxIndexStatusTool(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidVault_ShouldNotThrow()
    {
        var vault = Substitute.For<IVault>();
        var tool = new FluxIndexStatusTool(vault);
        tool.Should().NotBeNull();
    }

    #endregion

    #region GetStatusAsync

    [Fact]
    public async Task GetStatusAsync_ShouldReturnVaultStatus()
    {
        _vault.StatusAsync(Arg.Any<CancellationToken>()).Returns(new VaultStatus
        {
            TotalEntries = 10,
            SourceCount = 2,
            ExtractedCount = 3,
            MemorizedCount = 5,
            InSyncCount = 8,
            SourceModifiedCount = 1,
            ErrorCount = 1,
            QueuedCount = 2,
            ActiveWatcherCount = 1,
            TotalStorageSizeBytes = 52_428_800 // 50MB
        });

        var resultJson = await _tool.GetStatusAsync();
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("totalEntries").GetInt32().Should().Be(10);
        result.RootElement.GetProperty("memorizedCount").GetInt32().Should().Be(5);
        result.RootElement.GetProperty("errorCount").GetInt32().Should().Be(1);
        result.RootElement.GetProperty("queuedCount").GetInt32().Should().Be(2);
        result.RootElement.GetProperty("activeWatcherCount").GetInt32().Should().Be(1);
        result.RootElement.GetProperty("totalStorageSizeMb").GetDouble().Should().Be(50.0);
    }

    [Fact]
    public async Task GetStatusAsync_VaultThrows_ShouldReturnError()
    {
        _vault.StatusAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Database unreachable"));

        var resultJson = await _tool.GetStatusAsync();
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("Database unreachable");
    }

    [Fact]
    public async Task GetStatusAsync_ShouldCallVaultStatus()
    {
        _vault.StatusAsync(Arg.Any<CancellationToken>()).Returns(new VaultStatus());

        await _tool.GetStatusAsync();

        await _vault.Received(1).StatusAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetDocumentInfoAsync

    [Fact]
    public async Task GetDocumentInfoAsync_EntryExists_ShouldReturnInfo()
    {
        var entry = VaultEntry.Create("/test/file.pdf", "/tmp/.vault");
        _vault.GetAsync("/test/file.pdf", Arg.Any<CancellationToken>()).Returns(entry);

        var resultJson = await _tool.GetDocumentInfoAsync("/test/file.pdf");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("fileName").GetString().Should().Be("file.pdf");
        result.RootElement.GetProperty("stage").GetString().Should().Be("Source");
    }

    [Fact]
    public async Task GetDocumentInfoAsync_EntryNotFound_ShouldReturnError()
    {
        _vault.GetAsync("/missing/file.pdf", Arg.Any<CancellationToken>()).Returns((VaultEntry?)null);

        var resultJson = await _tool.GetDocumentInfoAsync("/missing/file.pdf");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("No entry found");
    }

    [Fact]
    public async Task GetDocumentInfoAsync_VaultThrows_ShouldReturnError()
    {
        _vault.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("DB error"));

        var resultJson = await _tool.GetDocumentInfoAsync("/test/file.pdf");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("DB error");
    }

    #endregion

    #region ListDocumentsAsync

    [Fact]
    public async Task ListDocumentsAsync_NoFilter_ShouldReturnAll()
    {
        var entries = new List<VaultEntry>
        {
            VaultEntry.Create("/test/a.pdf", "/tmp/.vault"),
            VaultEntry.Create("/test/b.txt", "/tmp/.vault")
        };
        _vault.ListAsync(null, Arg.Any<CancellationToken>()).Returns(entries);

        var resultJson = await _tool.ListDocumentsAsync();
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("totalCount").GetInt32().Should().Be(2);
        result.RootElement.GetProperty("stageFilter").GetString().Should().Be("all");
        result.RootElement.GetProperty("documents").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task ListDocumentsAsync_WithStageFilter_ShouldPassToVault()
    {
        _vault.ListAsync(ProcessingStage.Memorized, Arg.Any<CancellationToken>())
            .Returns(new List<VaultEntry>());

        await _tool.ListDocumentsAsync("Memorized");

        await _vault.Received(1).ListAsync(ProcessingStage.Memorized, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListDocumentsAsync_InvalidStageFilter_ShouldReturnError()
    {
        var resultJson = await _tool.ListDocumentsAsync("InvalidStage");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("Invalid stage filter");
    }

    [Fact]
    public async Task ListDocumentsAsync_CaseInsensitiveFilter_ShouldWork()
    {
        _vault.ListAsync(ProcessingStage.Extracted, Arg.Any<CancellationToken>())
            .Returns(new List<VaultEntry>());

        var resultJson = await _tool.ListDocumentsAsync("extracted");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ListDocumentsAsync_VaultThrows_ShouldReturnError()
    {
        _vault.ListAsync(Arg.Any<ProcessingStage?>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("List failed"));

        var resultJson = await _tool.ListDocumentsAsync();
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("List failed");
    }

    #endregion

    #region DetectChangesAsync

    [Fact]
    public async Task DetectChangesAsync_NoChanges_ShouldReportNone()
    {
        _vault.DetectChangesAsync("/test/file.pdf", Arg.Any<CancellationToken>())
            .Returns(new ChangeDetectionResult
            {
                FilePath = "/test/file.pdf",
                FileName = "file.pdf",
                EntryExists = true,
                SourceExists = true,
                SourceChanged = false,
                VaultChanged = false,
                RecommendedAction = ChangeAction.None,
                Stage = ProcessingStage.Memorized,
                ChunkCount = 12
            });

        var resultJson = await _tool.DetectChangesAsync("/test/file.pdf");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("hasChanges").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("recommendedAction").GetString().Should().Be("None");
        result.RootElement.GetProperty("chunkCount").GetInt32().Should().Be(12);
    }

    [Fact]
    public async Task DetectChangesAsync_SourceChanged_ShouldReportMemorize()
    {
        _vault.DetectChangesAsync("/test/file.pdf", Arg.Any<CancellationToken>())
            .Returns(new ChangeDetectionResult
            {
                FilePath = "/test/file.pdf",
                FileName = "file.pdf",
                EntryExists = true,
                SourceExists = true,
                SourceChanged = true,
                VaultChanged = false,
                RecommendedAction = ChangeAction.Memorize,
                FileSize = 1024,
                FileModifiedAt = DateTimeOffset.UtcNow
            });

        var resultJson = await _tool.DetectChangesAsync("/test/file.pdf");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("hasChanges").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("sourceChanged").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("recommendedAction").GetString().Should().Be("Memorize");
    }

    [Fact]
    public async Task DetectChangesAsync_NewFile_ShouldReportMemorize()
    {
        _vault.DetectChangesAsync("/test/newfile.pdf", Arg.Any<CancellationToken>())
            .Returns(new ChangeDetectionResult
            {
                FilePath = "/test/newfile.pdf",
                FileName = "newfile.pdf",
                EntryExists = false,
                SourceExists = true,
                SourceChanged = false,
                VaultChanged = false,
                RecommendedAction = ChangeAction.Memorize
            });

        var resultJson = await _tool.DetectChangesAsync("/test/newfile.pdf");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("entryExists").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("recommendedAction").GetString().Should().Be("Memorize");
    }

    [Fact]
    public async Task DetectChangesAsync_DeletedSource_ShouldReportRemove()
    {
        _vault.DetectChangesAsync("/test/deleted.pdf", Arg.Any<CancellationToken>())
            .Returns(new ChangeDetectionResult
            {
                FilePath = "/test/deleted.pdf",
                FileName = "deleted.pdf",
                EntryExists = true,
                SourceExists = false,
                RecommendedAction = ChangeAction.Remove
            });

        var resultJson = await _tool.DetectChangesAsync("/test/deleted.pdf");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("sourceExists").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("recommendedAction").GetString().Should().Be("Remove");
    }

    [Fact]
    public async Task DetectChangesAsync_VaultThrows_ShouldReturnError()
    {
        _vault.DetectChangesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Detection failed"));

        var resultJson = await _tool.DetectChangesAsync("/test/file.pdf");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("Detection failed");
    }

    #endregion
}
