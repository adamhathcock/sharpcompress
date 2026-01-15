# SharpCompress Performance Guide

This guide helps you optimize SharpCompress for performance in various scenarios.

## API Selection Guide

### Archive API vs Reader API

Choose the right API based on your use case:

| Aspect | Archive API | Reader API |
|--------|------------|-----------|
| **Stream Type** | Seekable only | Non-seekable OK |
| **Memory Usage** | All entries in memory | One entry at a time |
| **Random Access** | ✓ Yes | ✗ No |
| **Best For** | Small-to-medium archives | Large or streaming data |
| **Performance** | Fast for random access | Better for large files |

### Archive API (Fast for Random Access)

```csharp
// Use when:
// - Archive fits in memory
// - You need random access to entries
// - Stream is seekable (file, MemoryStream)

using (var archive = ZipArchive.OpenArchive("archive.zip"))
{
    // Random access - all entries available
    var specific = archive.Entries.FirstOrDefault(e => e.Key == "file.txt");
    if (specific != null)
    {
        specific.WriteToFile(@"C:\output\file.txt");
    }
}
```

**Performance Characteristics:**
- ✓ Instant entry lookup
- ✓ Parallel extraction possible
- ✗ Entire archive in memory
- ✗ Can't process while downloading

### Reader API (Best for Large Files)

```csharp
// Use when:
// - Processing large archives (>100 MB)
// - Streaming from network/pipe
// - Memory is constrained
// - Forward-only processing is acceptable

using (var stream = File.OpenRead("large.zip"))
using (var reader = ReaderFactory.OpenReader(stream))
{
    while (reader.MoveToNextEntry())
    {
        // Process one entry at a time
        reader.WriteEntryToDirectory(@"C:\output");
    }
}
```

**Performance Characteristics:**
- ✓ Minimal memory footprint
- ✓ Works with non-seekable streams
- ✓ Can process while downloading
- ✗ Forward-only (no random access)
- ✗ Entry lookup requires iteration

---

## Buffer Sizing

### Understanding Buffers

SharpCompress uses internal buffers for reading compressed data. Buffer size affects:
- **Speed:** Larger buffers = fewer I/O operations = faster
- **Memory:** Larger buffers = higher memory usage

### Recommended Buffer Sizes

| Scenario | Size | Notes |
|----------|------|-------|
| Embedded/IoT devices | 4-8 KB | Minimal memory usage |
| Memory-constrained | 16-32 KB | Conservative default |
| Standard use (default) | 64 KB | Recommended default |
| Large file streaming | 256 KB | Better throughput |
| High-speed SSD | 512 KB - 1 MB | Maximum throughput |

### How Buffer Size Affects Performance

```csharp
// SharpCompress manages buffers internally
// You can't directly set buffer size, but you can:

// 1. Use Stream.CopyTo with explicit buffer size
using (var entryStream = reader.OpenEntryStream())
using (var fileStream = File.Create(@"C:\output\file.txt"))
{
    // 64 KB buffer (default)
    entryStream.CopyTo(fileStream);
    
    // Or specify larger buffer for faster copy
    entryStream.CopyTo(fileStream, bufferSize: 262144);  // 256 KB
}

// 2. Use custom buffer for writing
using (var entryStream = reader.OpenEntryStream())
using (var fileStream = File.Create(@"C:\output\file.txt"))
{
    byte[] buffer = new byte[262144];  // 256 KB
    int bytesRead;
    while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
    {
        fileStream.Write(buffer, 0, bytesRead);
    }
}
```

---

## Streaming Large Files

### Non-Seekable Stream Patterns

For processing archives from downloads or pipes:

```csharp
// Download stream (non-seekable)
using (var httpStream = await httpClient.GetStreamAsync(url))
using (var reader = ReaderFactory.OpenReader(httpStream))
{
    // Process entries as they arrive
    while (reader.MoveToNextEntry())
    {
        if (!reader.Entry.IsDirectory)
        {
            reader.WriteEntryToDirectory(@"C:\output");
        }
    }
}
```

**Performance Tips:**
- Don't try to buffer the entire stream
- Process entries immediately
- Use async APIs for better responsiveness

### Download-Then-Extract vs Streaming

Choose based on your constraints:

