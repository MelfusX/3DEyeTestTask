# FileSorting

A tool for sorting large text files that don't fit into RAM.

Each line has the format <int>. <string> — a number, followed by a dot and a space, followed by arbitrary text. Sorting is done by the text part first (lexicographically), then by the number if texts are equal.

## How it works

The solution is based on classic external merge sort. The input file is split into fixed-size chunks (256 MB by default), each chunk is sorted in memory and written to a temp file. Then the sorted chunks are merged via k-way merge using `PriorityQueue`. If there are more chunks than `MergeWayCount`, merging happens in multiple passes with parallel processing of intermediate batches.

There's also a generator that creates test files of a given size from a configurable pool of strings and a number range.

## Project structure

- **FileSorting.Domain** — `FileEntry` model (readonly struct with span-based parsing, no unnecessary allocations)
- **FileSorting.Generation** — test file generation
- **FileSorting.Sorting** — sorting: splitting, k-way merging, atomic writes via staging files
- **FileSorting.Generator.App** / **FileSorting.Sorter.App** — CLI apps (System.CommandLine)
- **FileSorting.Tests** — unit and integration tests
- **FileSorting.Benchmarks** — parsing and sorting benchmarks (BenchmarkDotNet)

## Key decisions

- **Atomic writes**: data goes to a staging file with a GUID in its name, then `File.Move` on completion. No corrupted output on crash or cancellation.
- **Span-based parsing**: `FileEntry` uses `ReadOnlySpan<char>`, avoiding extra string copies.
- **Configurable merge**: chunk size, k-way factor, and reader buffer size are all in config — can be tuned for specific hardware.
- **Read-ahead during merge**: async read of the next line from a chunk while the current one is being processed.
- **Auto-cleanup**: temp directories are removed via an `IDisposable` workspace.

## Usage

### Generate a test file

```bash
dotnet run --project src/FileSorting.Generator.App -- --size 1024 --output data.txt
```

Options:
- `--output, -o` — Output file path (required)
- `--size, -s` — Target file size in MB (default: from config)
- `--max-number, -n` — Maximum number value (default: from config)

### Sort a file

```bash
dotnet run --project src/FileSorting.Sorter.App -- --input data.txt --output sorted.txt
```

Options:
- `--input, -i` — Input file path to sort (required)
- `--output, -o` — Output file path (default: `_sorted_<input name>` in the input directory)
- `--chunk-size, -c` — Chunk size in MB (default: from config)
- `--merge-way, -m` — K-way merge factor (default: from config)
- `--temp-dir, -t` — Temporary directory for chunks

### Configuration

Both apps use `appsettings.json` for defaults. CLI arguments override config values.

**Generator** (`src/FileSorting.Generator.App/appsettings.json`):
```json
{
  "Generation": {
    "FileSizeMb": 1024,
    "MaxNumber": 100000,
    "StringPool": ["The quick brown fox", "Jumped over the lazy dog"]
  }
}
```

**Sorter** (`src/FileSorting.Sorter.App/appsettings.json`):
```json
{
  "Sorting": {
    "ChunkSizeMb": 256,
    "MergeWayCount": 16,
    "MaxMergeBufferMb": 64,
    "TempDirectory": null,
    "EncodingName": "utf-8"
  }
}
```
