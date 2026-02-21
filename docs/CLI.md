# SharpCompress CLI (Native AOT)

`SharpCompress.Cli` is a `net10.0` console executable that inspects archives using SharpCompress APIs.

## Build

```bash
dotnet build src/SharpCompress.Cli/SharpCompress.Cli.csproj -c Release
```

## Run

```bash
dotnet run --project src/SharpCompress.Cli/SharpCompress.Cli.csproj -- inspect tests/TestArchives/Archives/Zip.deflate.zip
```

## Commands

- `inspect <archive...>`: show archive summary and entry metadata.
- `list <archive...>`: list entry metadata without summary.
- `formats`: list supported formats and whether they support forward or seekable mode.

The root command also accepts archive paths directly and behaves like `inspect`.

## Access Modes

- `--access forward` (default): start with the forward-only Reader API.
- `--access seekable`: force Archive API mode.

When `--access forward` is used, the CLI automatically switches to seekable mode when required (for example multi-volume archives).

## Options

- `--format table|json`: output mode.
- `--long`: include extra metadata columns.
- `--include-directories`: include directory entries in output.
- `--limit <n>`: cap displayed entries per archive.
- `--password <value>`: provide password for encrypted archives.
- `--look-for-header`: enable header scan for self-extracting archives.
- `--extension-hint <ext>`: help decoder selection (`zip`, `tar.gz`, `7z`, ...).
- `--rewindable-buffer-size <bytes>`: tune forward detection buffering.

## Native AOT Publish

```bash
dotnet publish src/SharpCompress.Cli/SharpCompress.Cli.csproj -c Release -r osx-arm64 -p:PublishAot=true
```
