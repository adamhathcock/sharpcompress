# API Quick Reference

Quick reference for commonly used SharpCompress APIs.

## Factory Methods

### Opening Archives

```csharp
// Auto-detect format
using (var reader = ReaderFactory.OpenReader(stream))
{
    // Works with Zip, Tar, GZip, Rar, 7Zip, etc.
}

// Specific format - Archive API
using (var archive = ZipArchive.OpenArchive("file.zip"))
using (var archive = TarArchive.OpenArchive("file.tar"))
using (var archive = RarArchive.OpenArchive("file.rar"))
using (var archive = SevenZipArchive.OpenArchive("file.7z"))
using (var archive = GZipArchive.OpenArchive("file.gz"))

// With fluent options (preferred)
var options = ReaderOptions.ForEncryptedArchive("password")
    .WithArchiveEncoding(new ArchiveEncoding { Default = Encoding.GetEncoding(932) });
using (var archive = ZipArchive.OpenArchive("encrypted.zip", options))

// Alternative: object initializer
var options2 = new ReaderOptions
{
    Password = "password",
    LeaveStreamOpen = true,
    ArchiveEncoding = new ArchiveEncoding { Default = Encoding.GetEncoding(932) }
};
```

```csharp
// Auto-detect and open with the Archive API
using (var archive = ArchiveFactory.OpenArchive("archive.zip"))
{
    foreach (var entry in archive.Entries)
    {
        Console.WriteLine(entry.Key);
    }
}

// Detect before opening
if (ArchiveFactory.IsArchive("archive.zip", out var archiveType))
{
    Console.WriteLine($"Detected {archiveType}");
}

// Detect capabilities before choosing Archive API vs Reader API
var info = ArchiveFactory.GetArchiveInformation("archive.arc");
if (info is not null)
{
    Console.WriteLine($"Type: {info.Type}");
    Console.WriteLine($"Supports random access: {info.SupportsRandomAccess}");
}

var asyncInfo = await ArchiveFactory.GetArchiveInformationAsync(
    "archive.zip",
    cancellationToken
);

// Multi-volume archives
var parts = ArchiveFactory.GetFileParts("archive.part1.rar")
    .Select(path => new FileInfo(path))
    .ToArray();
using (var archive = ArchiveFactory.OpenArchive(parts))
{
    archive.WriteToDirectory(@"C:\output");
}
```

`ArchiveInformation.SupportsRandomAccess` is `true` when the detected format supports `IArchive` random access. It is `false` for reader-only formats such as Ace, Arc, Arj, and standalone LZW, where `ReaderFactory.OpenReader` should be used instead.

### Creating Archives

```csharp
// Writer Factory
using (var writer = WriterFactory.OpenWriter(stream, ArchiveType.Zip, CompressionType.Deflate))
{
    // Write entries
}

// Specific writer
using (var archive = ZipArchive.CreateArchive())
using (var archive = TarArchive.CreateArchive())
using (var archive = GZipArchive.CreateArchive())

// With fluent options (preferred)
var options = WriterOptions.ForZip()
    .WithCompressionLevel(9)
    .WithLeaveStreamOpen(false)
    .WithBufferSize(131072);
using (var archive = ZipArchive.CreateArchive())
{
    archive.SaveTo("output.zip", options);
}

// Alternative: constructor with object initializer
var options2 = new WriterOptions(CompressionType.Deflate)
{
    CompressionLevel = 9,
    LeaveStreamOpen = false,
    BufferSize = 131072
};
```

`WriterOptions.BufferSize` controls stream copy buffers used while writing archive entries. If it is not set, SharpCompress falls back to `Constants.BufferSize`.

---

## Archive API Methods

### Reading/Extracting

