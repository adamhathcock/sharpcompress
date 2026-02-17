# SharpCompress Interface Architecture

This document describes the interface hierarchy for archives, readers, and writers in SharpCompress.

## Overview

SharpCompress provides three main APIs for working with compressed archives:

| API | Use Case | Stream Requirements | Access Pattern |
|-----|----------|---------------------|----------------|
| **Archive** | Random access to entries | Seekable stream | Load all metadata, extract any entry |
| **Reader** | Sequential streaming | Non-seekable OK | Forward-only, memory efficient |
| **Writer** | Creating archives | Non-seekable OK | Forward-only writes |

---

## Core Interface Hierarchies

### Archive Interfaces

```mermaid
classDiagram
    direction TB
    
    class IDisposable {
        <<interface>>
        +Dispose()
    }
    
    class IAsyncDisposable {
        <<interface>>
        +DisposeAsync()
    }
    
    class IArchive {
        <<interface>>
        +Entries : IEnumerable~IArchiveEntry~
        +Volumes : IEnumerable~IVolume~
        +Type : ArchiveType
        +ReaderOptions : ReaderOptions
        +IsSolid : bool
        +IsComplete : bool
        +IsEncrypted : bool
        +TotalSize : long
        +TotalUncompressedSize : long
        +ExtractAllEntries() IReader
    }
    
    class IAsyncArchive {
        <<interface>>
        +EntriesAsync : IAsyncEnumerable~IArchiveEntry~
        +VolumesAsync : IAsyncEnumerable~IVolume~
        +Type : ArchiveType
        +ExtractAllEntriesAsync() ValueTask~IAsyncReader~
        +IsSolidAsync() ValueTask~bool~
        +IsCompleteAsync() ValueTask~bool~
        +IsEncryptedAsync() ValueTask~bool~
        +TotalSizeAsync() ValueTask~long~
        +TotalUncompressedSizeAsync() ValueTask~long~
    }
    
    class IExtractableArchive {
        <<interface>>
        +Entries : IEnumerable~IExtractableArchiveEntry~
    }
    
    class IExtractableAsyncArchive {
        <<interface>>
        +EntriesAsync : IAsyncEnumerable~IExtractableArchiveEntry~
    }
    
    class IWritableArchiveCommon {
        <<interface>>
        +PauseEntryRebuilding() IDisposable
    }
    
    class IWritableArchive {
        <<interface>>
        +AddEntry(key, source, closeStream, size, modified) IArchiveEntry
        +AddDirectoryEntry(key, modified) IArchiveEntry
        +RemoveEntry(entry)
    }
    
    class IWritableArchive~TOptions~ {
        <<interface>>
        +SaveTo(stream, options)
    }
    
    class IWritableAsyncArchive {
        <<interface>>
        +AddEntryAsync(...) ValueTask~IArchiveEntry~
        +AddDirectoryEntryAsync(...) ValueTask~IArchiveEntry~
        +RemoveEntryAsync(entry) ValueTask
    }
    
    class IWritableAsyncArchive~TOptions~ {
        <<interface>>
        +SaveToAsync(stream, options, ct) ValueTask
    }

    IDisposable <|-- IArchive
    IAsyncDisposable <|-- IAsyncArchive
    
    IArchive <|-- IExtractableArchive
    IAsyncArchive <|-- IExtractableAsyncArchive
    
    IArchive <|-- IWritableArchive
    IWritableArchiveCommon <|-- IWritableArchive
    IWritableArchive <|-- IWritableArchive~TOptions~
    
    IAsyncArchive <|-- IWritableAsyncArchive
    IWritableArchiveCommon <|-- IWritableAsyncArchive
    IWritableAsyncArchive <|-- IWritableAsyncArchive~TOptions~
```

### Reader Interfaces

