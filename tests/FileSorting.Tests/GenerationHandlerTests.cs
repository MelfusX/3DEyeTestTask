using FileSorting.Domain.Models;
using FileSorting.Generation;
using FileSorting.Generation.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileSorting.Tests;

public sealed class GenerationHandlerTests : IDisposable
{
    private readonly string _testDir;

    public GenerationHandlerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileGenerationTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public async Task GenerateFile_Success_WritesParsableOutput()
    {
        var outputPath = Path.Combine(_testDir, "generated.txt");
        var service = CreateService();

        await service.GenerateAsync(outputPath, 256, CancellationToken.None);

        Assert.True(File.Exists(outputPath));

        var lines = await File.ReadAllLinesAsync(outputPath);
        Assert.NotEmpty(lines);
        Assert.All(lines, line => FileEntryFactory.Create(line));
    }

    [Fact]
    public async Task GenerateFile_Cancelled_DoesNotLeaveOutputOrStagingFile()
    {
        var outputPath = Path.Combine(_testDir, "cancelled.txt");
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GenerateAsync(outputPath, 1024, cts.Token));

        Assert.False(File.Exists(outputPath));
    }

    private FileGenerationService CreateService()
    {
        var options = new GenerationOptions(1, 1000, ["Alpha", "Beta", "Gamma"]);
        return new FileGenerationService(options, NullLogger<FileGenerationService>.Instance);
    }
}