| Approach | When to Use |
|----------|------------|
| **Download then extract** | Moderate size, need random access |
| **Stream during download** | Large files, bandwidth limited, memory constrained |

```csharp
// Download then extract (requires disk space)
var archivePath = await DownloadFile(url, @"C:\temp\archive.zip");
using (var archive = ZipArchive.OpenArchive(archivePath))
{
    archive.WriteToDirectory(@"C:\output");
}

// Stream during download (on-the-fly extraction)
using (var httpStream = await httpClient.GetStreamAsync(url))
using (var reader = ReaderFactory.OpenReader(httpStream))
{
    while (reader.MoveToNextEntry())
    {
        reader.WriteEntryToDirectory(@"C:\output");
    }
}
```

---

## Solid Archive Optimization

### Why Solid Archives Are Slow

Solid archives (Rar, 7Zip) group files together in a single compressed stream:

```
Solid Archive Layout:
[Header] [Compressed Stream] [Footer]
         ├─ File1 compressed data
         ├─ File2 compressed data
         ├─ File3 compressed data
         └─ File4 compressed data
```

Extracting File3 requires decompressing File1 and File2 first.

### Sequential vs Random Extraction

**Random Extraction (Slow):**
```csharp
using (var archive = RarArchive.OpenArchive("solid.rar"))
{
    foreach (var entry in archive.Entries)
    {
        entry.WriteToFile(@"C:\output\" + entry.Key);  // ✗ Slow!
        // Each entry triggers full decompression from start
    }
}
```

**Sequential Extraction (Fast):**
```csharp
using (var archive = RarArchive.OpenArchive("solid.rar"))
{
    // Method 1: Use WriteToDirectory (recommended)
    archive.WriteToDirectory(@"C:\output", new ExtractionOptions
    {
        ExtractFullPath = true,
        Overwrite = true
    });
    
    // Method 2: Use ExtractAllEntries
    archive.ExtractAllEntries();
    
    // Method 3: Use Reader API (also sequential)
    using (var reader = RarReader.Open(File.OpenRead("solid.rar")))
    {
        while (reader.MoveToNextEntry())
        {
            reader.WriteEntryToDirectory(@"C:\output");
        }
    }
}
```

**Performance Impact:**
- Random extraction: O(n²) - very slow for many files
- Sequential extraction: O(n) - 10-100x faster

### Best Practices for Solid Archives

1. **Always extract sequentially** when possible
2. **Use Reader API** for large solid archives
3. **Process entries in order** from the archive
4. **Consider using 7Zip command-line** for scripted extractions

---

## Compression Level Trade-offs

### Deflate/GZip Levels

```csharp
// Level 1 = Fastest, largest size
// Level 6 = Default (balanced)
// Level 9 = Slowest, best compression

// Write with different compression levels
using (var archive = ZipArchive.CreateArchive())
{
    archive.AddAllFromDirectory(@"D:\data");
    
    // Fast compression (level 1)
    archive.SaveTo("fast.zip", new WriterOptions(CompressionType.Deflate)
    {
        CompressionLevel = 1
    });
    
    // Default compression (level 6)
    archive.SaveTo("default.zip", CompressionType.Deflate);
    
    // Best compression (level 9)
    archive.SaveTo("best.zip", new WriterOptions(CompressionType.Deflate)
    {
        CompressionLevel = 9
    });
}
```

**Speed vs Size:**
| Level | Speed | Size | Use Case |
|-------|-------|------|----------|
| 1 | 10x | 90% | Network, streaming |
| 6 | 1x | 75% | Default (good balance) |
| 9 | 0.1x | 65% | Archival, static storage |

### BZip2 Block Size

```csharp
// BZip2 block size affects memory and compression
// 100K to 900K (default 900K)

// Smaller block size = lower memory, faster
// Larger block size = better compression, slower

using (var archive = TarArchive.CreateArchive())
{
    archive.AddAllFromDirectory(@"D:\data");
    
    // These are preset in WriterOptions via CompressionLevel
    archive.SaveTo("archive.tar.bz2", CompressionType.BZip2);
}
```

### LZMA Settings

LZMA compression is very powerful but memory-intensive:

