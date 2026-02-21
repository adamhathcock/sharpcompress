# SharpCompress CLI Native AOT Plan (System.CommandLine + Spectre.Console)

## Goal
Create a `net10.0` Native AOT console tool to inspect archive files.

## Libraries
- System.CommandLine (parsing, help, completions)
- Spectre.Console (terminal output)

## Access Modes
- `--access forward` (default): use Reader API first.
- `--access seekable`: force Archive API only.
- In default forward mode, auto-switch to seekable when required by format/layout.

## Commands
- `inspect <archive...>`
- `list <archive...>`
- `formats`

## Key Options
- `--access forward|seekable`
- `--format table|json`
- `--password`
- `--look-for-header`
- `--extension-hint`
- `--rewindable-buffer-size`

## Fallback Rules (Default Forward)
1. Detect multi-volume archive parts -> use seekable.
2. Forward parse fails but seekable parse succeeds -> use seekable.
3. Report fallback reason in output.

## Deliverables
- `src/SharpCompress.Cli/` project
- Mode-aware inspection service
- Table + JSON renderers
- CLI docs and examples
- Tests for forward/seekable/fallback behavior

## Validation
- `dotnet tool restore`
- `dotnet csharpier format .`
- `dotnet csharpier check .`
- `dotnet test tests/SharpCompress.Test/SharpCompress.Test.csproj -c Release -f net10.0`
- `dotnet publish src/SharpCompress.Cli/SharpCompress.Cli.csproj -c Release -r osx-arm64 -p:PublishAot=true`
