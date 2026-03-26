using System.Runtime.InteropServices;
using FileSorting.Sorting.Configuration;
using FileSorting.Sorting.Infrastructure.IO;
using FileSorting.Sorting.Infrastructure.TempWorkspaces;
using FileSorting.Sorting.Services.Merging;
using FileSorting.Sorting.Services.Splitting;
using Microsoft.Extensions.Logging;

namespace FileSorting.Sorting;

public sealed class FileSortingService
{
    private readonly IChunkFileSplitter _splitter;
    private readonly IChunkFileMerger _merger;
    private readonly IFileWriter _fileWriter;
    private readonly SortingOptions _options;
    private readonly ITemporaryWorkspaceFactory _temporaryWorkspaceFactory;
    private readonly ILogger<FileSortingService> _logger;

    public FileSortingService(
        IChunkFileSplitter splitter,
        IChunkFileMerger merger,
        IFileWriter fileWriter,
        SortingOptions options,
        ITemporaryWorkspaceFactory temporaryWorkspaceFactory,
        ILogger<FileSortingService> logger)
    {
        _splitter = splitter;
        _merger = merger;
        _fileWriter = fileWriter;
        _options = options;
        _temporaryWorkspaceFactory = temporaryWorkspaceFactory;
        _logger = logger;
    }

    public async Task SortAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        var validatedPaths = ValidatePaths(inputPath, outputPath);

        using var workspace = _temporaryWorkspaceFactory.Create(_options.TempDirectory);

        var splitResult = await _splitter.SplitAsync(validatedPaths.InputPath, workspace.RootPath, cancellationToken);

        if (splitResult.ChunkFiles.Count == 0)
        {
            await _fileWriter.WriteAsync(
                validatedPaths.OutputPath,
                _options.Encoding,
                static (_, _) => Task.CompletedTask,
                cancellationToken);
                
            return;
        }

        if (splitResult.ChunkFiles.Count == 1)
        {
            await _fileWriter.CopyIntoPlaceAsync(
                splitResult.ChunkFiles[0],
                validatedPaths.OutputPath,
                cancellationToken);

            _fileWriter.TryDeleteFile(splitResult.ChunkFiles[0]);

            return;
        }

        await _merger.MergeAsync(splitResult.ChunkFiles, validatedPaths.OutputPath, workspace.RootPath, cancellationToken);
    }

    private static (string InputPath, string OutputPath) ValidatePaths(string inputPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var fullInput = Path.GetFullPath(inputPath);
        var fullOutput = Path.GetFullPath(outputPath);
        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!File.Exists(fullInput))
        {
            throw new FileNotFoundException("Input file not found.", fullInput);
        }

        if (string.Equals(fullInput, fullOutput, comparison))
        {
            throw new ArgumentException("Input and output paths must be different.");
        }

        var outputDirectory = Path.GetDirectoryName(fullOutput);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        return (fullInput, fullOutput);
    }
}
