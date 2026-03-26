using System.Text;
using FileSorting.Domain.Models;
using FileSorting.Sorting.Configuration;
using FileSorting.Sorting.Infrastructure.IO;
using Microsoft.Extensions.Logging;

namespace FileSorting.Sorting.Services.Splitting;

internal sealed class ChunkFileSplitter : IChunkFileSplitter
{
    private readonly SortingOptions _options;
    private readonly IFileWriter _fileWriter;
    private readonly ILogger<ChunkFileSplitter> _logger;

    public ChunkFileSplitter(
        SortingOptions options,
        IFileWriter fileWriter,
        ILogger<ChunkFileSplitter> logger)
    {
        _options = options;
        _fileWriter = fileWriter;
        _logger = logger;
    }

    public async Task<SplitFileResult> SplitAsync(
        string inputPath,
        string tempDirectory,
        CancellationToken cancellationToken)
    {
        var encoding = _options.Encoding;
        _logger.LogInformation("Splitting into sorted chunks (chunk size: {Size}MB)", _options.ChunkSizeMb);

        var chunkFiles = new List<string>();
        try
        {
            using var reader = OpenInputReader(inputPath, encoding);
            await ReadAndSplitAsync(reader, encoding, tempDirectory, chunkFiles, cancellationToken);
        }
        catch
        {
            foreach (var chunkFile in chunkFiles)
                _fileWriter.TryDeleteFile(chunkFile);

            throw;
        }

        _logger.LogInformation("Split complete: {Count} chunks created", chunkFiles.Count);
        return new SplitFileResult(chunkFiles);
    }

    private static StreamReader OpenInputReader(string inputPath, Encoding encoding)
    {
        var fileStream = new FileStream(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            AtomicFileWriter.DefaultBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return new StreamReader(
            fileStream,
            encoding,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: AtomicFileWriter.DefaultBufferSize,
            leaveOpen: false);
    }

    private async Task ReadAndSplitAsync(
        StreamReader reader,
        Encoding encoding,
        string tempDirectory,
        List<string> chunkFiles,
        CancellationToken cancellationToken)
    {
        var chunkSizeBytes = (long)_options.ChunkSizeMb * 1024 * 1024;
        var estimatedEntriesPerChunk = (int)Math.Clamp(chunkSizeBytes / 30, 1, int.MaxValue);
        var newLineByteCount = encoding.GetByteCount(Environment.NewLine);

        var entries = new List<FileEntry>(estimatedEntriesPerChunk);
        long currentChunkSize = 0;
        var chunkIndex = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line == null)
            {
                if (entries.Count > 0)
                {
                    chunkFiles.Add(await WriteChunkAsync(
                        entries, chunkIndex, tempDirectory, encoding, cancellationToken));
                }

                break;
            }

            var lineByteSize = encoding.GetByteCount(line) + newLineByteCount;

            if (currentChunkSize + lineByteSize > chunkSizeBytes && entries.Count > 0)
            {
                chunkFiles.Add(await WriteChunkAsync(
                    entries, chunkIndex, tempDirectory, encoding, cancellationToken));
                chunkIndex++;

                entries = new List<FileEntry>(estimatedEntriesPerChunk);
                currentChunkSize = 0;
            }

            entries.Add(FileEntryFactory.Create(line));
            currentChunkSize += lineByteSize;
        }
    }

    private async Task<string> WriteChunkAsync(
        List<FileEntry> entries,
        int chunkIndex,
        string tempDirectory,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        var chunkPath = Path.Combine(tempDirectory, $"chunk_{chunkIndex:D6}.tmp");

        entries.Sort();
        await _fileWriter.WriteAsync(
            chunkPath,
            encoding,
            (writer, ct) => WriteEntriesAsync(entries, writer, ct),
            cancellationToken);

        _logger.LogDebug("Chunk {Index} written: {Count} entries", chunkIndex, entries.Count);
        
        return chunkPath;
    }

    private static Task WriteEntriesAsync(
        List<FileEntry> entries,
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entry.WriteTo(writer);
        }

        return Task.CompletedTask;
    }
}