```csharp
using (var archive = ZipArchive.OpenArchive("file.zip"))
{
    // Get all entries
    IEnumerable<IArchiveEntry> entries = archive.Entries;

    // Find specific entry
    var entry = archive.Entries.FirstOrDefault(e => e.Key == "file.txt");

    // Extract all
    archive.WriteToDirectory(@"C:\output");

    // Extract single entry
    var firstEntry = archive.Entries.First();
    firstEntry.WriteToFile(@"C:\output\file.txt");

    // Get entry stream
    using (var stream = entry.OpenEntryStream())
    {
        stream.CopyTo(outputStream);
    }
}

// Async extraction (requires IAsyncArchive)
await using (var asyncArchive = await ZipArchive.OpenAsyncArchive("file.zip"))
{
    // Extract all entries asynchronously
    await asyncArchive.WriteToDirectoryAsync(
        @"C:\output",
        cancellationToken: cancellationToken
    );
}

// Open a specific entry stream asynchronously
await using (var asyncArchive = await ZipArchive.OpenAsyncArchive("file.zip"))
{
    await foreach (var entry in asyncArchive.EntriesAsync)
    {
        using (var stream = await entry.OpenEntryStreamAsync(cancellationToken))
        {
            // ...
        }
    }
}
```

### Entry Properties

```csharp
foreach (var entry in archive.Entries)
{
    string name = entry.Key;              // Entry name/path
    long size = entry.Size;               // Uncompressed size
    long compressedSize = entry.CompressedSize;
    bool isDir = entry.IsDirectory;
    DateTime? modTime = entry.LastModifiedTime;
    CompressionType compression = entry.CompressionType;
}
```

### Creating Archives

```csharp
using (var archive = ZipArchive.CreateArchive())
{
    // Add file
    archive.AddEntry("file.txt", @"C:\source\file.txt");

    // Add multiple files
    archive.AddAllFromDirectory(@"C:\source");
    archive.AddAllFromDirectory(@"C:\source", "*.txt");  // Pattern

    // Save to file
    archive.SaveTo("output.zip", CompressionType.Deflate);

    // Save to stream
    archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate)
    {
        CompressionLevel = 9,
        LeaveStreamOpen = true
    });
}
```

---

## Reader API Methods

### Forward-Only Reading

```csharp
using (var stream = File.OpenRead("file.zip"))
using (var reader = ReaderFactory.OpenReader(stream))
{
    while (reader.MoveToNextEntry())
    {
        IArchiveEntry entry = reader.Entry;

        if (!entry.IsDirectory)
        {
            // Extract entry
            reader.WriteEntryToDirectory(@"C:\output");
            reader.WriteEntryToFile(@"C:\output\file.txt");

            // Or get stream
            using (var entryStream = reader.OpenEntryStream())
            {
                entryStream.CopyTo(outputStream);
            }
        }
    }
}

// Async variants (use OpenAsyncReader to get IAsyncReader)
using (var stream = File.OpenRead("file.zip"))
await using (var reader = await ReaderFactory.OpenAsyncReader(stream))
{
    while (await reader.MoveToNextEntryAsync())
    {
        await reader.WriteEntryToFileAsync(
            @"C:\output\" + reader.Entry.Key,
            cancellationToken: cancellationToken
        );
    }

    // Async extraction of all entries
    await reader.WriteAllToDirectoryAsync(
        @"C:\output",
        cancellationToken: cancellationToken
    );
}
```

---

## Writer API Methods

### Creating Archives (Streaming)

```csharp
using (var stream = File.Create("output.zip"))
using (var writer = WriterFactory.OpenWriter(stream, ArchiveType.Zip, CompressionType.Deflate))
{
    // Write single file
    using (var fileStream = File.OpenRead("source.txt"))
    {
        writer.Write("entry.txt", fileStream, DateTime.Now);
    }
    
    // Write directory
    writer.WriteAll("C:\\source", "*", SearchOption.AllDirectories);
    writer.WriteAll("C:\\source", "*.txt", SearchOption.TopDirectoryOnly);
}

// Async variants: use OpenAsyncWriter to get IAsyncWriter
await using var stream = File.Create("output.zip");
await using var writer = await WriterFactory.OpenAsyncWriter(stream, ArchiveType.Zip, new WriterOptions(CompressionType.Deflate), cancellationToken);

using (var fileStream = File.OpenRead("source.txt"))
{
    await writer.WriteAsync("entry.txt", fileStream, DateTime.Now, cancellationToken);
}

await writer.WriteAllAsync("C:\\source", "*", SearchOption.AllDirectories, cancellationToken);
```

