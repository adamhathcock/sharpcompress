# Archive Formats

## Accessing Archives

Archive classes allow random access to a seekable stream.
Reader classes allow forward-only reading
Writer classes allow forward-only Writing

## Supported Format Table

| Archive Format | Compression Format(s) | Compress/Decompress | Archive API | Reader API | Writer API |
| --- | --- | --- | --- | --- | --- |
| Rar | Rar | Decompress (1) | RarArchive | RarReader | N/A |
| Zip (2) | None, DEFLATE, BZip2, LZMA/LZMA2, PPMd | Both | ZipArchive | ZipReader | ZipWriter |
| Tar | None, BZip2, GZip, LZip | Both | TarArchive | TarReader | TarWriter (3)  |
| GZip (single file) | GZip (DEFLATE) | Both | GZipArchive | GZipReader | GZipWriter |
| 7Zip (4) | LZMA, LZMA2, BZip2, PPMd, BCJ, BCJ2, Deflate | Decompress | SevenZipArchive | N/A | N/A |
| LZip (single file) (5) | LZip (LZMA) | Both | LZipArchive | LZipReader | LZipWriter |

 1. SOLID Rars are only supported in the RarReader API.
 2. Zip format supports pkware and WinzipAES encryption.  However, encrypted LZMA is not supported.  Zip64 reading is supported.
 3. The Tar format requires a file size in the header.  If no size is specified to the TarWriter and the stream is not seekable, then an exception will be thrown.
 4. The 7Zip format doesn't allow for reading as a forward-only stream so 7Zip is only supported through the Archive API
 5. LZip has no support for extra data like the file name or timestamp.  There is a default filename used when looking at the entry Key on the archive.

## Compressors

For those who want to directly compress/decompress bits

| Compressor | Compress/Decompress |
| --- | --- |
| BZip2Stream | Both |
| GZipStream | Both |
| DeflateStream | Both |
| LZMAStream | Both |
| PPMdStream | Both |
| ADCStream | Decompress |
| LZipStream | Both |

## Archive Formats vs Compression 

Sometimes the terminology gets mixed.

### Compression

DEFLATE, LZMA are pure compression algorithms

### Formats

Formats like Zip, 7Zip, Rar are archive formats only.  They use other compression methods (e.g. DEFLATE, LZMA, etc.) or propriatory (e.g RAR)

### Overlap

GZip, BZip2 and LZip are single file archival formats.  The overlap in the API happens because Tar uses the single file formats as "compression" methods and the API tries to hide this a bit.