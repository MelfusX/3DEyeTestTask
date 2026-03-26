using System.Text;
using FileSorting.Domain.Models;
using FileSorting.Sorting;
using FileSorting.Sorting.Configuration;
using FileSorting.Sorting.Infrastructure.IO;
using FileSorting.Sorting.Services.Merging;
using FileSorting.Sorting.Services.Splitting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileSorting.Tests;

public class SortingHandlerTests : IDisposable
{
    private static readonly string[] LargeTextPool =
    [
        "Alpha_" + new string('A', 48),
        "Beta_" + new string('B', 48),
        "Gamma_" + new string('C', 48),
        "Delta_" + new string('D', 48),
        "Epsilon_" + new string('E', 48)
    ];

    private readonly string _testDir;

    public SortingHandlerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileSortingTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private SortingOptions CreateOptions(int chunkSizeMb = 1, int mergeWay = 4, string? tempDirectory = null)
    {
        return new SortingOptions(
            chunkSizeMb: chunkSizeMb,
            mergeWayCount: mergeWay,
            maxMergeBufferMb: 64,
            tempDirectory: tempDirectory ?? Path.Combine(_testDir, "temp"),
            encodingName: "utf-8");
    }

    private async Task SortViaDiAsync(
        string inputPath,
        string outputPath,
        int chunkSizeMb = 1,
        int mergeWay = 4,
        string? tempDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(CreateOptions(chunkSizeMb, mergeWay, tempDirectory));
        services.AddSingleton<IFileWriter, AtomicFileWriter>();
        services.AddSingleton<FileSorting.Sorting.Infrastructure.TempWorkspaces.ITemporaryWorkspaceFactory, FileSorting.Sorting.Infrastructure.TempWorkspaces.TemporaryWorkspaceFactory>();
        services.AddSingleton<IChunkFileSplitter, ChunkFileSplitter>();
        services.AddSingleton<IChunkFileMerger, ChunkFileMerger>();
        services.AddSingleton<FileSortingService>();

        await using var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetRequiredService<FileSortingService>();

        await service.SortAsync(inputPath, outputPath, cancellationToken);
    }

    private static void AssertSortedLines(IReadOnlyList<string> lines)
    {
        for (var i = 1; i < lines.Count; i++)
        {
            var previous = FileEntryFactory.Create(lines[i - 1]);
            var current = FileEntryFactory.Create(lines[i]);

            Assert.True(previous.CompareTo(current) <= 0,
                $"Lines {i - 1} and {i} are not sorted: '{lines[i - 1]}' > '{lines[i]}'");
        }
    }

    private static async Task WriteLargeInputAsync(string path, int lineCount, int seed)
    {
        var random = new Random(seed);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous);
        await using var writer = new StreamWriter(stream);