```mermaid
classDiagram
    direction TB
    
    class IDisposable {
        <<interface>>
        +Dispose()
    }
    
    class IAsyncDisposable {
        <<interface>>
        +DisposeAsync()
    }
    
    class IReader {
        <<interface>>
        +ArchiveType : ArchiveType
        +Entry : IEntry
        +Cancelled : bool
        +MoveToNextEntry() bool
        +WriteEntryTo(stream)
        +OpenEntryStream() EntryStream
        +Cancel()
    }
    
    class IAsyncReader {
        <<interface>>
        +ArchiveType : ArchiveType
        +Entry : IEntry
        +Cancelled : bool
        +MoveToNextEntryAsync(ct) ValueTask~bool~
        +WriteEntryToAsync(stream, ct) ValueTask
        +OpenEntryStreamAsync(ct) ValueTask~EntryStream~
        +Cancel()
    }
    
    IDisposable <|-- IReader
    IAsyncDisposable <|-- IAsyncReader
```

### Writer Interfaces

```mermaid
classDiagram
    direction TB
    
    class IDisposable {
        <<interface>>
        +Dispose()
    }
    
    class IAsyncDisposable {
        <<interface>>
        +DisposeAsync()
    }
    
    class IWriter {
        <<interface>>
        +WriterType : ArchiveType
        +Write(filename, source, modificationTime)
        +WriteDirectory(directoryName, modificationTime)
    }
    
    class IAsyncWriter {
        <<interface>>
        +WriterType : ArchiveType
        +WriteAsync(filename, source, modificationTime, ct) ValueTask
        +WriteDirectoryAsync(directoryName, modificationTime, ct) ValueTask
    }
    
    IDisposable <|-- IWriter
    IDisposable <|-- IAsyncWriter
    IAsyncDisposable <|-- IAsyncWriter
```

---

## Entry Interfaces

```mermaid
classDiagram
    direction TB
    
    class IEntry {
        <<interface>>
        +Key : string?
        +Size : long
        +CompressedSize : long
        +CompressionType : CompressionType
        +Crc : long
        +IsDirectory : bool
        +IsEncrypted : bool
        +IsSolid : bool
        +IsSplitAfter : bool
        +LinkTarget : string?
        +Attrib : int?
        +ArchivedTime : DateTime?
        +CreatedTime : DateTime?
        +LastAccessedTime : DateTime?
        +LastModifiedTime : DateTime?
        +VolumeIndexFirst : int
        +VolumeIndexLast : int
        +Options : IReaderOptions
    }
    
    class IArchiveEntry {
        <<interface>>
        +IsComplete : bool
        +Archive : IArchive
    }
    
    class IExtractableArchiveEntry {
        <<interface>>
        +OpenEntryStream() Stream
        +OpenEntryStreamAsync(ct) ValueTask~Stream~
    }
    
    IEntry <|-- IArchiveEntry
    IArchiveEntry <|-- IExtractableArchiveEntry
```

---

## Factory Interfaces

```mermaid
classDiagram
    direction TB
    
    class IFactory {
        <<interface>>
        +Name : string
        +KnownArchiveType : ArchiveType?
        +GetSupportedExtensions() IEnumerable~string~
        +IsArchive(stream, password) bool
        +IsArchiveAsync(stream, password, ct) ValueTask~bool~
        +GetFilePart(index, part1) FileInfo?
    }
    
    class IArchiveFactory {
        <<interface>>
        +OpenArchive(stream, options) IArchive
        +OpenArchive(fileInfo, options) IArchive
        +OpenAsyncArchive(stream, options, ct) ValueTask~IAsyncArchive~
        +OpenAsyncArchive(fileInfo, options, ct) ValueTask~IAsyncArchive~
    }
    
    class IMultiArchiveFactory {
        <<interface>>
        +OpenArchive(streams, options) IArchive
        +OpenArchive(fileInfos, options) IArchive
        +OpenAsyncArchive(streams, options) IAsyncArchive
        +OpenAsyncArchive(fileInfos, options) IAsyncArchive
    }
    
    class IReaderFactory {
        <<interface>>
        +OpenReader(stream, options) IReader
        +OpenAsyncReader(stream, options, ct) ValueTask~IAsyncReader~
    }
    
    class IWriterFactory {
        <<interface>>
        +OpenWriter(stream, options) IWriter
        +OpenAsyncWriter(stream, options, ct) IAsyncWriter
    }
    
    IFactory <|-- IArchiveFactory
    IFactory <|-- IMultiArchiveFactory
    IFactory <|-- IReaderFactory
    IFactory <|-- IWriterFactory
```

