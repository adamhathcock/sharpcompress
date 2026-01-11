# API Quick Reference

Quick reference for commonly used SharpCompress APIs.

## Factory Methods

### Opening Archives

```csharp
// Auto-detect format
using (var reader = ReaderFactory.Open(stream))
{
    // Works with Zip, Tar, GZip, Rar, 7Zip, etc.
}

// Specific format - Archive API
using (var archive = ZipArchive.Open("file.zip"))
using (var archive = TarArchive.Open("file.tar"))
using (var archive = RarArchive.Open("file.rar"))
using (var archive = SevenZipArchive.Open("file.7z"))
using (var archive = GZipArchive.Open("file.gz"))

// With options
var options = new ReaderOptions 
{ 
    Password = "password",
    LeaveStreamOpen = true,
    ArchiveEncoding = new ArchiveEncoding { Default = Encoding.GetEncoding(932) }
};
using (var archive = ZipArchive.Open("encrypted.zip", options))
```

### Creating Archives

```csharp
// Writer Factory
using (var writer = WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate))
{
    // Write entries
}

// Specific writer
using (var archive = ZipArchive.Create())
using (var archive = TarArchive.Create())
using (var archive = GZipArchive.Create())

// With options
var options = new WriterOptions(CompressionType.Deflate) 
{ 
    CompressionLevel = 9,
    LeaveStreamOpen = false
};
using (var archive = ZipArchive.Create())
{
    archive.SaveTo("output.zip", options);
}
```

---

## Archive API Methods

### Reading/Extracting

```csharp
using (var archive = ZipArchive.Open("file.zip"))
{
    // Get all entries
    IEnumerable<IEntry> entries = archive.Entries;
    
    // Find specific entry
    var entry = archive.Entries.FirstOrDefault(e => e.Key == "file.txt");
    
    // Extract all
    archive.WriteToDirectory(@"C:\output", new ExtractionOptions
    {
        ExtractFullPath = true,
        Overwrite = true
    });
    
    // Extract single entry
    var entry = archive.Entries.First();
    entry.WriteToFile(@"C:\output\file.txt");
    entry.WriteToFile(@"C:\output\file.txt", new ExtractionOptions { Overwrite = true });
    
    // Get entry stream
    using (var stream = entry.OpenEntryStream())
    {
        stream.CopyTo(outputStream);
    }
}

// Async variants
await archive.WriteToDirectoryAsync(@"C:\output", options, cancellationToken);
using (var stream = await entry.OpenEntryStreamAsync(cancellationToken))
{
    // ...
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
using (var archive = ZipArchive.Create())
{
    // Add file
    archive.AddEntry("file.txt", "C:\\source\\file.txt");
    
    // Add multiple files
    archive.AddAllFromDirectory("C:\\source");
    archive.AddAllFromDirectory("C:\\source", "*.txt");  // Pattern
    
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
using (var reader = ReaderFactory.Open(stream))
{
    while (reader.MoveToNextEntry())
    {
        IEntry entry = reader.Entry;
        
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

// Async variants
while (await reader.MoveToNextEntryAsync())
{
    await reader.WriteEntryToFileAsync(@"C:\output\" + reader.Entry.Key, cancellationToken);
}

// Async extraction
await reader.WriteAllToDirectoryAsync(@"C:\output", 
    new ExtractionOptions { ExtractFullPath = true, Overwrite = true },
    cancellationToken);
```

---

## Writer API Methods

### Creating Archives (Streaming)

```csharp
using (var stream = File.Create("output.zip"))
using (var writer = WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate))
{
    // Write single file
    using (var fileStream = File.OpenRead("source.txt"))
    {
        writer.Write("entry.txt", fileStream, DateTime.Now);
    }
    
    // Write directory
    writer.WriteAll("C:\\source", "*", SearchOption.AllDirectories);
    writer.WriteAll("C:\\source", "*.txt", SearchOption.TopDirectoryOnly);
    
    // Async variants
    using (var fileStream = File.OpenRead("source.txt"))
    {
        await writer.WriteAsync("entry.txt", fileStream, DateTime.Now, cancellationToken);
    }
    
    await writer.WriteAllAsync("C:\\source", "*", SearchOption.AllDirectories, cancellationToken);
}
```

