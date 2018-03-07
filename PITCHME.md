
#### SharpCompress - Pure C# Archival and Compression

---

#### Overview

* History
* Design
* Archival Formats
* Usages (Code!)

---

#### Why?

* Bored
* Interested in Comics and wanted to make my own cross-platform viewer
* Wrote a viewer in Sliverlight 2 using first versions of SharpCompress.
* Used it on OS X

---

#### Initial Version

* Started as NUnrar on Codeplex
* Used Visual Studio 2003 to convert JUnrar to C#
* Cleaned up to have a nicer API

---

### More Formats

* Integrated DotNetZip
* Created Unified API
* Added Tar
* Contributions: 7Zip, LZip, more!

---

# Design

---

### Unified APIs

* Random Access
  * Archive API
* Forward-only
  * Reader API
  * Writer API
* Neutral Factories

---

### Neural Factories

* Factories
  * `ArchiveFactory`
  * `ReaderFactory`
  * `WriterFactory`
* Strategy
  * Look for Archive Signatures
  * "Rewind" if necessary with RewindableStream

---

### Random Access

* Random/Seekable access on a data stream (e.g. a File)
* Strategy
  * Read Header, Skip Data
  * Dictionary

---

### Forward-only

* Everything is a stream of data
* Support NetworkStreams
* Very large files
* `yield return` usage

---

# Formats

---

### Zip

* Header-Data Format 
  * Optional data trailer (forward-only writing support)
  * Trailing dictionary of entries
* pkware spec - APPNOTE.txt
* Supports Reader API, Writer API and Archive API
* Compression algorthims: just about everything
  * Deflate, BZ2, LZMA 1/2, PPMd

---

### Rar

* Header-Data Format
  * SOLID is a stream of compressed header-data pairs for small files
  * Multi-file archive
* Unrar open-source, rar is closed-source
* Supports Reader API and Archive API
* Compression looks to be a modification of PPMd

---

### 7Zip

* Multi-data compressed Format
  * Headers are compressed
  * Multiple compressed "streams"
* Readable Archive API support
* Annoying
* Known for LZMA

---

### Tar

* Header-Data Format
* Supports Reader API, Writer API and Archive API
* Uncompressed
* Many additions to out-grow limitations 
  * UStar
  * PAX

---

### GZip, BZip, LZip, Xz

* Header-Data Format of a single entry
* Supports Reader API, Writer API and Archive API
* Used with Tar
* Compression
  * GZip - Deflate
  * BZip2 - BZip2
  * Xz - LZMA2
  * LZip - LZMA1 (improvement on Xz)

---

# Usages

---

### Reader

Writing entry to directory

```csharp
using (Stream stream = new NetworkStream()) // pretend
using (IReader reader = ReaderFactory.Open(stream))
{
    while (reader.MoveToNextEntry())
    {
        if (!reader.Entry.IsDirectory)
        {
            reader.WriteEntryToDirectory(test.SCRATCH_FILES_PATH, new ExtractionOptions()
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }
    }
}
```

---

### Reader

Writing entry to a stream

```csharp
using (var reader = RarReader.Open("Rar.rar"))
{
    while (reader.MoveToNextEntry())
    {
        if (!reader.Entry.IsDirectory)
        {
            using (var entryStream = reader.OpenEntryStream())
            {
                string file = Path.GetFileName(reader.Entry.Key);
                string folder = Path.GetDirectoryName(reader.Entry.Key);
                string destdir = Path.Combine(SCRATCH_FILES_PATH, folder);
                if (!Directory.Exists(destdir))
                {
                    Directory.CreateDirectory(destdir);
                }
                string destinationFileName = Path.Combine(destdir, file);
                using (FileStream fs = File.OpenWrite(destinationFileName))
                {
                    entryStream.TransferTo(fs);
                }
            }
        }
    }
}
```

---

### Writer

Creating archive

```csharp
using (Stream stream = File.OpenWrite("Test.tar.lz"))
using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, CompressionType.LZip))
{
    writer.WriteAll("C:\", "*", SearchOption.AllDirectories);
}
```

---

### Archive

---

### Projects

* Mono's Zip implementation
* Nodatime
* Octopus Deploy
* Duplicati
* Large ISO multi-file usage

---

### Open-source Notes

* Mostly solo effort
* A few significant contributions
  * Russian friend did RarStream
  * Jon Skeet contributed LZip reading
  * Deflate64 recently added
* Can always use help!
  * Multi-file zip support
  * Encryption in various formats (some support exists)
  * General clean up