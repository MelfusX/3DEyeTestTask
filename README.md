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
