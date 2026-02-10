# SharpCompress Architecture Guide

This guide explains the internal architecture and design patterns of SharpCompress for contributors.

## Overview

SharpCompress is organized into three main layers:

```
┌─────────────────────────────────────────┐
│     User-Facing APIs (Top Layer)        │
│  Archive, Reader, Writer Factories      │
├─────────────────────────────────────────┤
│     Format-Specific Implementations     │
│  ZipArchive, TarReader, GZipWriter,     │
│  RarArchive, SevenZipArchive, etc.      │
├─────────────────────────────────────────┤
│     Compression & Crypto (Bottom Layer) │
│  Deflate, LZMA, BZip2, AES, CRC32       │
└─────────────────────────────────────────┘
```

---

## Directory Structure

### `src/SharpCompress/`

#### `Archives/` - Archive Implementations
Contains `IArchive` implementations for seekable, random-access APIs.

**Key Files:**
- `AbstractArchive.cs` - Base class for all archives
- `IArchive.cs` - Archive interface definition
- `ArchiveFactory.cs` - Factory for opening archives
- Format-specific: `ZipArchive.cs`, `TarArchive.cs`, `RarArchive.cs`, `SevenZipArchive.cs`, `GZipArchive.cs`

**Use Archive API when:**
- Stream is seekable (file, memory)
- Need random access to entries
- Archive fits in memory
- Simplicity is important

#### `Readers/` - Reader Implementations
Contains `IReader` implementations for forward-only, non-seekable APIs.

**Key Files:**
- `AbstractReader.cs` - Base reader class
- `IReader.cs` - Reader interface
- `ReaderFactory.cs` - Auto-detection factory
- `ReaderOptions.cs` - Configuration for readers
- Format-specific: `ZipReader.cs`, `TarReader.cs`, `GZipReader.cs`, `RarReader.cs`, etc.

**Use Reader API when:**
- Stream is non-seekable (network, pipe, compressed)
- Processing large files
- Memory is limited
- Forward-only processing is acceptable

#### `Writers/` - Writer Implementations
Contains `IWriter` implementations for forward-only writing.

**Key Files:**
- `AbstractWriter.cs` - Base writer class
- `IWriter.cs` - Writer interface
- `WriterFactory.cs` - Factory for creating writers
- `WriterOptions.cs` - Configuration for writers
- Format-specific: `ZipWriter.cs`, `TarWriter.cs`, `GZipWriter.cs`

#### `Factories/` - Format Detection
Factory classes for auto-detecting archive format and creating appropriate readers/writers.

**Key Files:**
- `Factory.cs` - Base factory class
- `IFactory.cs` - Factory interface
- Format-specific: `ZipFactory.cs`, `TarFactory.cs`, `RarFactory.cs`, etc.

**How It Works:**
1. `ReaderFactory.OpenReader(stream)` probes stream signatures
2. Identifies format by magic bytes
3. Creates appropriate reader instance
4. Returns generic `IReader` interface

#### `Common/` - Shared Types
Common types, options, and enumerations used across formats.

**Key Files:**
- `IEntry.cs` - Entry interface (file within archive)
- `Entry.cs` - Entry implementation
- `ArchiveType.cs` - Enum for archive formats
- `CompressionType.cs` - Enum for compression methods
- `ArchiveEncoding.cs` - Character encoding configuration
- `IExtractionOptions.cs` - Extraction configuration exposed through `ReaderOptions`
- Format-specific headers: `Zip/Headers/`, `Tar/Headers/`, `Rar/Headers/`, etc.

#### `Compressors/` - Compression Algorithms
Low-level compression streams implementing specific algorithms.

**Algorithms:**
- `Deflate/` - DEFLATE compression (Zip default)
- `BZip2/` - BZip2 compression
- `LZMA/` - LZMA compression (7Zip, XZ, LZip)
- `PPMd/` - Prediction by Partial Matching (Zip, 7Zip)
- `ZStandard/` - ZStandard compression (decompression only)
- `Xz/` - XZ format (decompression only)
- `Rar/` - RAR-specific unpacking
- `Arj/`, `Arc/`, `Ace/` - Legacy format decompression
- `Filters/` - BCJ/BCJ2 filters for executable compression

**Each Compressor:**
- Implements a `Stream` subclass
- Provides both compression and decompression
- Some are read-only (decompression only)

#### `Crypto/` - Encryption & Hashing
Cryptographic functions and stream wrappers.

