# SharpCompress Usage

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

If using Compression Stream classes directly and you don't want the wrapped stream to be closed.  Use the `NonDisposingStream` as a wrapped to prevent the stream being disposed.  The change in 0.21 simplified a lot even though the usage is a bit more convoluted.

## Samples

Also, look over the tests for more thorough [examples](https://github.com/adamhathcock/sharpcompress/tree/master/tests/SharpCompress.Test)

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

### Extract all files from a Rar file to a directory using RarArchive

```C#
using (var archive = RarArchive.Open("Test.rar"))
{
    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
    {
        entry.WriteToDirectory("D:\\temp", new ExtractionOptions()
        {
            ExtractFullPath = true,
            Overwrite = true
        });
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