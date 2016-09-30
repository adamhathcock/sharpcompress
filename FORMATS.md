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
| Tar | None, BZip2, GZip | Both | TarArchive | TarReader | TarWriter (3)  |
| GZip (single file) | GZip | Both | GZipArchive | GZipReader | GZipWriter |
| 7Zip (4) | LZMA, LZMA2, BZip2, PPMd, BCJ, BCJ2, Deflate | Decompress | SevenZipArchive | N/A | N/A |

 1. SOLID Rars are only supported in the RarReader API.
 2. Zip format supports pkware and WinzipAES encryption.  However, encrypted LZMA is not supported.
 3. The Tar format requires a file size in the header.  If no size is specified to the TarWriter and the stream is not seekable, then an exception will be thrown.
 4. The 7Zip format doesn't allow for reading as a forward-only stream so 7Zip is only supported through the Archive API

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
