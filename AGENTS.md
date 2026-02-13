---
description: 'Guidelines for building SharpCompress - A C# compression library'
applyTo: '**/*.cs'
---

# SharpCompress Development

## About SharpCompress
SharpCompress is a pure C# compression library supporting multiple archive formats (Zip, Tar, GZip, BZip2, 7Zip, Rar, LZip, XZ, ZStandard). The project currently targets .NET Framework 4.8, .NET Standard 2.0, .NET 8.0, and .NET 10.0. The library provides both seekable Archive APIs and forward-only Reader/Writer APIs for streaming scenarios.

## C# Instructions
- Use language features supported by the current project toolchain (`LangVersion=latest`) and existing codebase patterns.
- Add comments for non-obvious logic and important design decisions; avoid redundant comments.
- Follow the existing code style and patterns in the codebase.

## General Instructions
- **Do not commit or stage changes unless the user explicitly asks for it.**
- Make only high confidence suggestions when reviewing code changes.
- Write code with good maintainability practices, including comments on why certain design decisions were made.
- Handle edge cases and write clear exception handling.
- For libraries or external dependencies, mention their usage and purpose in comments.
- Preserve backward compatibility when making changes to public APIs.

### Workspace Hygiene
- Do not edit generated or machine-local files unless required for the task (for example: `bin/`, `obj/`, `*.csproj.user`).
- Avoid broad formatting-only diffs in unrelated files.

## Naming Conventions

- Follow PascalCase for component names, method names, and public members.
- Use camelCase for private fields and local variables.
- Prefix interface names with "I" (e.g., IUserService).

## Code Formatting

**Copilot agents: You MUST run the `format` task after making code changes to ensure consistency.**

- Use CSharpier for code formatting to ensure consistent style across the project
- CSharpier is configured as a local tool in `.config/dotnet-tools.json`

### Commands

1. **Restore tools** (first time only):
   ```bash
   dotnet tool restore
   ```