---

## Common Options

### ReaderOptions

Use preset properties and fluent helpers for common configurations:

```csharp
// External stream with password and custom encoding
var options = ReaderOptions.ForExternalStream
    .WithPassword("password")
    .WithArchiveEncoding(new ArchiveEncoding { Default = Encoding.GetEncoding(932) });

using (var archive = ZipArchive.OpenArchive("file.zip", options))
{
    // ...
}

// Open-time presets
var external = ReaderOptions.ForExternalStream;
var owned = ReaderOptions.ForFilePath;
var encrypted = ReaderOptions.ForEncryptedArchive("password");
var encoded = ReaderOptions.ForEncoding(new ArchiveEncoding { Default = Encoding.UTF8 });
var sfx = ReaderOptions.ForSelfExtractingArchive("password");

// Faster detection when the container is known
var hinted = ReaderOptions.ForExternalStream.WithExtensionHint("tar.gz");

// Increase for non-seekable streams with large detection probes, such as SFX RAR
var buffered = ReaderOptions.ForExternalStream.WithRewindableBufferSize(1_048_576);

// Extraction presets
var safeOptions = ExtractionOptions.SafeExtract;  // No overwrite
var flatOptions = ExtractionOptions.FlatExtract;  // No directory structure
var metadataOptions = ExtractionOptions.PreserveMetadata; // Keep timestamps and attributes

// Tune extraction copy buffering
var extractionOptions = new ExtractionOptions { BufferSize = 131072 };

// Factory defaults:
// - file path / FileInfo overloads use LeaveStreamOpen = false
// - stream overloads use LeaveStreamOpen = true
```

Alternative: traditional object initializer:

```csharp
var options = new ReaderOptions
{
    Password = "password",
    LeaveStreamOpen = true,
    ArchiveEncoding = new ArchiveEncoding { Default = Encoding.GetEncoding(932) },
    ExtensionHint = "zip",
    LookForHeader = true,
    DisableCheckIncomplete = false,
    BufferSize = 81920,
    RewindableBufferSize = 1_048_576,
};

var extractionOptions = new ExtractionOptions
{
    ExtractFullPath = true,
    Overwrite = true,
    BufferSize = 131072,
};
```

### WriterOptions

Factory methods provide a clean, discoverable way to create writer options:

```csharp
// Factory methods for common archive types
var zipOptions = WriterOptions.ForZip()                           // ZIP with Deflate
    .WithCompressionLevel(9)                                      // 0-9 for Deflate
    .WithLeaveStreamOpen(false);                                  // Close stream when done

var tarOptions = WriterOptions.ForTar(CompressionType.GZip)       // TAR with GZip
    .WithLeaveStreamOpen(false);

var gzipOptions = WriterOptions.ForGZip()                         // GZip file
    .WithCompressionLevel(6);

archive.SaveTo("output.zip", zipOptions);
```

Use typed writer options when a format exposes extra settings:

```csharp
// ZIP archive-level options
var zipWriterOptions = new ZipWriterOptions(CompressionType.Deflate)
{
    ArchiveComment = "Created by SharpCompress",
    UseZip64 = true,
    CompressionLevel = 9,
};

// ZIP per-entry options
using (var writer = new ZipWriter(outputStream, zipWriterOptions))
using (var source = File.OpenRead("source.txt"))
{
    writer.Write("entry.txt", source, new ZipWriterEntryOptions
    {
        CompressionType = CompressionType.ZStandard,
        CompressionLevel = 3,
        EntryComment = "per-entry comment",
        ModificationDateTime = DateTime.UtcNow,
        EnableZip64 = true,
    });
}

// TAR-specific options
var tarOptions = new TarWriterOptions(CompressionType.GZip, finalizeArchiveOnClose: true)
{
    HeaderFormat = TarHeaderWriteFormat.GNU_TAR_LONG_LINK,
};

// GZip-specific options
var gzipOptions = new GZipWriterOptions(compressionLevel: 9);

// 7z writing requires a seekable output stream and writes non-solid archives
var sevenZipOptions = new SevenZipWriterOptions(CompressionType.LZMA2)
{
    CompressHeader = true,
    LzmaProperties = new LzmaEncoderProperties(),
};
```

