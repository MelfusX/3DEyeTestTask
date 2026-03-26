using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FileSorting.Domain.Models;

namespace FileSorting.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[RankColumn]
public class ParsingBenchmarks
{
    private string[] _lines = null!;

    [Params("Ascii", "Utf8Mixed")]
    public string Dataset { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        var strings = Dataset == "Utf8Mixed"
            ? new[] { "Apple", "Банан желтый", "樱桃最好", "😀 Emoji payload", "Résumé sample" }
            : new[] { "Apple", "Banana is yellow", "Cherry is the best", "Something something" };
        _lines = new string[100_000];
        for (var i = 0; i < _lines.Length; i++)
        {
            _lines[i] = $"{random.Next(1, 100000)}. {strings[random.Next(strings.Length)]}";
        }
    }

    [Benchmark]
    public void ParseLines()
    {
        foreach (var line in _lines)
            FileEntryFactory.Create(line);
    }

    [Benchmark]
    public void SortEntries()
    {
        var entries = new List<FileEntry>(_lines.Length);
        foreach (var line in _lines)
            entries.Add(FileEntryFactory.Create(line));
        entries.Sort();
    }
}