---

## Format-Specific Interfaces

Each archive format has marker interfaces that inherit from the core interfaces:

### Archive Formats

```mermaid
classDiagram
    direction LR
    
    class IArchive {
        <<interface>>
    }
    class IAsyncArchive {
        <<interface>>
    }
    class IExtractableArchive {
        <<interface>>
    }
    class IExtractableAsyncArchive {
        <<interface>>
    }
    
    class IZipArchive {
        <<interface>>
    }
    class IZipAsyncArchive {
        <<interface>>
    }
    class ITarArchive {
        <<interface>>
    }
    class ITarAsyncArchive {
        <<interface>>
    }
    class IGZipArchive {
        <<interface>>
    }
    class IGZipAsyncArchive {
        <<interface>>
    }
    class ISevenZipArchive {
        <<interface>>
    }
    class ISevenZipAsyncArchive {
        <<interface>>
    }
    
    IExtractableArchive <|-- IZipArchive
    IExtractableAsyncArchive <|-- IZipAsyncArchive
    
    IExtractableArchive <|-- ITarArchive
    IExtractableAsyncArchive <|-- ITarAsyncArchive
    
    IExtractableArchive <|-- IGZipArchive
    IExtractableAsyncArchive <|-- IGZipAsyncArchive
    
    IArchive <|-- ISevenZipArchive : "No random extraction"
    IAsyncArchive <|-- ISevenZipAsyncArchive : "No random extraction"
```

> **Note:** 7Zip archives implement `IArchive` directly (not `IExtractableArchive`) because the format requires sequential decompression - you cannot extract individual entries randomly.

### Reader Formats

```mermaid
classDiagram
    direction LR
    
    class IReader {
        <<interface>>
    }
    class IAsyncReader {
        <<interface>>
    }
    
    class IZipReader {
        <<interface>>
    }
    class IZipAsyncReader {
        <<interface>>
    }
    class ITarReader {
        <<interface>>
    }
    class ITarAsyncReader {
        <<interface>>
    }
    class IGZipReader {
        <<interface>>
    }
    class IRarReader {
        <<interface>>
    }
    class IAceReader {
        <<interface>>
    }
    class IArcReader {
        <<interface>>
    }
    class IArjReader {
        <<interface>>
    }
    class ILzwReader {
        <<interface>>
    }
    
    IReader <|-- IZipReader
    IAsyncReader <|-- IZipAsyncReader
    IReader <|-- ITarReader
    IAsyncReader <|-- ITarAsyncReader
    IReader <|-- IGZipReader
    IReader <|-- IRarReader
    IReader <|-- IAceReader
    IReader <|-- IArcReader
    IReader <|-- IArjReader
    IReader <|-- ILzwReader
```

---

## Relationship Overview

