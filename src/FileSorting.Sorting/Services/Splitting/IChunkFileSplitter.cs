namespace FileSorting.Sorting.Services.Splitting;

public interface IChunkFileSplitter
{
    Task<SplitFileResult> SplitAsync(
        string inputPath,
        string tempDirectory,
        CancellationToken cancellationToken);
}