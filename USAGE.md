# SharpCompress Usage

## Async/Await Support

SharpCompress now provides full async/await support for all I/O operations. All `Read`, `Write`, and extraction operations have async equivalents ending in `Async` that accept an optional `CancellationToken`. This enables better performance and scalability for I/O-bound operations.

**Key Async Methods:**
- `reader.WriteEntryToAsync(stream, cancellationToken)` - Extract entry asynchronously  
- `reader.WriteAllToDirectoryAsync(path, options, cancellationToken)` - Extract all asynchronously
- `writer.WriteAsync(filename, stream, modTime, cancellationToken)` - Write entry asynchronously
- `writer.WriteAllAsync(directory, pattern, searchOption, cancellationToken)` - Write directory asynchronously
- `entry.OpenEntryStreamAsync(cancellationToken)` - Open entry stream asynchronously

See [Async Examples](#async-examples) section below for usage patterns.

## Stream Rules (changed with 0.21)

When dealing with Streams, the rule should be that you don't close a stream you didn't create. This, in effect, should mean you should always put a Stream in a using block to dispose it. 

However, the .NET Framework often has classes that will dispose streams by default to make things "easy" like the following:

```C#
using (var reader = new StreamReader(File.Open("foo")))
{
    ...
}
```

In this example, reader should get disposed. However, stream rules should say the the `FileStream` created by `File.Open` should remain open. However, the .NET Framework closes it for you by default unless you override the constructor. In general, you should be writing Stream code like this:

```C#
using (var fileStream = File.Open("foo"))
using (var reader = new StreamReader(fileStream))
{
    ...
}
```

To deal with the "correct" rules as well as the expectations of users, I've decided to always close wrapped streams as of 0.21.

To be explicit though, consider always using the overloads that use `ReaderOptions` or `WriterOptions` and explicitly set `LeaveStreamOpen` the way you want.

If using Compression Stream classes directly and you don't want the wrapped stream to be closed.  Use the `NonDisposingStream` as a wrapper to prevent the stream being disposed.  The change in 0.21 simplified a lot even though the usage is a bit more convoluted.

## Samples

Also, look over the tests for more thorough [examples](https://github.com/adamhathcock/sharpcompress/tree/master/tests/SharpCompress.Test)

### Create Zip Archive from multiple files
```C#
using(var archive = ZipArchive.Create())
{
    archive.AddEntry("file01.txt", "C:\\file01.txt");
    archive.AddEntry("file02.txt", "C:\\file02.txt");
    ...
    
    archive.SaveTo("C:\\temp.zip", CompressionType.Deflate);
}
```

### Create Zip Archive from all files in a directory to a file

```C#
using (var archive = ZipArchive.Create())
{
    archive.AddAllFromDirectory("D:\\temp");
    archive.SaveTo("C:\\temp.zip", CompressionType.Deflate);
}
```

### Create Zip Archive from all files in a directory and save in memory

```C#
var memoryStream = new MemoryStream();
using (var archive = ZipArchive.Create())
{
    archive.AddAllFromDirectory("D:\\temp");
    archive.SaveTo(memoryStream, new WriterOptions(CompressionType.Deflate)
                                {
                                    LeaveStreamOpen = true
                                });
}
//reset memoryStream to be usable now
memoryStream.Position = 0;
```

### Extract all files from a rar file to a directory using RarArchive

Note: Extracting a solid rar or 7z file needs to be done in sequential order to get acceptable decompression speed.
It is explicitly recommended to use `ExtractAllEntries` when extracting an entire `IArchive` instead of iterating over all its `Entries`.
Alternatively, use `IArchive.WriteToDirectory`.

```C#
using (var archive = RarArchive.Open("Test.rar"))
{
    using (var reader = archive.ExtractAllEntries())
    {
        reader.WriteAllToDirectory(@"D:\temp", new ExtractionOptions()
        {
            ExtractFullPath = true,
            Overwrite = true
        });
    }
}
```

### Iterate over all files from a Rar file using RarArchive

```C#
using (var archive = RarArchive.Open("Test.rar"))
{
    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
    {
        Console.WriteLine($"{entry.Key}: {entry.Size} bytes");
    }
}
```

### Use ReaderFactory to autodetect archive type and Open the entry stream

```C#
using (Stream stream = File.OpenRead("Tar.tar.bz2"))
using (var reader = ReaderFactory.Open(stream))
{
    while (reader.MoveToNextEntry())
    {
        if (!reader.Entry.IsDirectory)
        {
            Console.WriteLine(reader.Entry.Key);
            reader.WriteEntryToDirectory(@"C:\temp", new ExtractionOptions()
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }
    }
}
```

### Use ReaderFactory to autodetect archive type and Open the entry stream

```C#
using (Stream stream = File.OpenRead("Tar.tar.bz2"))
using (var reader = ReaderFactory.Open(stream))
{
    while (reader.MoveToNextEntry())
    {
        if (!reader.Entry.IsDirectory)
        {
            using (var entryStream = reader.OpenEntryStream())
            {
                entryStream.CopyTo(...);
            }
        }
    }
}
```

### Use WriterFactory to write all files from a directory in a streaming manner.

```C#
using (Stream stream = File.OpenWrite("C:\\temp.tgz"))
using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, new WriterOptions(CompressionType.GZip)
                                                {
                                                    LeaveOpenStream = true
                                                }))
{
    writer.WriteAll("D:\\temp", "*", SearchOption.AllDirectories);
}
```

### Extract zip which has non-utf8 encoded filename(cp932)

```C#
var opts = new SharpCompress.Readers.ReaderOptions();
var encoding = Encoding.GetEncoding(932);
opts.ArchiveEncoding = new SharpCompress.Common.ArchiveEncoding();
opts.ArchiveEncoding.CustomDecoder = (data, x, y) =>
{
    return encoding.GetString(data);
};
var tr = SharpCompress.Archives.Zip.ZipArchive.Open("test.zip", opts);
foreach(var entry in tr.Entries)
{
    Console.WriteLine($"{entry.Key}");
}
```

## Async Examples

### Async Reader Examples

**Extract single entry asynchronously:**
```C#
using (Stream stream = File.OpenRead("archive.zip"))
using (var reader = ReaderFactory.Open(stream))
{
    while (reader.MoveToNextEntry())
    {
        if (!reader.Entry.IsDirectory)
        {
            using (var entryStream = reader.OpenEntryStream())
            {
                using (var outputStream = File.Create("output.bin"))
                {
                    await reader.WriteEntryToAsync(outputStream, cancellationToken);
                }
            }
        }
    }
}
```

**Extract all entries asynchronously:**
```C#
using (Stream stream = File.OpenRead("archive.tar.gz"))
using (var reader = ReaderFactory.Open(stream))
{
    await reader.WriteAllToDirectoryAsync(
        @"D:\temp",
        new ExtractionOptions()
        {
            ExtractFullPath = true,
            Overwrite = true
        },
        cancellationToken
    );
}
```

**Open and process entry stream asynchronously:**
```C#
using (var archive = ZipArchive.Open("archive.zip"))
{
    foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
    {
        using (var entryStream = await entry.OpenEntryStreamAsync(cancellationToken))
        {
            // Process the decompressed stream asynchronously
            await ProcessStreamAsync(entryStream, cancellationToken);
        }
    }
}
```

### Async Writer Examples

**Write single file asynchronously:**
```C#
using (Stream archiveStream = File.OpenWrite("output.zip"))
using (var writer = WriterFactory.Open(archiveStream, ArchiveType.Zip, CompressionType.Deflate))
{
    using (Stream fileStream = File.OpenRead("input.txt"))
    {
        await writer.WriteAsync("entry.txt", fileStream, DateTime.Now, cancellationToken);
    }
}
```

**Write entire directory asynchronously:**
```C#
using (Stream stream = File.OpenWrite("backup.tar.gz"))
using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, new WriterOptions(CompressionType.GZip)))
{
    await writer.WriteAllAsync(
        @"D:\files",
        "*",
        SearchOption.AllDirectories,
        cancellationToken
    );
}
```

**Write with progress tracking and cancellation:**
```C#
var cts = new CancellationTokenSource();

// Set timeout or cancel from UI
cts.CancelAfter(TimeSpan.FromMinutes(5));

using (Stream stream = File.OpenWrite("archive.zip"))
using (var writer = WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate))
{
    try
    {
        await writer.WriteAllAsync(@"D:\data", "*", SearchOption.AllDirectories, cts.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Operation was cancelled");
    }
}
```

### Archive Async Examples

**Extract from archive asynchronously:**
```C#
using (var archive = ZipArchive.Open("archive.zip"))
{
    using (var reader = archive.ExtractAllEntries())
    {
        await reader.WriteAllToDirectoryAsync(
            @"C:\output",
            new ExtractionOptions() { ExtractFullPath = true, Overwrite = true },
            cancellationToken
        );
    }
}
```

**Benefits of Async Operations:**
- Non-blocking I/O for better application responsiveness
- Improved scalability for server applications
- Support for cancellation via CancellationToken
- Better resource utilization in async/await contexts
- Compatible with modern .NET async patterns
