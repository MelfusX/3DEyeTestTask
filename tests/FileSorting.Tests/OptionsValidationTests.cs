using FileSorting.Generation.Configuration;
using FileSorting.Sorting.Configuration;

namespace FileSorting.Tests;

public class OptionsValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SortingOptions_InvalidChunkSize_Throws(int chunkSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SortingOptions(chunkSize, 2, 64, null, "utf-8"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public void SortingOptions_InvalidMergeWay_Throws(int mergeWay)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SortingOptions(256, mergeWay, 64, null, "utf-8"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SortingOptions_InvalidMaxMergeBuffer_Throws(int buffer)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SortingOptions(256, 16, buffer, null, "utf-8"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SortingOptions_InvalidEncodingName_Throws(string encodingName)
    {
        Assert.Throws<ArgumentException>(() =>
            new SortingOptions(256, 16, 64, null, encodingName));
    }

    [Fact]
    public void SortingOptions_NullEncodingName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SortingOptions(256, 16, 64, null, null!));
    }

    [Fact]
    public void SortingOptions_ValidValues_Succeeds()
    {
        var options = new SortingOptions(256, 2, 1, null, "utf-8");

        Assert.Equal(256, options.ChunkSizeMb);
        Assert.Equal(2, options.MergeWayCount);
        Assert.Equal(1, options.MaxMergeBufferMb);
        Assert.NotNull(options.Encoding);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GenerationOptions_InvalidFileSizeMb_Throws(long fileSizeMb)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenerationOptions(fileSizeMb, 100, ["Apple"]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2147483647)]
    public void GenerationOptions_InvalidMaxNumber_Throws(int maxNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenerationOptions(1024, maxNumber, ["Apple"]));
    }

    [Fact]
    public void GenerationOptions_NullStringPool_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GenerationOptions(1024, 100, null!));
    }

    [Fact]
    public void GenerationOptions_EmptyStringPool_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenerationOptions(1024, 100, []));
    }

    [Fact]
    public void GenerationOptions_ValidValues_Succeeds()
    {
        var options = new GenerationOptions(1024, 100000, ["Apple", "Banana"]);

        Assert.Equal(1024, options.FileSizeMb);
        Assert.Equal(100000, options.MaxNumber);
        Assert.Equal(2, options.StringPool.Count);
    }

    [Fact]
    public void GenerationOptions_CopiesInputStringPool()
    {
        var source = new[] { "Apple", "Banana" };
        var options = new GenerationOptions(1024, 100000, source);

        source[0] = "Mutated";

        Assert.Equal("Apple", options.StringPool[0]);
    }

    [Fact]
    public void GenerationOptions_NullEntryInStringPool_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenerationOptions(1024, 100, ["Apple", null!, "Banana"]));
    }

    [Fact]
    public void GenerationOptions_EmptyEntryInStringPool_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenerationOptions(1024, 100, ["Apple", "", "Banana"]));
    }
}
