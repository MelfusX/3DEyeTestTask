using System.Text;
using Microsoft.Extensions.Logging;

namespace FileSorting.Sorting.Infrastructure.IO;

internal sealed class AtomicFileWriter : IFileWriter
{
    public const int DefaultBufferSize = 64 * 1024;

    private readonly ILogger<AtomicFileWriter> _logger;

    public AtomicFileWriter(ILogger<AtomicFileWriter> logger)
    {
        _logger = logger;
    }

    public async Task WriteAsync(
        string destinationPath,
        Encoding encoding,
        Func<StreamWriter, CancellationToken, Task> writeAction,
        CancellationToken cancellationToken)
    {
        EnsureDirectory(destinationPath);

        var stagingPath = CreateStagingPath(destinationPath);
        try
        {
            await using var stream = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                DefaultBufferSize,
                FileOptions.Asynchronous);

            await using var writer = new StreamWriter(stream, encoding, DefaultBufferSize);

            await writeAction(writer, cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
        catch
        {
            DeleteIfExists(stagingPath);
            throw;
        }

        File.Move(stagingPath, destinationPath, overwrite: true);
    }

    public async Task CopyIntoPlaceAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        EnsureDirectory(destinationPath);

        var stagingPath = CreateStagingPath(destinationPath);
        try
        {
            await using var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                DefaultBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using var destination = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                DefaultBufferSize,
                FileOptions.Asynchronous);

            await source.CopyToAsync(destination, DefaultBufferSize, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }
        catch
        {
            DeleteIfExists(stagingPath);
            throw;
        }

        File.Move(stagingPath, destinationPath, overwrite: true);
    }

    public void TryDeleteFile(string path)
    {
        try
        {
            DeleteIfExists(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file: {Path}", path);
        }
    }

    private static string CreateStagingPath(string destinationPath) => $"{destinationPath}.{Guid.NewGuid():N}.writing";

    private static void EnsureDirectory(string destinationPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
