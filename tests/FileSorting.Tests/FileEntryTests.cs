using FileSorting.Domain.Models;

namespace FileSorting.Tests;

public class FileEntryTests
{
    private static FileEntry Entry(int number, string text) => FileEntryFactory.Create($"{number}. {text}");

    [Fact]
    public void CompareTo_DifferentText_SortsByText()
    {
        var apple = Entry(100, "Apple");
        var banana = Entry(1, "Banana");

        Assert.True(apple.CompareTo(banana) < 0);
        Assert.True(banana.CompareTo(apple) > 0);
    }

    [Fact]
    public void CompareTo_SameText_SortsByNumber()
    {
        var entry1 = Entry(1, "Apple");
        var entry415 = Entry(415, "Apple");

        Assert.True(entry1.CompareTo(entry415) < 0);
        Assert.True(entry415.CompareTo(entry1) > 0);
    }

    [Fact]
    public void CompareTo_Equal_ReturnsZero()
    {
        var a = Entry(42, "Test");
        var b = Entry(42, "Test");

        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void Sort_ProducesCorrectOrder()
    {
        var entries = new List<FileEntry>
        {
            Entry(415, "Apple"),
            Entry(30432, "Something something something"),
            Entry(1, "Apple"),
            Entry(32, "Cherry is the best"),
            Entry(2, "Banana is yellow")
        };

        entries.Sort();

        Assert.Equal("1. Apple", entries[0].ToString());
        Assert.Equal("415. Apple", entries[1].ToString());
        Assert.Equal("2. Banana is yellow", entries[2].ToString());
        Assert.Equal("32. Cherry is the best", entries[3].ToString());
        Assert.Equal("30432. Something something something", entries[4].ToString());
    }

    [Fact]
    public void Operators_WorkCorrectly()
    {
        var small = Entry(1, "Apple");
        var large = Entry(2, "Banana");

        Assert.True(small < large);
        Assert.True(large > small);
        Assert.True(small <= large);
        Assert.True(large >= small);
        Assert.True(small <= Entry(1, "Apple"));
        Assert.True(small >= Entry(1, "Apple"));
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = Entry(42, "Test");
        var b = Entry(42, "Test");

        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var a = Entry(42, "Test");
        var b = Entry(43, "Test");
        var c = Entry(42, "Other");

        Assert.False(a.Equals(b));
        Assert.False(a.Equals(c));
        Assert.True(a != b);
    }

    [Fact]
    public void GetHashCode_EqualEntries_ReturnsSameHash()
    {
        var a = Entry(42, "Test");
        var b = Entry(42, "Test");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Theory]
    [InlineData(1, "Apple", "1. Apple")]
    [InlineData(415, "Test text", "415. Test text")]
    [InlineData(0, "Zero", "0. Zero")]
    public void ToString_ReturnsCanonicalString(int number, string text, string expected)
    {
        var entry = Entry(number, text);
        Assert.Equal(expected, entry.ToString());
    }

    [Fact]
    public void WriteTo_WritesFormattedLineWithNewline()
    {
        var entry = Entry(42, "Answer");
        using var sw = new StringWriter();

        entry.WriteTo(sw);

        Assert.Equal("42. Answer" + Environment.NewLine, sw.ToString());
    }

    [Fact]
    public void WriteTo_MultipleEntries_WritesEachOnNewLine()
    {
        var entries = new[] { Entry(1, "First"), Entry(2, "Second") };
        using var sw = new StringWriter();

        foreach (var entry in entries)
            entry.WriteTo(sw);

        var lines = sw.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("1. First", lines[0]);
        Assert.Equal("2. Second", lines[1]);
    }
}
