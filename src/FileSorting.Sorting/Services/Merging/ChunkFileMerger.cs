using System.Text;
using FileSorting.Domain.Models;
using FileSorting.Sorting.Configuration;
using FileSorting.Sorting.Infrastructure.IO;
using Microsoft.Extensions.Logging;

namespace FileSorting.Sorting.Services.Merging;

internal sealed class ChunkFileMerger : IChunkFileMerger
{
    private const int MaxParallelMergeDegree = 4;

    private readonly SortingOptions _options;
    private readonly IFileWriter _fileWriter;
    private readonly ILogger<ChunkFileMerger> _logger;

    public ChunkFileMerger(
        SortingOptions options,
        IFileWriter fileWriter,
        ILogger<ChunkFileMerger> logger)
    {
        _options = options;
        _fileWriter = fileWriter;
        _logger = logger;
    }

    public async Task MergeAsync(
        IReadOnlyList<string> chunkFiles,
        string outputPath,
        string tempDirectory,
        CancellationToken cancellationToken)
    {
        var encoding = _options.Encoding;
        var current = new List<string>(chunkFiles);

        _logger.LogInformation("K-way merge (merge way: {Way})", _options.MergeWayCount);

        current = await RunIntermediatePassesAsync(current, tempDirectory, encoding, cancellationToken);

        await MergeFilesAsync(current, outputPath, encoding, cancellationToken);
        _logger.LogInformation("Merge complete: {Path}", outputPath);
    }

    private async Task<List<string>> RunIntermediatePassesAsync(
        List<string> current,
        string tempDirectory,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        var mergeWay = _options.MergeWayCount;
        var pass = 0;

        while (current.Count > mergeWay)
        {
            var batchCount = (current.Count + mergeWay - 1) / mergeWay;
            var newChunkFiles = new string?[batchCount];

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, MaxParallelMergeDegree),
                CancellationToken = cancellationToken
            };

            try
            {
                await Parallel.ForEachAsync(
                    Enumerable.Range(0, batchCount),
                    parallelOptions,
                    async (batchIndex, ct) =>
                    {
                        var start = batchIndex * mergeWay;
                        var end = Math.Min(start + mergeWay, current.Count);
                        var batch = current.GetRange(start, end - start);
                        var mergedPath = Path.Combine(tempDirectory, $"merged_p{pass}_{batchIndex:D6}.tmp");
                        await MergeFilesAsync(batch, mergedPath, encoding, ct);
                        newChunkFiles[batchIndex] = mergedPath;

                        foreach (var file in batch)
                            _fileWriter.TryDeleteFile(file);
                    });
            }
            catch
            {
                foreach (var chunkFile in newChunkFiles)
                {
                    if (!string.IsNullOrEmpty(chunkFile))
                        _fileWriter.TryDeleteFile(chunkFile);
                }

                throw;
            }

            pass++;
            current = newChunkFiles.Select(path => path!).ToList();
            _logger.LogInformation("Merge pass {Pass} complete: {Count} chunks remaining", pass, current.Count);
        }

        return current;
    }

    private int CalculateReaderBufferSize(int readerCount)
    {
        var totalBytes = (long)_options.MaxMergeBufferMb * 1024 * 1024;
        return (int)Math.Clamp(totalBytes / Math.Max(readerCount, 1), 4096, 1024 * 1024);
    }

    private async Task MergeFilesAsync(
        List<string> chunkFiles,
        string outputPath,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        var readerBufferSize = CalculateReaderBufferSize(chunkFiles.Count);
        var readers = OpenReaders(chunkFiles, encoding, readerBufferSize);
        try
        {
            var pq = await InitializePriorityQueueAsync(readers, cancellationToken);

            await _fileWriter.WriteAsync(
                outputPath,
                encoding,
                (writer, ct) => WriteMergedOutputAsync(pq, readers, writer, ct),
                cancellationToken);
        }
        finally
        {
            foreach (var reader in readers)
                reader.Dispose();
        }
    }

    private static List<StreamReader> OpenReaders(List<string> chunkFiles, Encoding encoding, int bufferSize)
    {
        var readers = new List<StreamReader>(chunkFiles.Count);
        try
        {
            foreach (var file in chunkFiles)
            {
                var fs = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                readers.Add(new StreamReader(fs, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: bufferSize));
            }

            return readers;
        }
        catch
        {
            foreach (var reader in readers)
            {
                reader.Dispose();
            }

            throw;
        }
    }

    private static async Task<PriorityQueue<(FileEntry Entry, int ReaderIndex), FileEntry>> InitializePriorityQueueAsync(
        List<StreamReader> readers,
        CancellationToken cancellationToken)
    {
        var pq = new PriorityQueue<(FileEntry Entry, int ReaderIndex), FileEntry>(readers.Count);

        for (var i = 0; i < readers.Count; i++)
        {
            var line = await readers[i].ReadLineAsync(cancellationToken);
            if (line != null)
            {
                var entry = FileEntryFactory.Create(line);
                pq.Enqueue((entry, i), entry);
            }
        }

        return pq;
    }

    private async Task WriteMergedOutputAsync(
        PriorityQueue<(FileEntry Entry, int ReaderIndex), FileEntry> pq,
        List<StreamReader> readers,
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        var pending = new Task<string?>?[readers.Count];
        long linesWritten = 0;

        while (pq.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (entry, readerIndex) = pq.Dequeue();
            entry.WriteTo(writer);
            linesWritten++;

            if (linesWritten % 1_000_000 == 0)
                _logger.LogInformation("Merge progress: {Lines:N0} lines written", linesWritten);

            pending[readerIndex] ??= readers[readerIndex].ReadLineAsync(cancellationToken).AsTask();

            var nextLine = await pending[readerIndex];
            pending[readerIndex] = null;

            if (nextLine != null)
            {
                var nextEntry = FileEntryFactory.Create(nextLine);
                pq.Enqueue((nextEntry, readerIndex), nextEntry);
                pending[readerIndex] = readers[readerIndex].ReadLineAsync(cancellationToken).AsTask();
            }
        }

        _logger.LogInformation("Merge write complete: {Lines:N0} total lines", linesWritten);
    }
}