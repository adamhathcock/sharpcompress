# SharpCompress AI Agent Instructions

## Project Overview
SharpCompress is a pure C# compression library supporting multiple archive formats (Zip, Tar, GZip, BZip2, 7Zip, Rar, LZip, XZ, ZStandard) for .NET Framework 4.8, .NET 8.0, and .NET 10.0. The library provides both seekable Archive APIs and forward-only Reader/Writer APIs for streaming scenarios.

## Architecture & Design Patterns

### Three-Tier API Design
SharpCompress has three distinct API patterns for different use cases:

1. **Archive API** (`IArchive`) - Random access on seekable streams
   - Use for: File-based archives where you can seek backward/forward
   - Example: `ZipArchive.Open()`, `TarArchive.Open()`, `RarArchive.Open()`
   - Located in: `src/SharpCompress/Archives/`

2. **Reader API** (`IReader`) - Forward-only on non-seekable streams
   - Use for: Streaming scenarios (network, pipes) where seeking isn't possible
   - Example: `ZipReader.Open()`, `TarReader.Open()`, `ReaderFactory.Open()`
   - Located in: `src/SharpCompress/Readers/`

3. **Writer API** (`IWriter`) - Forward-only writing
   - Use for: Creating archives in streaming fashion
   - Example: `ZipWriter`, `TarWriter`, `WriterFactory.Open()`
   - Located in: `src/SharpCompress/Writers/`

**Important:** 7Zip only supports Archive API due to format design limitations.

### Factory Pattern
All format types implement factory interfaces (`IArchiveFactory`, `IReaderFactory`, `IWriterFactory`) for auto-detection:
- `ReaderFactory.Open()` - Auto-detects format by probing stream
- `WriterFactory.Open()` - Creates writer for specified `ArchiveType`
- Factories located in: `src/SharpCompress/Factories/`

### Stream Disposal Rules (Changed in v0.21)
**Critical:** SharpCompress closes wrapped streams by default to align with .NET Framework expectations.

- Always use `ReaderOptions` or `WriterOptions` with `LeaveStreamOpen = true` to prevent disposal
- Example: `new ReaderOptions { LeaveStreamOpen = true }`
- When working with compression streams directly, use `NonDisposingStream` wrapper (if it exists)
- Always wrap operations in `using` blocks for proper disposal

## Development Workflow

### Building & Testing
```bash
# Build entire solution
dotnet build SharpCompress.sln

# Build specific framework (library targets: net48, net481, netstandard2.0, net6.0, net8.0)
dotnet build src/SharpCompress/SharpCompress.csproj -f net8.0

# Run tests (targets: net10.0, net48)
dotnet test tests/SharpCompress.Test/SharpCompress.Test.csproj -f net10.0

# Custom build script (Bullseye-based)
dotnet run --project build/build.csproj -- test
```

### Code Formatting (REQUIRED)
```bash
# Restore CSharpier tool
dotnet tool restore

# Format code (MUST run before committing)
dotnet csharpier .

# Check formatting
dotnet csharpier check .
```
**Never commit without running `dotnet csharpier .` from project root.**

### VS Code Tasks
- Build: `Ctrl+Shift+B` (Cmd+Shift+B on Mac)
- Test: Use "test" task or F5 to debug tests
- Format: "format" task runs CSharpier

### Debugging Features
When building with `DEBUG_STREAMS` constant (enabled for net10.0 Debug builds):
- Stream operations emit debug information
- Helps trace stream lifecycle and disposal issues
- See `#if DEBUG_STREAMS` blocks in stream classes

## Code Conventions

### Nullable Reference Types
- **All variables are non-nullable by default**
- Check for `null` only at entry points (public APIs)
- Always use `is null` or `is not null` (never `== null` or `!= null`)
- Trust C# null annotations - don't add redundant null checks
- Extension method: `value.NotNull(nameof(value))` validates parameters

### Async/Await Patterns
- **All I/O operations support async/await** with `CancellationToken`
- Naming: Async methods end with `Async` suffix
- Key async methods:
  - `WriteEntryToAsync(stream, cancellationToken)`
  - `WriteAllToDirectoryAsync(path, options, cancellationToken)` 
  - `OpenEntryStreamAsync(cancellationToken)`
  - `MoveToNextEntryAsync(cancellationToken)`
- Always provide `CancellationToken` parameter in new async methods