```mermaid
flowchart TB
    subgraph "Entry Points"
        AF[ArchiveFactory]
        RF[ReaderFactory]
        WF[WriterFactory]
    end
    
    subgraph "Archives (Random Access)"
        IA[IArchive]
        IAA[IAsyncArchive]
        IEA[IExtractableArchive]
        IWA[IWritableArchive]
    end
    
    subgraph "Readers (Sequential)"
        IR[IReader]
        IAR[IAsyncReader]
    end
    
    subgraph "Writers (Sequential)"
        IW[IWriter]
        IAW[IAsyncWriter]
    end
    
    subgraph "Entries"
        IE[IEntry]
        IAE[IArchiveEntry]
        IEAE[IExtractableArchiveEntry]
    end
    
    AF -->|OpenArchive| IA
    AF -->|OpenAsyncArchive| IAA
    RF -->|OpenReader| IR
    RF -->|OpenAsyncReader| IAR
    WF -->|OpenWriter| IW
    WF -->|OpenAsyncWriter| IAW
    
    IA -->|ExtractAllEntries| IR
    IAA -->|ExtractAllEntriesAsync| IAR
    
    IA -->|Entries| IAE
    IEA -->|Entries| IEAE
    IR -->|Entry| IE
    
    IEAE -->|OpenEntryStream| Stream
    IR -->|OpenEntryStream| Stream
```

---

## API Selection Guide

### When to use Archive API

- You have a **seekable stream** (file or memory with full access)
- Need **random access** to specific entries
- Want to **list all entries** before extracting
- Need to **modify** the archive (add/remove entries)

```csharp
// Sync
using var archive = ArchiveFactory.OpenArchive(stream);
foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
{
    entry.OpenEntryStream(); // Random access
}

// Async
await using var archive = await ArchiveFactory.OpenAsyncArchive(stream);
await foreach (var entry in archive.EntriesAsync)
{
    await entry.OpenEntryStreamAsync();
}
```

### When to use Reader API

- Working with **non-seekable streams** (network, pipes)
- Processing **large archives** where memory is a concern
- Only need **sequential forward** access
- Processing entries **as they arrive**

```csharp
// Sync
using var reader = ReaderFactory.OpenReader(stream);
while (reader.MoveToNextEntry())
{
    reader.WriteEntryTo(outputStream);
}

// Async
await using var reader = await ReaderFactory.OpenAsyncReader(stream);
while (await reader.MoveToNextEntryAsync())
{
    await reader.WriteEntryToAsync(outputStream);
}
```

### When to use Writer API

- **Creating** new archives
- **Streaming** content into archives
- Writing to **non-seekable** output streams

```csharp
// Sync
using var writer = WriterFactory.OpenWriter(outputStream, options);
writer.Write("file.txt", contentStream, DateTime.Now);

// Async
await using var writer = WriterFactory.OpenAsyncWriter(outputStream, options);
await writer.WriteAsync("file.txt", contentStream, DateTime.Now);
```

---

## Interface Capabilities by Format

| Format | IArchive | IExtractableArchive | IReader | IWriter |
|--------|----------|---------------------|---------|---------|
| **Zip** | ✅ | ✅ | ✅ | ✅ |
| **Tar** | ✅ | ✅ | ✅ | ✅ |
| **GZip** | ✅ | ✅ | ✅ | ✅ |
| **7Zip** | ✅ | ❌ (sequential only) | ✅ (sequential only) | ❌ |
| **Rar** | ✅ | ✅ | ✅ (read-only) | ❌ |
| **Ace** | ❌ | ❌ | ✅ (read-only) | ❌ |
| **Arc** | ❌ | ❌ | ✅ (read-only) | ❌ |
| **Arj** | ❌ | ❌ | ✅ (read-only) | ❌ |
| **Lzw** | ❌ | ❌ | ✅ (read-only) | ❌ |

---

## Key Design Decisions

1. **Sync/Async Split**: Each major interface has both sync (`IArchive`) and async (`IAsyncArchive`) variants for different use cases.

2. **IExtractableArchive**: Separates archives that support random entry extraction from those that don't (7Zip requires sequential processing).

3. **Marker Interfaces**: Format-specific interfaces (`IZipArchive`, `ITarReader`) are markers that allow type-safe casting when format-specific features are needed.

4. **Factory Pattern**: All instances are created through factories (`ArchiveFactory`, `ReaderFactory`, `WriterFactory`) which handle format detection and instantiation.

5. **Entry Hierarchy**: `IEntry` → `IArchiveEntry` → `IExtractableArchiveEntry` provides progressive capabilities.
