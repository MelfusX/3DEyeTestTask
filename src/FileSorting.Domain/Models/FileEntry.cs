namespace FileSorting.Domain.Models;

public readonly struct FileEntry : IComparable<FileEntry>, IEquatable<FileEntry>
{
    public int Number { get; }
    public ReadOnlySpan<char> TextSpan => _line.AsSpan(_textStart);

    private readonly string _line;
    private readonly int _textStart;

    internal FileEntry(int number, string line, int textStart)
    {
        Number = number;
        _line = line;
        _textStart = textStart;
    }

    public int CompareTo(FileEntry other)
    {
        var textCmp = _line.AsSpan(_textStart).SequenceCompareTo(other._line.AsSpan(other._textStart));
        return textCmp != 0 ? textCmp : Number.CompareTo(other.Number);
    }

    public override string ToString() => _line;

    public void WriteTo(TextWriter writer) => writer.WriteLine(_line);

    public bool Equals(FileEntry other) => Number == other.Number 
        && _line.AsSpan(_textStart).SequenceEqual(other._line.AsSpan(other._textStart));

    public override bool Equals(object? obj) => obj is FileEntry other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Number, string.GetHashCode(_line.AsSpan(_textStart)));

    public static bool operator ==(FileEntry left, FileEntry right) => left.Equals(right);
    public static bool operator !=(FileEntry left, FileEntry right) => !left.Equals(right);
    public static bool operator <(FileEntry left, FileEntry right) => left.CompareTo(right) < 0;
    public static bool operator >(FileEntry left, FileEntry right) => left.CompareTo(right) > 0;
    public static bool operator <=(FileEntry left, FileEntry right) => left.CompareTo(right) <= 0;
    public static bool operator >=(FileEntry left, FileEntry right) => left.CompareTo(right) >= 0;
}