### C# Style
- Use latest C# features (currently C# 14)
- File-scoped namespaces required (`namespace SharpCompress.Archives;`)
- `var` for all local variables unless type clarity is critical
- Expression-bodied members preferred for simple operations
- Private fields use `_camelCase` prefix (enforced by .editorconfig)
- Constants use `CONSTANT_CASE` (all caps with underscores)

## Testing Patterns

### Test Organization
- Base class: `TestBase` - Provides `TEST_ARCHIVES_PATH`, `SCRATCH_FILES_PATH`, temp directory management
- Framework: xUnit with AwesomeAssertions
- Test archives: `tests/TestArchives/` - Use existing archives, don't create new ones unnecessarily
- **Never emit "Arrange", "Act", "Assert" comments** - code should be self-documenting
- Match naming style of nearby test files

### Common Test Patterns
```csharp
public class MyFormatTests : TestBase
{
    [Fact]
    public void ExtractTest()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "test.zip");
        using (var archive = ZipArchive.Open(testArchive))
        using (var reader = archive.ExtractAllEntries())
        {
            reader.WriteAllToDirectory(SCRATCH_FILES_PATH, 
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
        }
        VerifyFiles(); // Compares against ORIGINAL_FILES_PATH
    }
}
```

### Critical Test Areas
- Test both Archive and Reader APIs when format supports both
- Test async operations with cancellation tokens
- Test stream disposal behavior (`LeaveStreamOpen`)
- Test with multiple target frameworks if behavior differs (net10.0 vs net48)
- Edge cases: empty archives, large files, encrypted archives, multi-volume

## Format-Specific Knowledge

### Tar Considerations
- **Tar requires file size in header** - If stream is non-seekable and no size provided, TarWriter throws
- Often combined with compression: `.tar.gz`, `.tar.bz2`, `.tar.xz`, `.tar.lz`
- Long filenames handled via GNU longlink extension

### Zip Considerations  
- Supports Zip64 for large files (seekable streams only)
- Encryption: PKWare and WinZip AES supported (except encrypted LZMA)
- Compression methods: DEFLATE (default), Deflate64 (read-only), BZip2, LZMA, PPMd, Shrink, Reduce, Implode
- Multi-volume Zip requires `ZipArchive` (Reader can't seek across volumes)
- `ZipReader` processes LocalEntry headers and intentionally skips DirectoryEntry headers (they're redundant in streaming mode)

### Rar Considerations
- Read-only format (proprietary)
- RAR5 decryption supported but CRC check incomplete
- SOLID archives require sequential extraction for performance

### 7Zip Limitations
- No Reader/Writer API support (format doesn't support streaming)
- Archive API only - requires seekable stream

## Project Structure
```
src/SharpCompress/
  ├── Archives/        # IArchive implementations (Zip, Tar, Rar, 7Zip, GZip)
  ├── Readers/         # IReader implementations (forward-only)
  ├── Writers/         # IWriter implementations (forward-only)
  ├── Compressors/     # Low-level compression streams (BZip2, Deflate, LZMA, etc.)
  ├── Factories/       # Format detection and factory pattern
  ├── Common/          # Shared types (ArchiveType, Entry, Options)
  ├── Crypto/          # Encryption implementations
  └── IO/              # Stream utilities and wrappers

tests/SharpCompress.Test/
  ├── Zip/, Tar/, Rar/, SevenZip/, GZip/, BZip2/  # Format-specific tests
  ├── TestBase.cs      # Base test class with helper methods
  └── TestArchives/    # Test data (not checked into main test project)
```

## Common Pitfalls

1. **Don't mix Archive and Reader APIs** - Archive needs seekable stream, Reader doesn't
2. **Solid archives (Rar, 7Zip)** - Use `ExtractAllEntries()` for best performance, not individual entry extraction
3. **Stream disposal** - Always set `LeaveStreamOpen` explicitly when needed (default is to close)
4. **Tar + non-seekable stream** - Must provide file size or it will throw
5. **Multi-framework differences** - Some features differ between .NET Framework and modern .NET (e.g., Mono.Posix)
6. **Format detection** - Use `ReaderFactory.Open()` for auto-detection, test with actual archive files

## Performance Considerations
- Use Reader/Writer APIs for large files to avoid loading entire file in memory
- Leverage async I/O for better scalability
- For solid archives (Rar, 7Zip), sequential extraction is significantly faster
- Consider compression level trade-offs when writing (speed vs size)

## References
- [FORMATS.md](../FORMATS.md) - Complete format support matrix
- [USAGE.md](../USAGE.md) - API usage examples
- [AGENTS.md](../AGENTS.md) - Detailed coding conventions
