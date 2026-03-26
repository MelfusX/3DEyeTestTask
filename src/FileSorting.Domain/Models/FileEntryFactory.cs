using System.Globalization;

namespace FileSorting.Domain.Models;

public static class FileEntryFactory
{
    internal const string Separator = ". ";

    public static FileEntry Create(string line)
    {
        var separatorIndex = line.IndexOf(Separator, StringComparison.Ordinal);

        if (separatorIndex < 0)
        {
            throw new FormatException("Invalid line format: separator '. ' not found.");
        }

        if (!int.TryParse(line.AsSpan(0, separatorIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            throw new FormatException("Invalid number in line.");
        }

        return new FileEntry(number, line, separatorIndex + Separator.Length);
    }
}
