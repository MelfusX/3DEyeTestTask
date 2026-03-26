using System.Text;
using FileSorting.Generation.Configuration;
using Microsoft.Extensions.Logging;

namespace FileSorting.Generation;

public sealed class FileGenerationService
{
    private readonly GenerationOptions _options;
    private readonly ILogger<FileGenerationService> _logger;

    private const int BufferSize = 64 * 1024;

    public FileGenerationService(GenerationOptions options, ILogger<FileGenerationService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task GenerateAsync(string outputPath, long targetSizeBytes, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating file {Path} with target size {Size} bytes", outputPath, targetSizeBytes);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        try
        {
            await using var stream = new FileStream(
                outputPath, 
                FileMode.Create, 
                FileAccess.Write, 
                FileShare.None,
                BufferSize, 
                FileOptions.Asynchronous);

            await using StreamWriter writer = new(stream, Encoding.UTF8, BufferSize);

            (long lineCount, long bytesWritten) = await WriteEntriesAsync(writer, targetSizeBytes, cancellationToken);

            _logger.LogInformation("Generation complete: {Lines} lines, {Bytes} bytes", lineCount, bytesWritten);
        }
        catch
        {
            try 
            { 
                File.Delete(outputPath); 
            } 
            catch 
            { 
                _logger.LogWarning("Failed to delete incomplete file {Path} after generation error", outputPath);
            }
            
            throw;
        }
    }

    private async Task<(long LineCount, long BytesWritten)> WriteEntriesAsync(
        StreamWriter writer, long targetSizeBytes, CancellationToken cancellationToken)
    {
        var stringPool = _options.StringPool;
        var maxNumber = _options.MaxNumber;
        var random = Random.Shared;
        var newLineByteCount = Encoding.UTF8.GetByteCount(Environment.NewLine);
        var numBuffer = new char[16];
        var stringPoolByteCounts = stringPool.Select(text => Encoding.UTF8.GetByteCount(text)).ToArray();

        long bytesWritten = 0;
        long lineCount = 0;
        var lastReportPercent = 0;

        while (bytesWritten < targetSizeBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var number = random.Next(1, maxNumber + 1);
            var textIndex = random.Next(stringPool.Count);
            var text = stringPool[textIndex];

            number.TryFormat(numBuffer, out var numChars);
            await writer.WriteAsync(numBuffer, 0, numChars);
            await writer.WriteAsync(". ");
            await writer.WriteLineAsync(text);

            bytesWritten += stringPoolByteCounts[textIndex]
                            + numChars
                            + 2 + newLineByteCount;
            lineCount++;

            ReportProgress(ref lastReportPercent, bytesWritten, targetSizeBytes, lineCount);
        }

        return (lineCount, bytesWritten);
    }

    private void ReportProgress(ref int lastReportPercent, long bytesWritten, long targetSizeBytes, long lineCount)
    {
        var percent = (int)(bytesWritten * 100 / targetSizeBytes);
        if (percent >= lastReportPercent + 5)
        {
            lastReportPercent = percent;
            _logger.LogInformation("Generation progress: {Percent}% ({Lines} lines)", percent, lineCount);
        }
    }
}