Alternative: traditional constructor with object initializer:

```csharp
var options = new WriterOptions(CompressionType.Deflate)
{
    CompressionLevel = 9,
    LeaveStreamOpen = true,
};
archive.SaveTo("output.zip", options);
```

### Extraction behavior

```csharp
var options = new ExtractionOptions
{
    ExtractFullPath = true,                         // Recreate directory structure
    Overwrite = true,                               // Overwrite existing files
    PreserveFileTime = true                         // Keep original timestamps
};

using (var archive = ZipArchive.OpenArchive("file.zip"))
{
    archive.WriteToDirectory(@"C:\output", options);
}
```

### Options matrix

```text
ReaderOptions: open-time behavior (password, encoding, stream ownership)
ExtractionOptions: extract-time behavior (overwrite, paths, timestamps, attributes, symlinks)
WriterOptions: write-time behavior (compression type/level, encoding, stream ownership)
ZipWriterEntryOptions: per-entry ZIP overrides (compression, level, timestamps, comments, zip64)
```

### Compression Providers

`ReaderOptions` and `WriterOptions` expose a `Providers` registry that controls which `ICompressionProvider` implementations are used for each `CompressionType`. The registry defaults to `CompressionProviderRegistry.Default`, so you only need to set it if you want to swap in a custom provider (for example the `SystemGZipCompressionProvider` or `SystemDeflateCompressionProvider`). The selected registry is honored by Reader/Writer APIs, Archive APIs, and async entry-stream extraction paths.

```csharp
var registry = CompressionProviderRegistry.Default
    .With(new SystemGZipCompressionProvider())
    .With(new SystemDeflateCompressionProvider());
var readerOptions = ReaderOptions.ForFilePath.WithProviders(registry);
var writerOptions = new WriterOptions(CompressionType.GZip)
{
    CompressionLevel = 6,
}.WithProviders(registry);

using var reader = ReaderFactory.OpenReader(input, readerOptions);
using var writer = WriterFactory.OpenWriter(output, ArchiveType.GZip, writerOptions);
```

Registry API summary:

```text
CompressionProviderRegistry.Default: built-in provider registry
CompressionProviderRegistry.Empty: empty registry, primarily useful for tests
With(provider): returns a new registry with that provider added or replaced
GetProvider(type): returns the registered ICompressionProvider, or null
CreateCompressStream(...): creates a compression stream or throws if unsupported
CreateDecompressStream(...): creates a decompression stream or throws if unsupported
CreateCompressStreamAsync(...): async stream creation counterpart
CreateDecompressStreamAsync(...): async stream creation counterpart
GetCompressingProvider(type): returns ICompressionProviderHooks when available
```

`ICompressionProvider` implementations declare their `CompressionType`, whether they support compression/decompression, and create sync/async streams. For simple providers, derive from `CompressionProviderBase`; it supplies default async implementations that delegate to the synchronous methods. For read-only codecs, derive from `DecompressionOnlyProviderBase`.

```csharp
public sealed class CustomGZipProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.GZip;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel) =>
        new GZipStream(destination, CompressionMode.Compress, leaveOpen: false);

    public override Stream CreateDecompressStream(Stream source) =>
        new GZipStream(source, CompressionMode.Decompress, leaveOpen: false);
}
```

`CompressionContext` carries metadata that providers may need when creating streams, including `InputSize`, `OutputSize`, `Properties`, `CanSeek`, `FormatOptions`, and `ReaderOptions`. Use `CompressionContext.FromStream(stream)` to populate stream-derived values, `WithReaderOptions(...)` to attach reader metadata, and `ResolveArchiveEncoding()` to get the archive header encoding from the context.