**Key Files:**
- `Crc32Stream.cs` - CRC32 calculation wrapper
- `BlockTransformer.cs` - Block cipher transformations
- AES, PKWare, WinZip encryption implementations

#### `IO/` - Stream Utilities
Stream wrappers and utilities.

**Key Classes:**
- `SharpCompressStream` - Base stream class
- `ProgressReportingStream` - Progress tracking wrapper
- `MarkingBinaryReader` - Binary reader with position marks
- `BufferedSubStream` - Buffered read-only substream
- `ReadOnlySubStream` - Read-only view of parent stream
- `NonDisposingStream` - Prevents wrapped stream disposal

---

## Design Patterns

### 1. Factory Pattern

**Purpose:** Auto-detect format and create appropriate reader/writer.

**Example:**
```csharp
// User calls factory
using (var reader = ReaderFactory.OpenReader(stream))  // Returns IReader
{
    while (reader.MoveToNextEntry())
    {
        // Process entry
    }
}

// Behind the scenes:
// 1. Factory.Open() probes stream signatures
// 2. Detects format (Zip, Tar, Rar, etc.)
// 3. Creates appropriate reader (ZipReader, TarReader, etc.)
// 4. Returns as generic IReader interface
```

**Files:**
- `src/SharpCompress/Factories/ReaderFactory.cs`
- `src/SharpCompress/Factories/WriterFactory.cs`
- `src/SharpCompress/Factories/ArchiveFactory.cs`

### 2. Strategy Pattern

**Purpose:** Encapsulate compression algorithms as swappable strategies.

**Example:**
```csharp
// Different compression strategies
CompressionType.Deflate     // DEFLATE
CompressionType.BZip2       // BZip2
CompressionType.LZMA        // LZMA
CompressionType.PPMd        // PPMd

// Writer uses strategy pattern
var archive = ZipArchive.CreateArchive();
archive.SaveTo("output.zip", CompressionType.Deflate);   // Use Deflate
archive.SaveTo("output.bz2", CompressionType.BZip2);    // Use BZip2
```

**Files:**
- `src/SharpCompress/Compressors/` - Strategy implementations

### 3. Decorator Pattern

**Purpose:** Wrap streams with additional functionality.

**Example:**
```csharp
// Progress reporting decorator
var progressStream = new ProgressReportingStream(baseStream, progressReporter);
progressStream.Read(buffer, 0, buffer.Length);  // Reports progress

// Non-disposing decorator
var nonDisposingStream = new NonDisposingStream(baseStream);
using (var compressor = new DeflateStream(nonDisposingStream))
{
    // baseStream won't be disposed when compressor is disposed
}
```

**Files:**
- `src/SharpCompress/IO/ProgressReportingStream.cs`
- `src/SharpCompress/IO/NonDisposingStream.cs`

### 4. Template Method Pattern

**Purpose:** Define algorithm skeleton in base class, let subclasses fill details.

**Example:**
```csharp
// AbstractArchive defines common archive operations
public abstract class AbstractArchive : IArchive
{
    // Template methods
    public virtual void WriteToDirectory(string destinationDirectory)
    {
        // Common extraction logic
        foreach (var entry in Entries)
        {
            // Call subclass method
            entry.WriteToFile(destinationPath);
        }
    }
    
    // Subclasses override format-specific details
    protected abstract Entry CreateEntry(EntryData data);
}
```

**Files:**
- `src/SharpCompress/Archives/AbstractArchive.cs`
- `src/SharpCompress/Readers/AbstractReader.cs`

### 5. Iterator Pattern

**Purpose:** Provide sequential access to entries.

**Example:**
```csharp
// Archive API - provides collection
IEnumerable<IEntry> entries = archive.Entries;
foreach (var entry in entries)
{
    // Random access - entries already in memory
}

// Reader API - provides iterator
IReader reader = ReaderFactory.OpenReader(stream);
while (reader.MoveToNextEntry())
{
    // Forward-only iteration - one entry at a time
    var entry = reader.Entry;
}
```

---

## Key Interfaces

### IArchive - Random Access API

```csharp
public interface IArchive : IDisposable
{
    IEnumerable<IEntry> Entries { get; }
    
    void WriteToDirectory(string destinationDirectory);
    
    IEntry FirstOrDefault(Func<IEntry, bool> predicate);
    
    // ... format-specific methods
}
```

**Implementations:** `ZipArchive`, `TarArchive`, `RarArchive`, `SevenZipArchive`, `GZipArchive`

### IReader - Forward-Only API

