# Formats

## Accessing Archives

* Archive classes allow random access to a seekable stream.
* Reader classes allow forward-only reading on a stream.
* Writer classes allow forward-only Writing on a stream.

## Supported Format Table

| Archive Format         | Compression Format(s)                             | Compress/Decompress | Archive API     | Reader API | Writer API    |
| ---------------------- | ------------------------------------------------- | ------------------- | --------------- | ---------- | ------------- |
| Rar                    | Rar                                               | Decompress (1)      | RarArchive      | RarReader  | N/A           |
| Zip (2)                | None, DEFLATE, Deflate64, BZip2, LZMA/LZMA2, PPMd | Both                | ZipArchive      | ZipReader  | ZipWriter     |
| Tar                    | None                                              | Both                | TarArchive      | TarReader  | TarWriter (3) |
| Tar.GZip               | DEFLATE                                           | Both                | TarArchive      | TarReader  | TarWriter (3) |
| Tar.BZip2              | BZip2                                             | Both                | TarArchive      | TarReader  | TarWriter (3) |
| Tar.LZip               | LZMA                                              | Both                | TarArchive      | TarReader  | TarWriter (3) |
| Tar.XZ                 | LZMA2                                             | Decompress          | TarArchive      | TarReader  | TarWriter (3) |
| GZip (single file)     | DEFLATE                                           | Both                | GZipArchive     | GZipReader | GZipWriter    |
| 7Zip (4)               | LZMA, LZMA2, BZip2, PPMd, BCJ, BCJ2, Deflate      | Decompress          | SevenZipArchive | N/A        | N/A           |

1. SOLID Rars are only supported in the RarReader API.
2. Zip format supports pkware and WinzipAES encryption. However, encrypted LZMA is not supported. Zip64 reading/writing is supported but only with seekable streams as the Zip spec doesn't support Zip64 data in post data descriptors. Deflate64 is only supported for reading.
3. The Tar format requires a file size in the header. If no size is specified to the TarWriter and the stream is not seekable, then an exception will be thrown.
4. The 7Zip format doesn't allow for reading as a forward-only stream so 7Zip is only supported through the Archive API
5. LZip has no support for extra data like the file name or timestamp. There is a default filename used when looking at the entry Key on the archive.

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

## Archive Formats vs Compression

Sometimes the terminology gets mixed.

### Compression

DEFLATE, LZMA are pure compression algorithms

### Formats

Formats like Zip, 7Zip, Rar are archive formats only. They use other compression methods (e.g. DEFLATE, LZMA, etc.) or propriatory (e.g RAR)

### Overlap

GZip, BZip2 and LZip are single file archival formats. The overlap in the API happens because Tar uses the single file formats as "compression" methods and the API tries to hide this a bit.