When a format needs additional initialization/finalization data (LZMA, PPMd, etc.) the registry exposes `GetCompressingProvider` which returns the `ICompressionProviderHooks` contract; the rest of the API continues to flow through `Providers`, including pre/properties/post compression hook data.

---

## Compression Types

### Available Compressions

```csharp
// For creating archives
CompressionType.None       // No compression (store)
CompressionType.GZip       // GZip wrapper/combined tar compression
CompressionType.Deflate    // DEFLATE (default for ZIP/GZip)
CompressionType.Deflate64  // Deflate64
CompressionType.BZip2      // BZip2
CompressionType.LZMA       // LZMA (for 7Zip, LZip, XZ)
CompressionType.LZMA2      // LZMA2 (for 7Zip/XZ)
CompressionType.BCJ        // 7Zip branch converter filter
CompressionType.BCJ2       // 7Zip branch converter filter
CompressionType.LZip       // LZip wrapper/combined tar compression
CompressionType.Xz         // XZ wrapper/combined tar compression
CompressionType.PPMd       // PPMd (for ZIP)
CompressionType.Rar        // RAR compression (read-only)
CompressionType.Lzw        // LZW, including .Z/tar.Z reading
CompressionType.Shrink     // ZIP shrink (read-only)
CompressionType.Reduce1    // ZIP reduce methods (read-only)
CompressionType.Reduce2
CompressionType.Reduce3
CompressionType.Reduce4
CompressionType.Explode    // ZIP implode/explode (read-only)
CompressionType.Squeezed   // ARC/ARJ legacy compression (read-only)
CompressionType.Packed     // ARC legacy compression (read-only)
CompressionType.Crunched   // ARC legacy compression (read-only)
CompressionType.Squashed   // Legacy compression (read-only)
CompressionType.Crushed    // Legacy compression (read-only)
CompressionType.Distilled  // Legacy compression (read-only)
CompressionType.ZStandard  // ZStandard
CompressionType.ArjLZ77    // ARJ compression (read-only)
CompressionType.AceLZ77    // ACE compression (read-only)
CompressionType.Unknown

// For Tar archives with compression
// Use WriterFactory to create compressed tar archives
using (var writer = WriterFactory.OpenWriter(stream, ArchiveType.Tar, CompressionType.GZip))  // Tar.GZip
using (var writer = WriterFactory.OpenWriter(stream, ArchiveType.Tar, CompressionType.BZip2)) // Tar.BZip2
```

### Archive Types

```csharp
ArchiveType.Zip
ArchiveType.Tar
ArchiveType.GZip
ArchiveType.BZip2
ArchiveType.Rar
ArchiveType.SevenZip
ArchiveType.Arc
ArchiveType.Arj
ArchiveType.Ace
ArchiveType.Lzw
```

---

## Patterns & Examples

### Extract with Error Handling

```csharp
try
{
    using (var archive = ZipArchive.OpenArchive("archive.zip", 
        ReaderOptions.ForEncryptedArchive("password")))
    {
        archive.WriteToDirectory(@"C:\output");
    }
}
catch (PasswordRequiredException)
{
    Console.WriteLine("Password required");
}
catch (InvalidArchiveException)
{
    Console.WriteLine("Archive is invalid");
}
catch (SharpCompressException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

### Extract with Progress

```csharp
var progress = new Progress<ProgressReport>(report =>
{
    Console.WriteLine($"Extracting {report.EntryPath}: {report.PercentComplete}%");
});

var options = ReaderOptions.ForFilePath.WithProgress(progress);
using (var archive = ZipArchive.OpenArchive("archive.zip", options))
{
    archive.WriteToDirectory(@"C:\output");
}
```

### Async Extract with Cancellation

```csharp
var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromMinutes(5));