```csharp
public interface IReader : IDisposable
{
    IEntry Entry { get; }
    
    bool MoveToNextEntry();
    
    void WriteEntryToDirectory(string destinationDirectory);
    
    Stream OpenEntryStream();
    
    // ... async variants
}
```

**Implementations:** `ZipReader`, `TarReader`, `RarReader`, `GZipReader`, etc.

### IWriter - Writing API

```csharp
public interface IWriter : IDisposable
{
    void Write(string entryPath, Stream source, 
              DateTime? modificationTime = null);
    
    void WriteAll(string sourceDirectory, string searchPattern,
                 SearchOption searchOption);
    
    // ... async variants
}
```

**Implementations:** `ZipWriter`, `TarWriter`, `GZipWriter`

### IEntry - Archive Entry

```csharp
public interface IEntry
{
    string Key { get; }
    uint Size { get; }
    uint CompressedSize { get; }
    bool IsDirectory { get; }
    DateTime? LastModifiedTime { get; }
    CompressionType CompressionType { get; }
    
    void WriteToFile(string fullPath);
    void WriteToStream(Stream destinationStream);
    Stream OpenEntryStream();
    
    // ... async variants
}
```

---

## Adding Support for a New Format

### Step 1: Understand the Format
- Research format specification
- Understand compression/encryption used
- Study existing similar formats in codebase

### Step 2: Create Format Structure Classes

**Create:** `src/SharpCompress/Common/NewFormat/`

```csharp
// Headers and data structures
public class NewFormatHeader
{
    public uint Magic { get; set; }
    public ushort Version { get; set; }
    // ... other fields
    
    public static NewFormatHeader Read(BinaryReader reader)
    {
        // Deserialize from binary
    }
}

public class NewFormatEntry
{
    public string FileName { get; set; }
    public uint CompressedSize { get; set; }
    public uint UncompressedSize { get; set; }
    // ... other fields
}
```

### Step 3: Create Archive Implementation

**Create:** `src/SharpCompress/Archives/NewFormat/NewFormatArchive.cs`

```csharp
public class NewFormatArchive : AbstractArchive
{
    private NewFormatHeader _header;
    private List<NewFormatEntry> _entries;
    
    public static NewFormatArchive OpenArchive(Stream stream)
    {
        var archive = new NewFormatArchive();
        archive._header = NewFormatHeader.Read(stream);
        archive.LoadEntries(stream);
        return archive;
    }
    
    public override IEnumerable<IEntry> Entries => _entries.Select(e => new Entry(e));
    
    protected override Stream OpenEntryStream(Entry entry)
    {
        // Return decompressed stream for entry
    }
    
    // ... other abstract method implementations
}
```

### Step 4: Create Reader Implementation

**Create:** `src/SharpCompress/Readers/NewFormat/NewFormatReader.cs`

```csharp
public class NewFormatReader : AbstractReader
{
    private NewFormatHeader _header;
    private BinaryReader _reader;
    
    public NewFormatReader(Stream stream)
    {
        _reader = new BinaryReader(stream);
        _header = NewFormatHeader.Read(_reader);
    }
    
    public override bool MoveToNextEntry()
    {
        // Read next entry header
        if (!_reader.BaseStream.CanRead) return false;
        
        var entryData = NewFormatEntry.Read(_reader);
        // ... set this.Entry
        return entryData != null;
    }
    
    // ... other abstract method implementations
}
```

### Step 5: Create Factory

**Create:** `src/SharpCompress/Factories/NewFormatFactory.cs`

```csharp
public class NewFormatFactory : Factory, IArchiveFactory, IReaderFactory
{
    // Archive format magic bytes (signature)
    private static readonly byte[] NewFormatSignature = new byte[] { 0x4E, 0x46 };  // "NF"
    
    public static NewFormatFactory Instance { get; } = new();
    
    public IArchive CreateArchive(Stream stream)
        => NewFormatArchive.OpenArchive(stream);
    
    public IReader CreateReader(Stream stream, ReaderOptions options)
        => new NewFormatReader(stream) { Options = options };
    
    public bool Matches(Stream stream, ReadOnlySpan<byte> signature) 
        => signature.StartsWith(NewFormatSignature);
}
```

### Step 6: Register Factory

**Update:** `src/SharpCompress/Factories/ArchiveFactory.cs`

```csharp
private static readonly IFactory[] Factories = 
{
    ZipFactory.Instance,
    TarFactory.Instance,
    RarFactory.Instance,
    SevenZipFactory.Instance,
    GZipFactory.Instance,
    NewFormatFactory.Instance,  // Add here
    // ... other factories
};
```

