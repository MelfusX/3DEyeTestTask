using FileSorting.Domain.Models;

namespace FileSorting.Tests;

public class FileEntryFactoryTests
{
    private static FileEntry Entry(int number, string text) => FileEntryFactory.Create($"{number}. {text}");

    [Theory]
    [InlineData("415. Apple", 415, "Apple")]
    [InlineData("1. Apple", 1, "Apple")]
    [InlineData("30432. Something something something", 30432, "Something something something")]
    [InlineData("32. Cherry is the best", 32, "Cherry is the best")]
    public void Create_ValidLine_ReturnsCorrectValues(string line, int expectedNumber, string expectedText)
    {
        var entry = FileEntryFactory.Create(line);

        Assert.Equal(expectedNumber, entry.Number);
        Assert.Equal($"{expectedNumber}. {expectedText}", entry.ToString());
    }

    [Fact]
    public void Create_InvalidFormat_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => FileEntryFactory.Create("no separator here"));
    }

    [Fact]
    public void Create_InvalidNumber_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => FileEntryFactory.Create("abc. Text"));
    }

    [Theory]
    [InlineData(415, "Apple", "415. Apple")]
    [InlineData(1, "Test text", "1. Test text")]
    public void ToString_ReturnsCorrectString(int number, string text, string expected)
    {
        var entry = Entry(number, text);
        Assert.Equal(expected, entry.ToString());
    }

    [Fact]
    public void Create_ToString_Roundtrip()
    {
        var original = "12345. Hello World";
        var entry = FileEntryFactory.Create(original);
        var formatted = entry.ToString();
        Assert.Equal(original, formatted);
    }

    [Fact]
    public void Create_EmptyLine_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => FileEntryFactory.Create(""));
    }

    [Fact]
    public void Create_TextWithDotsInside_ParsesCorrectly()
    {
        var entry = FileEntryFactory.Create("5. Hello. World. Test");

        Assert.Equal(5, entry.Number);
        Assert.Equal("5. Hello. World. Test", entry.ToString());
    }

    [Fact]
    public void Create_LargeNumber_ParsesCorrectly()
    {
        var entry = FileEntryFactory.Create("2147483647. Max int");

        Assert.Equal(int.MaxValue, entry.Number);
        Assert.Equal("2147483647. Max int", entry.ToString());
    }

    [Fact]
    public void Create_ZeroNumber_ParsesCorrectly()
    {
        var entry = FileEntryFactory.Create("0. Zero entry");

        Assert.Equal(0, entry.Number);
        Assert.Equal("0. Zero entry", entry.ToString());
    }

    [Fact]
    public void Create_NegativeNumber_ParsesCorrectly()
    {
        var entry = FileEntryFactory.Create("-1. Negative");

        Assert.Equal(-1, entry.Number);
        Assert.Equal("-1. Negative", entry.ToString());
    }

    [Fact]
    public void Create_OnlySeparator_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => FileEntryFactory.Create(". "));
    }
}