        for (var i = 0; i < lineCount; i++)
        {
            var text = LargeTextPool[random.Next(LargeTextPool.Length)];
            await writer.WriteLineAsync($"{random.Next(1, 100000)}. {text}");
        }
    }

    private async Task SortFileAsync(string inputPath, string outputPath, int chunkSizeMb = 1, int mergeWay = 4)
    {
        var options = CreateOptions(chunkSizeMb: chunkSizeMb, mergeWay: mergeWay);
        var tempDir = Path.Combine(_testDir, "sort_temp_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(tempDir);

        var fileWriter = new AtomicFileWriter(NullLogger<AtomicFileWriter>.Instance);
        var splitter = new ChunkFileSplitter(options, fileWriter, NullLogger<ChunkFileSplitter>.Instance);
        var splitResult = await splitter.SplitAsync(inputPath, tempDir, CancellationToken.None);

        if (splitResult.ChunkFiles.Count == 0)
        {
            await File.WriteAllTextAsync(outputPath, string.Empty);
            return;
        }

        if (splitResult.ChunkFiles.Count == 1)
        {
            File.Move(splitResult.ChunkFiles[0], outputPath, overwrite: true);
            return;
        }

        var merger = new ChunkFileMerger(options, fileWriter, NullLogger<ChunkFileMerger>.Instance);
        await merger.MergeAsync(splitResult.ChunkFiles, outputPath, tempDir, CancellationToken.None);
    }

    [Fact]
    public async Task Sort_SmallFile_ProducesCorrectOutput()
    {
        var inputPath = Path.Combine(_testDir, "input.txt");
        var outputPath = Path.Combine(_testDir, "output.txt");

        var lines = new[]
        {
            "415. Apple",
            "30432. Something something something",
            "1. Apple",
            "32. Cherry is the best",
            "2. Banana is yellow"
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        await SortFileAsync(inputPath, outputPath);

        var result = await File.ReadAllLinesAsync(outputPath);

        Assert.Equal(5, result.Length);
        Assert.Equal("1. Apple", result[0]);
        Assert.Equal("415. Apple", result[1]);
        Assert.Equal("2. Banana is yellow", result[2]);
        Assert.Equal("32. Cherry is the best", result[3]);
        Assert.Equal("30432. Something something something", result[4]);
    }

    [Fact]
    public async Task Sort_EmptyFile_ProducesEmptyOutput()
    {
        var inputPath = Path.Combine(_testDir, "empty.txt");
        var outputPath = Path.Combine(_testDir, "empty_out.txt");

        await File.WriteAllTextAsync(inputPath, string.Empty);
        await SortFileAsync(inputPath, outputPath);

        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Empty(content);
    }

    [Fact]
    public async Task Sort_SingleLine_ProducesCorrectOutput()
    {
        var inputPath = Path.Combine(_testDir, "single.txt");
        var outputPath = Path.Combine(_testDir, "single_out.txt");

        await File.WriteAllLinesAsync(inputPath, ["42. Answer"]);
        await SortFileAsync(inputPath, outputPath);

        var result = await File.ReadAllLinesAsync(outputPath);
        Assert.Single(result);
        Assert.Equal("42. Answer", result[0]);
    }

    [Fact]
    public async Task Sort_MultiChunk_ProducesCorrectOutput()
    {
        var inputPath = Path.Combine(_testDir, "multi.txt");
        var outputPath = Path.Combine(_testDir, "multi_out.txt");

        await WriteLargeInputAsync(inputPath, lineCount: 80_000, seed: 42);

        var splitter = new ChunkFileSplitter(CreateOptions(), new AtomicFileWriter(NullLogger<AtomicFileWriter>.Instance), NullLogger<ChunkFileSplitter>.Instance);
        var tempDir = Path.Combine(_testDir, "multi_temp");
        Directory.CreateDirectory(tempDir);

        var splitResult = await splitter.SplitAsync(inputPath, tempDir, CancellationToken.None);
        Assert.True(splitResult.ChunkFiles.Count > 1, "Input should span multiple chunks for this test.");

        await SortFileAsync(inputPath, outputPath);

        var result = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(80_000, result.Length);
        AssertSortedLines(result);
    }

    [Fact]
    public async Task Sort_DuplicateEntries_PreservesAll()
    {
        var inputPath = Path.Combine(_testDir, "dupes.txt");
        var outputPath = Path.Combine(_testDir, "dupes_out.txt");

        var lines = new[] { "1. Apple", "1. Apple", "2. Apple", "1. Banana" };
        await File.WriteAllLinesAsync(inputPath, lines);
        await SortFileAsync(inputPath, outputPath);

        var result = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(4, result.Length);
        Assert.Equal("1. Apple", result[0]);
        Assert.Equal("1. Apple", result[1]);
        Assert.Equal("2. Apple", result[2]);
        Assert.Equal("1. Banana", result[3]);
    }

    [Fact]
    public async Task Sort_Cancellation_ThrowsOperationCanceled()
    {
        var inputPath = Path.Combine(_testDir, "cancel.txt");

        var sb = new StringBuilder();
        for (var i = 0; i < 10_000; i++)
            sb.AppendLine($"{i}. Line number {i}");
        await File.WriteAllTextAsync(inputPath, sb.ToString());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var tempDir = Path.Combine(_testDir, "cancel_temp");
        Directory.CreateDirectory(tempDir);

        var options = CreateOptions();
        var splitter = new ChunkFileSplitter(options, new AtomicFileWriter(NullLogger<AtomicFileWriter>.Instance), NullLogger<ChunkFileSplitter>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => splitter.SplitAsync(inputPath, tempDir, cts.Token));
    }

    [Fact]
    public async Task Sort_MultiPassMerge_ProducesCorrectOutput()
    {
        var inputPath = Path.Combine(_testDir, "multipass.txt");
        var outputPath = Path.Combine(_testDir, "multipass_out.txt");

        await WriteLargeInputAsync(inputPath, lineCount: 120_000, seed: 123);

        var splitter = new ChunkFileSplitter(CreateOptions(mergeWay: 2), new AtomicFileWriter(NullLogger<AtomicFileWriter>.Instance), NullLogger<ChunkFileSplitter>.Instance);
        var tempDir = Path.Combine(_testDir, "multipass_temp");
        Directory.CreateDirectory(tempDir);

        var splitResult = await splitter.SplitAsync(inputPath, tempDir, CancellationToken.None);
        Assert.True(splitResult.ChunkFiles.Count > 2, "Input should require multiple merge passes for this test.");

        await SortFileAsync(inputPath, outputPath, chunkSizeMb: 1, mergeWay: 2);

        var result = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(120_000, result.Length);
        AssertSortedLines(result);
    }

    [Fact]
    public async Task Sort_AlreadySorted_ProducesIdenticalOutput()
    {
        var inputPath = Path.Combine(_testDir, "sorted_input.txt");
        var outputPath = Path.Combine(_testDir, "sorted_output.txt");

        var lines = new[]
        {
            "1. Apple",
            "415. Apple",
            "2. Banana is yellow",
            "32. Cherry is the best",
            "30432. Something something something"
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        await SortFileAsync(inputPath, outputPath);

        var result = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(lines, result);
    }

    [Fact]
    public async Task Sort_ReverseSorted_ProducesCorrectOutput()
    {
        var inputPath = Path.Combine(_testDir, "reverse_input.txt");
        var outputPath = Path.Combine(_testDir, "reverse_output.txt");

        var lines = new[]
        {
            "30432. Something something something",
            "32. Cherry is the best",
            "2. Banana is yellow",
            "415. Apple",
            "1. Apple"
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        await SortFileAsync(inputPath, outputPath);

        var result = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(5, result.Length);
        Assert.Equal("1. Apple", result[0]);
        Assert.Equal("415. Apple", result[1]);
        Assert.Equal("2. Banana is yellow", result[2]);
        Assert.Equal("32. Cherry is the best", result[3]);
        Assert.Equal("30432. Something something something", result[4]);
    }

    [Fact]
    public async Task Sort_MalformedLine_ThrowsFormatException()
    {
        var inputPath = Path.Combine(_testDir, "malformed.txt");
        var outputPath = Path.Combine(_testDir, "malformed_out.txt");

        await File.WriteAllLinesAsync(inputPath, ["1. Valid", "bad line no separator", "2. Also Valid"]);

        await Assert.ThrowsAsync<FormatException>(() => SortFileAsync(inputPath, outputPath));
    }

    [Fact]
    public async Task Sort_MergeWay2_ProducesCorrectOutput()
    {
        var inputPath = Path.Combine(_testDir, "way2.txt");
        var outputPath = Path.Combine(_testDir, "way2_out.txt");

        var lines = new[]
        {
            "3. Cherry",
            "1. Apple",
            "2. Banana"
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        await SortFileAsync(inputPath, outputPath, chunkSizeMb: 1, mergeWay: 2);

        var result = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(3, result.Length);
        Assert.Equal("1. Apple", result[0]);
        Assert.Equal("2. Banana", result[1]);
        Assert.Equal("3. Cherry", result[2]);
    }

    [Fact]
    public async Task Sort_ViaDi_CleansTemporaryWorkspaceOnSuccess()
    {
        var inputPath = Path.Combine(_testDir, "cleanup_success_input.txt");
        var outputPath = Path.Combine(_testDir, "cleanup_success_output.txt");
        var tempBasePath = Path.Combine(_testDir, "cleanup_success_temp");

        Directory.CreateDirectory(tempBasePath);
        await File.WriteAllLinesAsync(inputPath, ["2. Banana", "1. Apple", "3. Cherry"]);

        await SortViaDiAsync(inputPath, outputPath, tempDirectory: tempBasePath);

        Assert.True(File.Exists(outputPath));
        Assert.Empty(Directory.GetDirectories(tempBasePath));
    }

    [Fact]
    public async Task Sort_ViaDi_CleansTemporaryWorkspaceOnFailure()
    {
        var inputPath = Path.Combine(_testDir, "cleanup_failure_input.txt");
        var outputPath = Path.Combine(_testDir, "cleanup_failure_output.txt");
        var tempBasePath = Path.Combine(_testDir, "cleanup_failure_temp");

        Directory.CreateDirectory(tempBasePath);
        await File.WriteAllLinesAsync(inputPath, ["1. Valid", "bad line no separator", "2. Valid"]);

        await Assert.ThrowsAsync<FormatException>(() =>
            SortViaDiAsync(inputPath, outputPath, tempDirectory: tempBasePath));

        Assert.Empty(Directory.GetDirectories(tempBasePath));
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task Sort_ViaDi_SameInputAndOutput_ThrowsArgumentException()
    {
        var inputPath = Path.Combine(_testDir, "same_path.txt");

        await File.WriteAllLinesAsync(inputPath, ["1. Apple"]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            SortViaDiAsync(inputPath, inputPath));
    }

    [Fact]
    public async Task Sort_ViaDi_MissingInput_ThrowsFileNotFoundException()
    {
        var inputPath = Path.Combine(_testDir, "missing.txt");
        var outputPath = Path.Combine(_testDir, "missing_output.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            SortViaDiAsync(inputPath, outputPath));
    }

    [Fact]
    public async Task Merge_Failure_DoesNotLeaveFinalOutputOrStagingFile()
    {
        var tempDir = Path.Combine(_testDir, "merge_failure_temp");
        Directory.CreateDirectory(tempDir);

        var chunkA = Path.Combine(tempDir, "chunk_a.tmp");
        var chunkB = Path.Combine(tempDir, "chunk_b.tmp");
        var outputPath = Path.Combine(_testDir, "merge_failure_output.txt");

        await File.WriteAllLinesAsync(chunkA, ["1. Apple", "2. Banana"]);
        await File.WriteAllLinesAsync(chunkB, ["3. Apple", "bad line no separator"]);

        var merger = new ChunkFileMerger(CreateOptions(), new AtomicFileWriter(NullLogger<AtomicFileWriter>.Instance), NullLogger<ChunkFileMerger>.Instance);

        await Assert.ThrowsAsync<FormatException>(() =>
            merger.MergeAsync([chunkA, chunkB], outputPath, tempDir, CancellationToken.None));

        Assert.False(File.Exists(outputPath));
        Assert.Empty(Directory.GetFiles(_testDir, "merge_failure_output.txt.*.writing"));
    }

    [Fact]
    public async Task Sort_NonAsciiText_ProducesCorrectOutput()
    {
        var inputPath = Path.Combine(_testDir, "unicode.txt");
        var outputPath = Path.Combine(_testDir, "unicode_out.txt");

        var lines = new[]
        {
            "3. Банан желтый",
            "1. Apple",
            "2. 樱桃最好",
            "4. Ångström"
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        await SortFileAsync(inputPath, outputPath);

        var result = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(4, result.Length);
        AssertSortedLines(result);
    }

    [Fact]
    public async Task Sort_NegativeNumbers_ProducesCorrectOutput()
    {
        var inputPath = Path.Combine(_testDir, "negative.txt");
        var outputPath = Path.Combine(_testDir, "negative_out.txt");

        var lines = new[]
        {
            "3. Apple",
            "-1. Apple",
            "0. Apple",
            "1. Apple"
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        await SortFileAsync(inputPath, outputPath);

        var result = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(4, result.Length);
        Assert.Equal("-1. Apple", result[0]);
        Assert.Equal("0. Apple", result[1]);
        Assert.Equal("1. Apple", result[2]);
        Assert.Equal("3. Apple", result[3]);
    }

    [Fact]
    public async Task Sort_VeryLargeLine_ProducesCorrectOutput()
    {
        var inputPath = Path.Combine(_testDir, "largeline.txt");
        var outputPath = Path.Combine(_testDir, "largeline_out.txt");

        var largeText = new string('X', 100_000);
        var lines = new[]
        {
            $"2. {largeText}",
            "1. Apple",
            $"1. {largeText}"
        };
        await File.WriteAllLinesAsync(inputPath, lines);
        await SortFileAsync(inputPath, outputPath, chunkSizeMb: 1);

        var result = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(3, result.Length);
        Assert.Equal("1. Apple", result[0]);
        AssertSortedLines(result);
    }

    [Fact]
    public async Task Merge_EmptyChunkFile_ProducesCorrectOutput()
    {
        var tempDir = Path.Combine(_testDir, "empty_chunk_temp");
        Directory.CreateDirectory(tempDir);

        var chunkA = Path.Combine(tempDir, "chunk_a.tmp");
        var chunkB = Path.Combine(tempDir, "chunk_b.tmp");
        var outputPath = Path.Combine(_testDir, "empty_chunk_out.txt");

        await File.WriteAllLinesAsync(chunkA, ["1. Apple", "2. Banana"]);
        await File.WriteAllTextAsync(chunkB, string.Empty);

        var merger = new ChunkFileMerger(CreateOptions(), new AtomicFileWriter(NullLogger<AtomicFileWriter>.Instance), NullLogger<ChunkFileMerger>.Instance);
        await merger.MergeAsync([chunkA, chunkB], outputPath, tempDir, CancellationToken.None);

        var result = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(2, result.Length);
        Assert.Equal("1. Apple", result[0]);
        Assert.Equal("2. Banana", result[1]);
    }
}