2. **Check if files are formatted correctly** (doesn't modify files):
   ```bash
   dotnet csharpier check .
   ```
   - Exit code 0: All files are properly formatted
   - Exit code 1: Some files need formatting (will show which files and differences)

3. **Format files** (modifies files):
   ```bash
   dotnet csharpier format .
   ```
   - Formats all files in the project to match CSharpier style
   - Run from project root directory

4. **Configure your IDE** to format on save using CSharpier for the best experience

### Additional Notes
- The project also uses `.editorconfig` for editor settings (indentation, encoding, etc.)
- Let CSharpier handle code style while `.editorconfig` handles editor behavior
- Always run `dotnet csharpier check .` before committing to verify formatting

## Project Setup and Structure

- The project targets multiple frameworks: .NET Framework 4.8, .NET Standard 2.0, .NET 8.0, and .NET 10.0
- Main library is in `src/SharpCompress/`
- Tests are in `tests/SharpCompress.Test/`
- Performance tests are in `tests/SharpCompress.Performance/`
- Test archives are in `tests/TestArchives/`
- Build project is in `build/`
- Use `dotnet build` to build the solution
- Use `dotnet test` to run tests
- Solution file: `SharpCompress.sln`

### Directory Structure
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

tests/
  ├── SharpCompress.Test/         # Unit/integration tests
  ├── SharpCompress.Performance/  # Benchmark tests
  └── TestArchives/               # Test data archives
```

### Factory Pattern
Factory implementations can implement one or more interfaces (`IArchiveFactory`, `IReaderFactory`, `IWriterFactory`) depending on format capabilities:
- `ArchiveFactory.OpenArchive()` - Opens archive API objects from seekable streams/files
- `ArchiveFactory.OpenAsyncArchive()` - Opens async archive API objects for async archive use cases
- `ReaderFactory.OpenReader()` - Auto-detects and opens forward-only readers
- `ReaderFactory.OpenAsyncReader()` - Auto-detects and opens forward-only async readers
- `WriterFactory.OpenWriter()` - Creates a writer for a specified `ArchiveType`
- `WriterFactory.OpenAsyncWriter()` - Creates an async writer for async write scenarios
- Factories located in: `src/SharpCompress/Factories/`

## Nullable Reference Types

- Declare variables non-nullable, and check for `null` at entry points.
- Always use `is null` or `is not null` instead of `== null` or `!= null`.
- Trust the C# null annotations and don't add null checks when the type system says a value cannot be null.

## SharpCompress-Specific Guidelines

### Supported Formats
SharpCompress supports multiple archive and compression formats:
- **Archive Formats**: Zip, Tar, 7Zip, Rar (read-only)
- **Compression**: DEFLATE, BZip2, LZMA/LZMA2, PPMd, ZStandard (decompress only), Deflate64 (decompress only)
- **Combined Formats**: Tar.GZip, Tar.BZip2, Tar.LZip, Tar.XZ, Tar.ZStandard
- See [docs/FORMATS.md](docs/FORMATS.md) for complete format support matrix

### Stream Handling Rules
- **Disposal**: As of version 0.21, SharpCompress closes wrapped streams by default
- Use `ReaderOptions` or `WriterOptions` with `LeaveStreamOpen = true` to control stream disposal
- Use `NonDisposingStream` wrapper when working with compression streams directly to prevent disposal
- Always dispose of readers, writers, and archives in `using` blocks
- For forward-only operations, use Reader/Writer APIs; for random access, use Archive APIs

### Async/Await Patterns
- All I/O operations support async/await with `CancellationToken`
- Async methods follow the naming convention: `MethodNameAsync`
- For async archive scenarios, prefer `ArchiveFactory.OpenAsyncArchive(...)` over sync `OpenArchive(...)`.
- For async forward-only read scenarios, prefer `ReaderFactory.OpenAsyncReader(...)` over sync `OpenReader(...)`.
- For async write scenarios, prefer `WriterFactory.OpenAsyncWriter(...)` over sync `OpenWriter(...)`.
- Key async methods:
  - `WriteEntryToAsync` - Extract entry asynchronously
  - `WriteAllToDirectoryAsync` - Extract all entries asynchronously
  - `WriteAsync` - Write entry asynchronously
  - `WriteAllAsync` - Write directory asynchronously
  - `OpenEntryStreamAsync` - Open entry stream asynchronously
- Always provide `CancellationToken` parameter in async methods

### Archive APIs vs Reader/Writer APIs
- **Archive API**: Use for random access with seekable streams (e.g., `ZipArchive`, `TarArchive`)
- **Reader API**: Use for forward-only reading on non-seekable streams (e.g., `ZipReader`, `TarReader`)
- **Writer API**: Use for forward-only writing on streams (e.g., `ZipWriter`, `TarWriter`)
- 7Zip only supports Archive API due to format limitations

### Tar-Specific Considerations
- Tar format requires file size in the header
- If no size is specified to TarWriter and the stream is not seekable, an exception will be thrown
- Tar combined with compression (GZip, BZip2, LZip, XZ) is supported

### Zip-Specific Considerations
- Supports Zip64 for large files (seekable streams only)
- Supports PKWare and WinZip AES encryption
- Multiple compression methods: None, Shrink, Reduce, Implode, DEFLATE, Deflate64, BZip2, LZMA, PPMd
- Encrypted LZMA is not supported

### Performance Considerations
- For large files, use Reader/Writer APIs with non-seekable streams to avoid loading entire file in memory
- Leverage async I/O for better scalability
- Consider compression level trade-offs (speed vs. size)
- Use appropriate buffer sizes for stream operations

## Testing

- Always include test cases for critical paths of the application.
- Test with multiple archive formats when making changes to core functionality.
- Include tests for both Archive and Reader/Writer APIs when applicable.
- Test async operations with cancellation tokens.
- Do not emit "Act", "Arrange" or "Assert" comments.
- Copy existing style in nearby files for test method names and capitalization.
- Use test archives from `tests/TestArchives` directory for consistency.
- Test stream disposal and `LeaveStreamOpen` behavior.
- Test edge cases: empty archives, large files, corrupted archives, encrypted archives.

### Validation Expectations
- Run targeted tests for the changed area first.
- Run `dotnet csharpier format .` after code edits.
- Run `dotnet csharpier check .` before handing off changes.

### Test Organization
- Base class: `TestBase` - Provides `TEST_ARCHIVES_PATH`, `SCRATCH_FILES_PATH`, temp directory management
- Framework: xUnit with AwesomeAssertions
- Test archives: `tests/TestArchives/` - Use existing archives, don't create new ones unnecessarily
- Match naming style of nearby test files

### Public API Change Checklist
- Preserve existing public method signatures and behavior when possible.
- If a breaking change is unavoidable, document it and provide a migration path.
- Add or update tests that cover backward compatibility expectations.

### Stream Ownership and Position Checklist
- Verify `LeaveStreamOpen` behavior for externally owned streams.
- Validate behavior for both seekable and non-seekable streams.
- Ensure stream position assumptions are explicit and tested.

## Common Pitfalls

1. **Don't mix Archive and Reader APIs** - Archive needs seekable stream, Reader doesn't
2. **Don't mix sync and async open paths** - For async workflows use `OpenAsyncArchive`/`OpenAsyncReader`/`OpenAsyncWriter`, not `OpenArchive`/`OpenReader`/`OpenWriter`
3. **Solid archives (Rar, 7Zip)** - Use `ExtractAllEntries()` for best performance, not individual entry extraction
4. **Stream disposal** - Always set `LeaveStreamOpen` explicitly when needed (default is to close)
5. **Tar + non-seekable stream** - Must provide file size or it will throw
6. **Format detection** - Use `ReaderFactory.OpenReader()` / `ReaderFactory.OpenAsyncReader()` for auto-detection, test with actual archive files