### Step 7: Add Tests

**Create:** `tests/SharpCompress.Test/NewFormat/NewFormatTests.cs`

```csharp
public class NewFormatTests : TestBase
{
    [Fact]
    public void NewFormat_Extracts_Successfully()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "archive.newformat");
        using (var archive = NewFormatArchive.OpenArchive(archivePath))
        {
            archive.WriteToDirectory(SCRATCH_FILES_PATH);
            // Assert extraction
        }
    }
    
    [Fact]
    public void NewFormat_Reader_Works()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "archive.newformat");
        using (var stream = File.OpenRead(archivePath))
        using (var reader = new NewFormatReader(stream))
        {
            Assert.True(reader.MoveToNextEntry());
            Assert.NotNull(reader.Entry);
        }
    }
}
```

### Step 8: Add Test Archives

Place test files in `tests/TestArchives/Archives/NewFormat/` directory.

### Step 9: Document

Update `docs/FORMATS.md` with format support information.

---

## Compression Algorithm Implementation

### Creating a New Compression Stream

**Example:** Creating `CustomStream` for a custom compression algorithm

```csharp
public class CustomStream : Stream
{
    private readonly Stream _baseStream;
    private readonly bool _leaveOpen;
    
    public CustomStream(Stream baseStream, bool leaveOpen = false)
    {
        _baseStream = baseStream;
        _leaveOpen = leaveOpen;
    }
    
    public override int Read(byte[] buffer, int offset, int count)
    {
        // Decompress data from _baseStream into buffer
        // Return number of decompressed bytes
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        // Compress data from buffer into _baseStream
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _baseStream?.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

---

## Stream Handling Best Practices

### Disposal Pattern

```csharp
// Correct: Nested using blocks
using (var fileStream = File.OpenRead("archive.zip"))
using (var archive = ZipArchive.OpenArchive(fileStream))
{
    archive.WriteToDirectory(@"C:\output");
}
// Both archive and fileStream properly disposed

// Correct: Using with options
var options = new ReaderOptions { LeaveStreamOpen = true };
var stream = File.OpenRead("archive.zip");
using (var archive = ZipArchive.OpenArchive(stream, options))
{
    archive.WriteToDirectory(@"C:\output");
}
stream.Dispose();  // Manually dispose if LeaveStreamOpen = true
```

### NonDisposingStream Wrapper

```csharp
// Prevent unwanted stream closure
var baseStream = File.OpenRead("data.bin");
var nonDisposing = new NonDisposingStream(baseStream);

using (var compressor = new DeflateStream(nonDisposing))
{
    // Compressor won't close baseStream when disposed
}

// baseStream still usable
baseStream.Position = 0;  // Works
baseStream.Dispose();  // Manual disposal
```

---

## Performance Considerations

### Memory Efficiency

1. **Avoid loading entire archive in memory** - Use Reader API for large files
2. **Process entries sequentially** - Especially for solid archives
3. **Use appropriate buffer sizes** - Larger buffers for network I/O
4. **Dispose streams promptly** - Free resources when done

### Algorithm Selection

1. **Archive API** - Fast for small archives with random access
2. **Reader API** - Efficient for large files or streaming
3. **Solid archives** - Sequential extraction much faster
4. **Compression levels** - Trade-off between speed and size

---

## Testing Guidelines

### Test Coverage

1. **Happy path** - Normal extraction works
2. **Edge cases** - Empty archives, single file, many files
3. **Corrupted data** - Handle gracefully
4. **Error cases** - Missing passwords, unsupported compression
5. **Async operations** - Both sync and async code paths

### Test Archives

- Use `tests/TestArchives/` for test data
- Create format-specific subdirectories
- Include encrypted, corrupted, and edge case archives
- Don't recreate existing archives

### Test Patterns

```csharp
[Fact]
public void Archive_Extraction_Works()
{
    // Arrange
    var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "test.zip");
    
    // Act
    using (var archive = ZipArchive.OpenArchive(testArchive))
    {
        archive.WriteToDirectory(SCRATCH_FILES_PATH);
    }
    
    // Assert
    Assert.True(File.Exists(Path.Combine(SCRATCH_FILES_PATH, "file.txt")));
}
```

---

## Related Documentation

- [AGENTS.md](../AGENTS.md) - Development guidelines
- [FORMATS.md](FORMATS.md) - Supported formats
