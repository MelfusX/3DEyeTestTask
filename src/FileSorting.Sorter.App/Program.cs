using System.CommandLine;
using System.Diagnostics;
using FileSorting.Sorting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var inputOption = new Option<string>(
    aliases: ["--input", "-i"],
    description: "Input file path to sort") { IsRequired = true };

var outputOption = new Option<string?>(
    aliases: ["--output", "-o"],
    description: "Output file path (defaults to sorted_<input name>)");

var chunkSizeOption = new Option<int?>(
    aliases: ["--chunk-size", "-c"],
    description: "Chunk size in MB for external merge sort");

var mergeWayOption = new Option<int?>(
    aliases: ["--merge-way", "-m"],
    description: "Number of chunks to merge simultaneously");

var tempDirOption = new Option<string?>(
    aliases: ["--temp-dir", "-t"],
    description: "Temporary directory for chunk files");

var rootCommand = new RootCommand("File Sorter - sorts large files using external merge sort")
{
    inputOption, outputOption, chunkSizeOption, mergeWayOption, tempDirOption
};

rootCommand.SetHandler(async (input, output, chunkSize, mergeWay, tempDir) =>
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });

    builder.Services.AddSortingServices(
        builder.Configuration,
        chunkSizeMb: chunkSize,
        mergeWayCount: mergeWay,
        tempDirectory: tempDir);

    using var host = builder.Build();
    var sortingService = host.Services.GetRequiredService<FileSortingService>();
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SorterApp");

    var inputPath = input;
    var outputPath = output ?? Path.Combine(Path.GetDirectoryName(inputPath) ?? ".", "sorted_" + Path.GetFileName(inputPath));

    if (!File.Exists(inputPath))
    {
        logger.LogError("Input file not found: {Path}", inputPath);

        return;
    }

    var fileInfo = new FileInfo(inputPath);
    logger.LogInformation("Starting sort: {Input} ({Size}MB) -> {Output}",
        inputPath, fileInfo.Length / (1024 * 1024), outputPath);

    var sw = Stopwatch.StartNew();
    await sortingService.SortAsync(inputPath, outputPath, cts.Token);
    sw.Stop();

    logger.LogInformation("File sorted in {Elapsed}", sw.Elapsed);
}, inputOption, outputOption, chunkSizeOption, mergeWayOption, tempDirOption);

await rootCommand.InvokeAsync(args);
