# Formats

## Accessing Archives

* Archive classes allow random access to a seekable stream.
* Reader classes allow forward-only reading on a stream.
* Writer classes allow forward-only Writing on a stream.

## Supported Format Table

| Archive Format         | Compression Format(s)                             | Compress/Decompress | Archive API     | Reader API | Writer API    |
| ---------------------- | ------------------------------------------------- | ------------------- | --------------- | ---------- | ------------- |
| Ace                    | None                                              | Decompress          | N/A             | AceReader  | N/A           |
| Arc                    | None, Packed, Squeezed, Crunched                  | Decompress          | N/A             | ArcReader  | N/A           |
| Arj                    | None                                              | Decompress          | N/A             | ArjReader  | N/A           |
| Rar                    | Rar                                               | Decompress          | RarArchive      | RarReader  | N/A           |
| Zip (2)                | None, Shrink, Reduce, Implode, DEFLATE, Deflate64, BZip2, LZMA/LZMA2, PPMd                           | Both                | ZipArchive      | ZipReader  | ZipWriter     | 
| Tar                    | None                                              | Both                | TarArchive      | TarReader  | TarWriter (3) |
| Tar.GZip               | DEFLATE                                           | Both                | TarArchive      | TarReader  | TarWriter (3) |
| Tar.BZip2              | BZip2                                             | Both                | TarArchive      | TarReader  | TarWriter (3) |
| Tar.Zstandard          | ZStandard                                         | Decompress          | TarArchive      | TarReader  | N/A |
| Tar.LZip               | LZMA                                              | Both                | TarArchive      | TarReader  | TarWriter (3) |
| Tar.XZ                 | LZMA2                                             | Decompress          | TarArchive      | TarReader  | TarWriter (3) |
| GZip (single file)     | DEFLATE                                           | Both                | GZipArchive     | GZipReader | GZipWriter    |
| 7Zip (4)               | LZMA, LZMA2, BZip2, PPMd, BCJ, BCJ2, Deflate      | Decompress          | SevenZipArchive | SevenZipReader | N/A        |

1. SOLID Rars are only supported in the RarReader API.
2. Zip format supports pkware and WinzipAES encryption. However, encrypted LZMA is not supported. Zip64 reading/writing is supported but only with seekable streams as the Zip spec doesn't support Zip64 data in post data descriptors. Deflate64 is only supported for reading. See [Zip Format Notes](#zip-format-notes) for details on multi-volume archives and streaming behavior.
3. The Tar format requires a file size in the header. If no size is specified to the TarWriter and the stream is not seekable, then an exception will be thrown.
4. 7Zip supports sequential reader extraction through `SevenZipReader`/`ReaderFactory`, but does not support random per-entry extraction via `IExtractableArchive` (entries must be processed sequentially). See [7Zip Format Notes](#7zip-format-notes).
5. LZip has no support for extra data like the file name or timestamp. There is a default filename used when looking at the entry Key on the archive.

### Zip Format Notes

- Multi-volume/split ZIP archives require ZipArchive (seekable streams) as ZipReader cannot seek across volume files.
- ZipReader processes entries from LocalEntry headers (which include directory entries ending with `/`) and intentionally skips DirectoryEntry headers from the central directory, as they are redundant in streaming mode - all entry data comes from LocalEntry headers which ZipReader has already processed.

### 7Zip Format Notes

- 7Zip supports sequential extraction through:
  - `SevenZipArchive.ExtractAllEntries()` / `ExtractAllEntriesAsync()`
  - `SevenZipReader.OpenReader(...)` / `OpenAsyncReader(...)`
  - `ReaderFactory.OpenReader(...)` / `OpenAsyncReader(...)` (auto-detect)
- 7Zip entries are organized into compression folders and should be processed in archive order for best performance.
- 7Zip still does **not** implement `IExtractableArchive`; individual random-access `OpenEntryStream` extraction from archive entries is not supported.

## Compression Streams

For those who want to directly compress/decompress bits. The single file formats are represented here as well. However, BZip2, LZip and XZ have no metadata (GZip has a little) so using them without something like a Tar file makes little sense.

| Compressor      | Compress/Decompress |
| --------------- | ------------------- |
| BZip2Stream     | Both                |
| GZipStream      | Both                |
| DeflateStream   | Both                |
| Deflate64Stream | Decompress          |
| LZMAStream      | Both                |
| PPMdStream      | Both                |
| ADCStream       | Decompress          |
| LZipStream      | Both                |
| XZStream        | Decompress          |
| ZStandardStream | Decompress          |

## Archive Formats vs Compression

Sometimes the terminology gets mixed.

### Compression

DEFLATE, LZMA are pure compression algorithms

### Formats

Formats like Zip, 7Zip, Rar are archive formats only. They use other compression methods (e.g. DEFLATE, LZMA, etc.) or propriatory (e.g RAR)

### Overlap

GZip, BZip2 and LZip are single file archival formats. The overlap in the API happens because Tar uses the single file formats as "compression" methods and the API tries to hide this a bit.