---

## Common Options

### ReaderOptions

```csharp
var options = new ReaderOptions
{
    Password = "password",                          // For encrypted archives
    LeaveStreamOpen = true,                         // Don't close wrapped stream
    ArchiveEncoding = new ArchiveEncoding          // Custom character encoding
    {
        Default = Encoding.GetEncoding(932)
    }
};
using (var archive = ZipArchive.Open("file.zip", options))
{
    // ...
}
```

### WriterOptions

```csharp
var options = new WriterOptions(CompressionType.Deflate)
{
    CompressionLevel = 9,                           // 0-9 for Deflate
    LeaveStreamOpen = true,                         // Don't close stream
};
archive.SaveTo("output.zip", options);
```

### ExtractionOptions

```csharp
var options = new ExtractionOptions
{
    ExtractFullPath = true,                         // Recreate directory structure
    Overwrite = true,                               // Overwrite existing files
    PreserveFileTime = true                         // Keep original timestamps
};
archive.WriteToDirectory(@"C:\output", options);
```

---

## Compression Types

### Available Compressions

```csharp
// For creating archives
CompressionType.None       // No compression (store)
CompressionType.Deflate    // DEFLATE (default for ZIP/GZip)
CompressionType.BZip2      // BZip2
CompressionType.LZMA       // LZMA (for 7Zip, LZip, XZ)
CompressionType.PPMd       // PPMd (for ZIP)
CompressionType.Rar        // RAR compression (read-only)

// For Tar archives
// Use CompressionType in TarWriter constructor
using (var writer = TarWriter(stream, CompressionType.GZip))  // Tar.GZip
using (var writer = TarWriter(stream, CompressionType.BZip2)) // Tar.BZip2
```

### Archive Types

```csharp
ArchiveType.Zip
ArchiveType.Tar
ArchiveType.GZip
ArchiveType.BZip2
ArchiveType.Rar
ArchiveType.SevenZip
ArchiveType.XZ
ArchiveType.ZStandard
```

---

## Patterns & Examples

### Extract with Error Handling

```csharp
try
{
    using (var archive = ZipArchive.Open("archive.zip", 
        new ReaderOptions { Password = "password" }))
    {
        archive.WriteToDirectory(@"C:\output", new ExtractionOptions
        {
            ExtractFullPath = true,
            Overwrite = true
        });
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

var options = new ReaderOptions { Progress = progress };
using (var archive = ZipArchive.Open("archive.zip", options))
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
    using (var archive = ZipArchive.Open("archive.zip"))
    {
        await archive.WriteToDirectoryAsync(@"C:\output",
            new ExtractionOptions { ExtractFullPath = true, Overwrite = true },
            cts.Token);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Extraction cancelled");
}
```

### Create with Custom Compression

```csharp
using (var archive = ZipArchive.Create())
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
using (var archive = ZipArchive.Create())
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

### Extract Specific Files

```csharp
using (var archive = ZipArchive.Open("archive.zip"))
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
using (var archive = ZipArchive.Open("archive.zip"))
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
var archive = ZipArchive.Open(stream);
archive.WriteToDirectory(@"C:\output");
// stream not disposed - leaked resource
```

### ✓ Correct - Using blocks

```csharp
using (var stream = File.OpenRead("archive.zip"))
using (var archive = ZipArchive.Open(stream))
{
    archive.WriteToDirectory(@"C:\output");
}
// Both properly disposed
```

### ✗ Wrong - Mixing API styles

```csharp
// Loading entire archive then iterating
using (var archive = ZipArchive.Open("large.zip"))
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
using (var reader = ReaderFactory.Open(stream))
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
