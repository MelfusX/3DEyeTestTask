using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FileSorting.Domain.Models;
using FileSorting.Sorting.Configuration;
using FileSorting.Sorting.Infrastructure.IO;
using FileSorting.Sorting.Services.Merging;
using FileSorting.Sorting.Services.Splitting;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileSorting.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[RankColumn]
public class SortingBenchmarks
{
    public enum DatasetShape
    {
        Random,
        Sorted,
        Reverse,
        Duplicates
    }

    private string _testDir = null!;
    private string _inputPath = null!;
    private int _expectedLineCount;

    [Params(1, 10, 50)]
    public int FileSizeMb { get; set; }

    [Params(16, 64)]
    public int ChunkSizeMb { get; set; }

    [Params(2, 16)]
    public int MergeWay { get; set; }

    [Params(DatasetShape.Random, DatasetShape.Sorted, DatasetShape.Duplicates)]
    public DatasetShape Shape { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileSortBench_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _inputPath = Path.Combine(_testDir, "input.txt");

        _expectedLineCount = GenerateFile(_inputPath, FileSizeMb * 1024L * 1024L, Shape);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Benchmark]
    public async Task ExternalMergeSort()
    {
        var outputPath = Path.Combine(_testDir, $"output_{ChunkSizeMb}_{MergeWay}_{Shape}.txt");
        var tempDir = Path.Combine(_testDir, "temp_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(tempDir);

        var options = new SortingOptions(
            chunkSizeMb: ChunkSizeMb,
            mergeWayCount: MergeWay,
            maxMergeBufferMb: 64,
            tempDirectory: tempDir,
            encodingName: "utf-8");

        var fileWriter = new AtomicFileWriter(NullLogger<AtomicFileWriter>.Instance);
        var splitter = new ChunkFileSplitter(options, fileWriter, NullLogger<ChunkFileSplitter>.Instance);
        var splitResult = await splitter.SplitAsync(_inputPath, tempDir, CancellationToken.None);

        if (splitResult.ChunkFiles.Count > 1)
        {
            var merger = new ChunkFileMerger(options, fileWriter, NullLogger<ChunkFileMerger>.Instance);
            await merger.MergeAsync(splitResult.ChunkFiles, outputPath, tempDir, CancellationToken.None);
        }
        else if (splitResult.ChunkFiles.Count == 1)
        {
            File.Move(splitResult.ChunkFiles[0], outputPath, overwrite: true);
        }

        ValidateOutput(outputPath, _expectedLineCount);

        if (File.Exists(outputPath))
            File.Delete(outputPath);
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }

    private static int GenerateFile(string path, long targetSize, DatasetShape shape)
    {
        var random = new Random(42);
        var strings = new[]
        {
            "Apple", "Banana is yellow", "Cherry is the best", "Something something", "Orange",
            "Банан желтый", "樱桃最好", "😀 Emoji payload"
        };
        var lines = new List<string>();

        using var writer = new StreamWriter(path, false, Encoding.UTF8, 64 * 1024);
        long written = 0;
        while (written < targetSize)
        {
            var line = shape switch
            {
                DatasetShape.Sorted => $"{lines.Count + 1}. Apple_{lines.Count:D7}",
                DatasetShape.Reverse => $"{lines.Count + 1}. Zulu_{lines.Count:D7}",
                DatasetShape.Duplicates => $"{random.Next(1, 32)}. Duplicate_{random.Next(1, 8)}",
                _ => $"{random.Next(1, 100000)}. {strings[random.Next(strings.Length)]}"
            };

            lines.Add(line);
            written += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
        }

        if (shape == DatasetShape.Reverse)
            lines.Reverse();

        foreach (var line in lines)
            writer.WriteLine(line);

        return lines.Count;
    }

    private static void ValidateOutput(string outputPath, int expectedLineCount)
    {
        var lineCount = 0;
        FileEntry? previous = null;

        foreach (var line in File.ReadLines(outputPath))
        {
            var current = FileEntryFactory.Create(line);
            if (previous.HasValue)
                if (previous.Value.CompareTo(current) > 0)
                    throw new InvalidOperationException($"Benchmark produced unsorted output at line {lineCount}.");

            previous = current;
            lineCount++;
        }

        if (lineCount != expectedLineCount)
            throw new InvalidOperationException($"Benchmark output line count mismatch. Expected {expectedLineCount}, got {lineCount}.");
    }
}
