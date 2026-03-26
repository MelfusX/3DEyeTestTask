using System.Text;

namespace FileSorting.Sorting.Configuration;

public sealed class SortingOptions
{
    public const string SectionName = "Sorting";

    public int ChunkSizeMb { get; }
    public int MergeWayCount { get; }
    public int MaxMergeBufferMb { get; }
    public string? TempDirectory { get; }
    public Encoding Encoding { get; }

    public SortingOptions(
        int chunkSizeMb,
        int mergeWayCount,
        int maxMergeBufferMb,
        string? tempDirectory,
        string encodingName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSizeMb, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(mergeWayCount, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMergeBufferMb, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(encodingName);

        ChunkSizeMb = chunkSizeMb;
        MergeWayCount = mergeWayCount;
        MaxMergeBufferMb = maxMergeBufferMb;
        TempDirectory = tempDirectory;
        Encoding = Encoding.GetEncoding(encodingName);
    }
}