try
{
    await using (var archive = await ZipArchive.OpenAsyncArchive("archive.zip"))
    {
        await archive.WriteToDirectoryAsync(
            @"C:\output",
            cancellationToken: cts.Token
        );
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Extraction cancelled");
}
```

### Create with Custom Compression

```csharp
using (var archive = ZipArchive.CreateArchive())
{
    archive.AddAllFromDirectory(@"D:\source");
    
    // Fastest
    archive.SaveTo("fast.zip", new WriterOptions(CompressionType.Deflate)
    {
        CompressionLevel = 1
    });
    
    // Balanced (default)
    archive.SaveTo("normal.zip", CompressionType.Deflate);
    
    // Best compression
    archive.SaveTo("best.zip", new WriterOptions(CompressionType.Deflate)
    {
        CompressionLevel = 9
    });
}
```

### Stream Processing (No File I/O)

```csharp
using (var outputStream = new MemoryStream())
using (var archive = ZipArchive.CreateArchive())
{
    // Add content from memory
    using (var contentStream = new MemoryStream(Encoding.UTF8.GetBytes("Hello")))
    {
        archive.AddEntry("file.txt", contentStream);
    }
    
    // Save to memory
    archive.SaveTo(outputStream, CompressionType.Deflate);
    
    // Get bytes
    byte[] archiveBytes = outputStream.ToArray();
}
```

### Buffered Forward-Only Streams

`SharpCompressStream` can wrap streams with buffering for forward-only scenarios:

```csharp
// Wrap a non-seekable stream with buffering
using (var bufferedStream = new SharpCompressStream(rawStream))
{
    // Provides ring buffer functionality for reading ahead
    // and seeking within buffered data
    using (var reader = ReaderFactory.OpenReader(bufferedStream))
    {
        while (reader.MoveToNextEntry())
        {
            reader.WriteEntryToDirectory(@"C:\output");
        }
    }
}
```

Useful for:
- Non-seekable streams (network streams, pipes)
- Forward-only reading with limited look-ahead
- Buffering unbuffered streams for better performance

### Extract Specific Files

```csharp
using (var archive = ZipArchive.OpenArchive("archive.zip"))
{
    var filesToExtract = new[] { "file1.txt", "file2.txt" };
    
    foreach (var entry in archive.Entries.Where(e => filesToExtract.Contains(e.Key)))
    {
        entry.WriteToFile(@"C:\output\" + entry.Key);
    }
}
```

### List Archive Contents

```csharp
using (var archive = ZipArchive.OpenArchive("archive.zip"))
{
    foreach (var entry in archive.Entries)
    {
        if (entry.IsDirectory)
            Console.WriteLine($"[DIR]  {entry.Key}");
        else
            Console.WriteLine($"[FILE] {entry.Key} ({entry.Size} bytes)");
    }
}
```

---

## Common Mistakes

### ✗ Wrong - Stream not disposed

```csharp
var stream = File.OpenRead("archive.zip");
var archive = ZipArchive.OpenArchive(stream);
archive.WriteToDirectory(@"C:\output");
// stream not disposed - leaked resource
```

### ✓ Correct - Using blocks

```csharp
using (var stream = File.OpenRead("archive.zip"))
using (var archive = ZipArchive.OpenArchive(stream))
{
    archive.WriteToDirectory(@"C:\output");
}
// Both properly disposed
```

### ✗ Wrong - Mixing API styles

```csharp
// Loading entire archive then iterating
using (var archive = ZipArchive.OpenArchive("large.zip"))
{
    var entries = archive.Entries.ToList();  // Loads all in memory
    foreach (var e in entries)
    {
        e.WriteToFile(...);  // Then extracts each
    }
}
```

### ✓ Correct - Use Reader for large files

```csharp
// Streaming iteration
using (var stream = File.OpenRead("large.zip"))
using (var reader = ReaderFactory.OpenReader(stream))
{
    while (reader.MoveToNextEntry())
    {
        reader.WriteEntryToDirectory(@"C:\output");
    }
}
```

---

## Related Documentation

- [USAGE.md](USAGE.md) - Complete code examples
- [FORMATS.md](FORMATS.md) - Supported formats
- [PERFORMANCE.md](PERFORMANCE.md) - API selection guide