```csharp
// LZMA (7Zip, .tar.lzma):
// - Dictionary size: 16 KB to 1 GB (default 32 MB)
// - Faster preset: smaller dictionary
// - Better compression: larger dictionary

// Preset via CompressionType
using (var archive = TarArchive.CreateArchive())
{
    archive.AddAllFromDirectory(@"D:\data");
    archive.SaveTo("archive.tar.xz", CompressionType.LZMA);  // Default settings
}
```

---

## Async Performance

### When Async Helps

Async is beneficial when:
- **Long I/O operations** (network, slow disks)
- **UI responsiveness** needed (Windows Forms, WPF, Blazor)
- **Server applications** (ASP.NET, multiple concurrent operations)

```csharp
// Async extraction (non-blocking)
using (var archive = ZipArchive.OpenArchive("archive.zip"))
{
    await archive.WriteToDirectoryAsync(
        @"C:\output",
        new ExtractionOptions { ExtractFullPath = true, Overwrite = true },
        cancellationToken
    );
}
// Thread can handle other work while I/O happens
```

### When Async Doesn't Help

Async doesn't improve performance for:
- **CPU-bound operations** (already fast)
- **Local SSD I/O** (I/O is fast enough)
- **Single-threaded scenarios** (no parallelism benefit)

```csharp
// Sync extraction (simpler, same performance on fast I/O)
using (var archive = ZipArchive.OpenArchive("archive.zip"))
{
    archive.WriteToDirectory(
        @"C:\output",
        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
    );
}
// Simple and fast - no async needed
```

### Cancellation Pattern

```csharp
var cts = new CancellationTokenSource();

// Cancel after 5 minutes
cts.CancelAfter(TimeSpan.FromMinutes(5));

try
{
    using (var archive = ZipArchive.OpenArchive("archive.zip"))
    {
        await archive.WriteToDirectoryAsync(
            @"C:\output",
            new ExtractionOptions { ExtractFullPath = true, Overwrite = true },
            cts.Token
        );
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Extraction cancelled");
    // Clean up partial extraction if needed
}
```

---

## Practical Performance Tips

### 1. Choose the Right API

| Scenario | API | Why |
|----------|-----|-----|
| Small archives | Archive | Faster random access |
| Large archives | Reader | Lower memory |
| Streaming | Reader | Works on non-seekable streams |
| Download streams | Reader | Async extraction while downloading |

### 2. Batch Operations

```csharp
// ✗ Slow - opens each archive separately
foreach (var file in files)
{
    using (var archive = ZipArchive.OpenArchive("archive.zip"))
    {
        archive.WriteToDirectory(@"C:\output");
    }
}

// ✓ Better - process multiple entries at once
using (var archive = ZipArchive.OpenArchive("archive.zip"))
{
    archive.WriteToDirectory(@"C:\output");
}
```

### 3. Profile Your Code

```csharp
var sw = Stopwatch.StartNew();
using (var archive = ZipArchive.OpenArchive("large.zip"))
{
    archive.WriteToDirectory(@"C:\output");
}
sw.Stop();

Console.WriteLine($"Extraction took {sw.ElapsedMilliseconds}ms");

// Measure memory before/after
var beforeMem = GC.GetTotalMemory(true);
// ... do work ...
var afterMem = GC.GetTotalMemory(true);
Console.WriteLine($"Memory used: {(afterMem - beforeMem) / 1024 / 1024}MB");
```

---

## Troubleshooting Performance

### Extraction is Slow

1. **Check if solid archive** → Use sequential extraction
2. **Check API** → Reader API might be faster for large files
3. **Check compression level** → Higher levels are slower to decompress
4. **Check I/O** → Network drives are much slower than SSD
5. **Check buffer size** → May need larger buffers for network

### High Memory Usage

1. **Use Reader API** instead of Archive API
2. **Process entries immediately** rather than buffering
3. **Reduce compression level** if writing
4. **Check for memory leaks** in your code

### CPU Usage at 100%

1. **Normal for compression** - especially with high compression levels
2. **Consider lower level** for faster processing
3. **Reduce parallelism** if processing multiple archives
4. **Check if awaiting properly** in async code

---

## Related Documentation

- [PERFORMANCE.md](USAGE.md) - Usage examples with performance considerations
- [FORMATS.md](FORMATS.md) - Format-specific performance notes
