using System.Text;
using FileSorting.Sorting.Infrastructure.IO;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileSorting.Tests;

public sealed class AtomicFileWriterTests : IDisposable
{
    private readonly string _testDir;
    private readonly AtomicFileWriter _writer = new(NullLogger<AtomicFileWriter>.Instance);

    public AtomicFileWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "AtomicWriterTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public async Task WriteAsync_CreatesDirectoryIfNotExists()
    {
        var nestedDir = Path.Combine(_testDir, "nested", "sub", "dir");
        var filePath = Path.Combine(nestedDir, "output.txt");

        await _writer.WriteAsync(
            filePath,
            Encoding.UTF8,
            (writer, _) => { writer.WriteLine("test"); return Task.CompletedTask; },
            CancellationToken.None);

        Assert.True(File.Exists(filePath));
        var lines = await File.ReadAllLinesAsync(filePath);
        Assert.Single(lines);
        Assert.Equal("test", lines[0]);
    }

    [Fact]
    public async Task WriteAsync_CleansUpStagingOnFailure()
    {
        var filePath = Path.Combine(_testDir, "fail_output.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _writer.WriteAsync(
                filePath,
                Encoding.UTF8,
                (_, _) => throw new InvalidOperationException("test error"),
                CancellationToken.None));

        Assert.False(File.Exists(filePath));
        Assert.Empty(Directory.GetFiles(_testDir, "fail_output.txt.*.writing"));
    }

    [Fact]
    public async Task WriteAsync_Cancellation_CleansUpStagingFile()
    {
        var filePath = Path.Combine(_testDir, "cancel_output.txt");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _writer.WriteAsync(
                filePath,
                Encoding.UTF8,
                async (writer, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    await writer.WriteLineAsync("never written");
                },
                cts.Token));

        Assert.False(File.Exists(filePath));
        Assert.Empty(Directory.GetFiles(_testDir, "cancel_output.txt.*.writing"));
    }

    [Fact]
    public async Task CopyIntoPlaceAsync_CreatesCopy()
    {
        var sourcePath = Path.Combine(_testDir, "source.txt");
        var destPath = Path.Combine(_testDir, "dest.txt");

        await File.WriteAllTextAsync(sourcePath, "hello world");

        await _writer.CopyIntoPlaceAsync(sourcePath, destPath, CancellationToken.None);

        Assert.True(File.Exists(destPath));
        Assert.Equal("hello world", await File.ReadAllTextAsync(destPath));
    }

    [Fact]
    public async Task CopyIntoPlaceAsync_OverwritesExisting()
    {
        var sourcePath = Path.Combine(_testDir, "source2.txt");
        var destPath = Path.Combine(_testDir, "dest2.txt");

        await File.WriteAllTextAsync(destPath, "old content");
        await File.WriteAllTextAsync(sourcePath, "new content");

        await _writer.CopyIntoPlaceAsync(sourcePath, destPath, CancellationToken.None);

        Assert.Equal("new content", await File.ReadAllTextAsync(destPath));
    }
}
