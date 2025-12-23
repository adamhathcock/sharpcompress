# Plan: Modernize SharpCompress Public API

Based on comprehensive analysis, the API has several inconsistencies around factory patterns, async support, format capabilities, and options classes. Most improvements can be done incrementally without breaking changes.

## Steps

1. **Standardize factory patterns** by deprecating format-specific static `Open` methods in [Archives/Zip/ZipArchive.cs](src/SharpCompress/Archives/Zip/ZipArchive.cs), [Archives/Tar/TarArchive.cs](src/SharpCompress/Archives/Tar/TarArchive.cs), etc. in favor of centralized [Factories/ArchiveFactory.cs](src/SharpCompress/Factories/ArchiveFactory.cs)

2. **Complete async implementation** in [Writers/Zip/ZipWriter.cs](src/SharpCompress/Writers/Zip/ZipWriter.cs) and other writers that currently use sync-over-async, implementing true async I/O throughout the writer hierarchy

3. **Unify options classes** by making [Common/ExtractionOptions.cs](src/SharpCompress/Common/ExtractionOptions.cs) inherit from `OptionsBase` and adding progress reporting to extraction methods consistently

4. **Clarify GZip semantics** in [Archives/GZip/GZipArchive.cs](src/SharpCompress/Archives/GZip/GZipArchive.cs) by adding XML documentation explaining single-entry limitation and relationship to GZip compression used in Tar.gz

## Further Considerations

1. **Breaking changes roadmap** - Should we plan a major version (2.0) to remove deprecated factory methods, clean up `ArchiveType` enum (remove Arc/Arj or add full support), and consolidate naming patterns?

2. **Progress reporting consistency** - Should `IProgress<ArchiveExtractionProgress<IEntry>>` be added to all extraction extension methods or consolidated into options classes?

## Detailed Analysis

### Factory Pattern Issues

Three different factory patterns exist with overlapping functionality:

1. **Static Factories**: ArchiveFactory, ReaderFactory, WriterFactory
2. **Instance Factories**: IArchiveFactory, IReaderFactory, IWriterFactory
3. **Format-specific static methods**: Each archive class has static `Open` methods

**Example confusion:**
```csharp
// Three ways to open a Zip archive - which is recommended?
var archive1 = ArchiveFactory.Open("file.zip");
var archive2 = ZipArchive.Open("file.zip");
var archive3 = ArchiveFactory.AutoFactory.Open(fileInfo, options);
```

### Async Support Gaps

Base `IWriter` interface has async methods, but writer implementations provide minimal async support. Most writers just call synchronous methods:

```csharp
public virtual async Task WriteAsync(...)
{
    // Default implementation calls synchronous version
    Write(filename, source, modificationTime);
    await Task.CompletedTask.ConfigureAwait(false);
}
```

Real async implementations only in:
- `TarWriter` - Proper async implementation
- Most other writers use sync-over-async

### GZip Archive Special Case

GZip is treated as both a compression format and an archive format, but only supports single-entry archives:

```csharp
protected override GZipArchiveEntry CreateEntryInternal(...)
{
    if (Entries.Any())
    {
        throw new InvalidFormatException("Only one entry is allowed in a GZip Archive");
    }
    // ...
}
```

### Options Class Hierarchy

```
OptionsBase (LeaveStreamOpen, ArchiveEncoding)
  ├─ ReaderOptions (LookForHeader, Password, DisableCheckIncomplete, BufferSize, ExtensionHint, Progress)
  ├─ WriterOptions (CompressionType, CompressionLevel, Progress)
  │   ├─ ZipWriterOptions (ArchiveComment, UseZip64)
  │   ├─ TarWriterOptions (FinalizeArchiveOnClose, HeaderFormat)
  │   └─ GZipWriterOptions (no additional properties)
  └─ ExtractionOptions (standalone - Overwrite, ExtractFullPath, PreserveFileTime, PreserveAttributes)
```

**Issues:**
- `ExtractionOptions` doesn't inherit from `OptionsBase` - no encoding support during extraction
- Progress reporting inconsistency between readers and extraction
- Obsolete properties (`ChecksumIsValid`, `Version`) with unclear migration path

### Implementation Priorities

**High Priority (Non-Breaking):**
1. Add API usage guide (Archive vs Reader, factory recommendations, async best practices)
2. Fix progress reporting consistency
3. Complete async implementation in writers

**Medium Priority (Next Major Version):**
1. Unify factory pattern - deprecate format-specific static `Open` methods
2. Clean up options classes - make `ExtractionOptions` inherit from `OptionsBase`
3. Clarify archive types - remove Arc/Arj from `ArchiveType` enum or add full support
4. Standardize naming across archive types

**Low Priority:**
1. Add BZip2 archive support similar to GZipArchive
2. Complete obsolete property cleanup with migration guide

### Backward Compatibility Strategy

**Safe (Non-Breaking) Changes:**
- Add new methods to interfaces (use default implementations)
- Add new options properties (with defaults)
- Add new factory methods
- Improve async implementations
- Add progress reporting support

**Breaking Changes to Avoid:**
- ❌ Removing format-specific `Open` methods (deprecate instead)
- ❌ Changing `LeaveStreamOpen` default (currently `true`)
- ❌ Removing obsolete properties before major version bump
- ❌ Changing return types or signatures of existing methods

**Deprecation Pattern:**
- Use `[Obsolete]` for one major version
- Use `[EditorBrowsable(EditorBrowsableState.Never)]` in next major version
- Remove in following major version
