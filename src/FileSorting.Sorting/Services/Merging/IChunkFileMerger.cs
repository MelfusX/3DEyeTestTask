namespace FileSorting.Sorting.Services.Merging;

public interface IChunkFileMerger
{
    Task MergeAsync(
        IReadOnlyList<string> chunkFiles,
        string outputPath,
        string tempDirectory,
        CancellationToken cancellationToken);
}