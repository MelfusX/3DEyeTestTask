namespace FileSorting.Generation.Configuration;

public sealed class GenerationOptions
{
    public const string SectionName = "Generation";

    public long FileSizeMb { get; }
    public int MaxNumber { get; }
    public IReadOnlyList<string> StringPool { get; }

    public GenerationOptions(long fileSizeMb, int maxNumber, string[] stringPool)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fileSizeMb, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(maxNumber, int.MaxValue);
        ArgumentNullException.ThrowIfNull(stringPool);

        if (stringPool.Length == 0)
        {
            throw new ArgumentException("String pool must not be empty.", nameof(stringPool));
        }

        if (Array.Exists(stringPool, string.IsNullOrEmpty))
        {
            throw new ArgumentException("String pool must not contain null or empty entries.", nameof(stringPool));
        }

        FileSizeMb = fileSizeMb;
        MaxNumber = maxNumber;
        StringPool = [.. stringPool];
    }
}
