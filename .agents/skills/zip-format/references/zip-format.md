# ZIP Format Reference

This reference summarizes the ZIP archive container format for SharpCompress work. It is locally authored from PKWARE APPNOTE and the current SharpCompress implementation.

Primary external reference:

- PKWARE APPNOTE.TXT - ZIP File Format Specification, version 6.3.10: https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT

Primary SharpCompress references:

- `docs/FORMATS.md`
- `src/SharpCompress/Factories/ZipFactory.cs`
- `src/SharpCompress/Archives/Zip/ZipArchive.cs`
- `src/SharpCompress/Readers/Zip/ZipReader.cs`
- `src/SharpCompress/Writers/Zip/ZipWriter.cs`
- `src/SharpCompress/Writers/Zip/ZipWritingStream.cs`
- `src/SharpCompress/Writers/Zip/ZipCentralDirectoryEntry.cs`
- `src/SharpCompress/Common/Zip/ZipCompressionMethod.cs`
- `src/SharpCompress/Common/Zip/ZipEntry.cs`
- `src/SharpCompress/Common/Zip/ZipFilePart.cs`
- `src/SharpCompress/Common/Zip/ZipHeaderFactory.cs`
- `src/SharpCompress/Common/Zip/ZipHeaderFactory.Async.cs`
- `src/SharpCompress/Common/Zip/SeekableZipHeaderFactory.cs`
- `src/SharpCompress/Common/Zip/SeekableZipHeaderFactory.Async.cs`
- `src/SharpCompress/Common/Zip/StreamingZipHeaderFactory.cs`
- `src/SharpCompress/Common/Zip/StreamingZipHeaderFactory.Async.cs`
- `src/SharpCompress/Common/Zip/Headers/ZipFileEntry.cs`
- `src/SharpCompress/Common/Zip/Headers/LocalEntryHeader.cs`
- `src/SharpCompress/Common/Zip/Headers/DirectoryEntryHeader.cs`
- `src/SharpCompress/Common/Zip/Headers/LocalEntryHeaderExtraFactory.cs`
- `tests/SharpCompress.Test/Zip/`

## Contents

- [Format Overview](#format-overview)
- [Record Signatures](#record-signatures)
- [Local File Header](#local-file-header)
- [Central Directory Header](#central-directory-header)
- [End Of Central Directory](#end-of-central-directory)
- [Data Descriptors](#data-descriptors)
- [General Purpose Bit Flags](#general-purpose-bit-flags)
- [Compression Methods](#compression-methods)
- [Extra Fields](#extra-fields)
- [Zip64](#zip64)
- [Names, Comments, And Encoding](#names-comments-and-encoding)
- [Encryption](#encryption)
- [Seekable And Streaming Reads](#seekable-and-streaming-reads)
- [SharpCompress Support Matrix](#sharpcompress-support-matrix)
- [SharpCompress Write Behavior](#sharpcompress-write-behavior)
- [Known Limitations](#known-limitations)
- [Test Fixtures](#test-fixtures)

## Format Overview

A ZIP archive stores each file as a local file record followed by compressed or stored payload bytes. Metadata is repeated in a central directory near the end of the archive, followed by the end of central directory record.

High-level APPNOTE layout:

```text
[local file header 1]
[encryption header 1]
[file data 1]
[data descriptor 1]
...
[local file header n]
[encryption header n]
[file data n]
[data descriptor n]
[archive decryption header]
[archive extra data record]
[central directory header 1]
...
[central directory header n]
[zip64 end of central directory record]
[zip64 end of central directory locator]
[end of central directory record]
```

All ordinary multi-byte ZIP fields are little-endian unless APPNOTE says otherwise. ZIP readers must identify records by signatures rather than by extension.

SharpCompress has two important read modes:

- Seekable Archive API reads the central directory first and then seeks to local headers for entry data.
- Streaming Reader API processes local headers and payloads in order and cannot rely on central directory data unless it is already supplied by the caller/path flow.

## Record Signatures

Common ZIP signatures used by SharpCompress:

| Record | Signature | SharpCompress constant |
| --- | --- | --- |
| Local file header | `0x04034b50` | `ENTRY_HEADER_BYTES` |
| Data descriptor | `0x08074b50` | `POST_DATA_DESCRIPTOR` |
| Central directory file header | `0x02014b50` | `DIRECTORY_START_HEADER_BYTES` |
| End of central directory | `0x06054b50` | `DIRECTORY_END_HEADER_BYTES` |
| Digital signature | `0x05054b50` | `DIGITAL_SIGNATURE` |
| Split archive marker | `0x30304b50` | `SPLIT_ARCHIVE_HEADER_BYTES` |
| Zip64 end of central directory | `0x06064b50` | `ZIP64_END_OF_CENTRAL_DIRECTORY` |
| Zip64 end of central directory locator | `0x07064b50` | `ZIP64_END_OF_CENTRAL_DIRECTORY_LOCATOR` |

`ZipHeaderFactory.IsHeader` recognizes these signatures while streaming.

## Local File Header

The local file header immediately precedes optional encryption metadata and file data.

| Field | Size | Notes |
| --- | --- | --- |
| Signature | 4 | `0x04034b50` |
| Version needed to extract | 2 | Feature/version indicator |
| General purpose bit flag | 2 | Encryption, data descriptor, EFS, method-specific bits |
| Compression method | 2 | See [Compression Methods](#compression-methods) |
| Last mod file time | 2 | MS-DOS time |
| Last mod file date | 2 | MS-DOS date |
| CRC-32 | 4 | Zero when bit 3 defers values to data descriptor |
| Compressed size | 4 | `0xffffffff` sentinel when Zip64 extra carries value |
| Uncompressed size | 4 | `0xffffffff` sentinel when Zip64 extra carries value |
| File name length | 2 | Byte count |
| Extra field length | 2 | Byte count |
| File name | variable | Not NUL-terminated |
| Extra field | variable | Sequence of ID/length/data blocks |

SharpCompress reads this in `LocalEntryHeader.Read` and then decodes names, loads extra fields, applies Unicode path extra data, applies Zip64 extra data, and applies selected Unix time data.

## Central Directory Header

The central directory file header repeats entry metadata and adds fields needed for random access.

| Field | Size | Notes |
| --- | --- | --- |
| Signature | 4 | `0x02014b50` |
| Version made by | 2 | Host/version metadata |
| Version needed to extract | 2 | Feature/version indicator |
| General purpose bit flag | 2 | Same semantic space as local header |
| Compression method | 2 | See [Compression Methods](#compression-methods) |
| Last mod file time | 2 | MS-DOS time |
| Last mod file date | 2 | MS-DOS date |
| CRC-32 | 4 | Entry CRC |
| Compressed size | 4 | Zip64 sentinel possible |
| Uncompressed size | 4 | Zip64 sentinel possible |
| File name length | 2 | Byte count |
| Extra field length | 2 | Byte count |
| File comment length | 2 | Byte count |
| Disk number start | 2 | Zip64 sentinel possible |
| Internal file attributes | 2 | Host/application metadata |
| External file attributes | 4 | Host-dependent file attributes |
| Relative offset of local header | 4 | Zip64 sentinel possible |
| File name | variable | Not NUL-terminated |
| Extra field | variable | Sequence of ID/length/data blocks |
| File comment | variable | Not NUL-terminated |

SharpCompress reads this in `DirectoryEntryHeader.Read`. Seekable ZIP reads use central directory entries to discover the archive contents, then `SeekableZipHeaderFactory.GetLocalHeader` seeks to local headers and copies central-directory-only metadata onto the local entry.

## End Of Central Directory

Every normal ZIP archive ends with exactly one EOCD record. The minimum EOCD length is 22 bytes, plus an optional ZIP file comment up to 65535 bytes.

EOCD fields:

| Field | Size | Notes |
| --- | --- | --- |
| Signature | 4 | `0x06054b50` |
| Number of this disk | 2 | Split/spanned metadata |
| Central directory start disk | 2 | Split/spanned metadata |
| Entries on this disk | 2 | `0xffff` sentinel when Zip64 is needed |
| Total entries | 2 | `0xffff` sentinel when Zip64 is needed |
| Central directory size | 4 | `0xffffffff` sentinel when Zip64 is needed |
| Central directory offset | 4 | `0xffffffff` sentinel when Zip64 is needed |
| ZIP file comment length | 2 | Byte count |
| ZIP file comment | variable | Archive-level comment |

`SeekableZipHeaderFactory.SeekBackToHeader` searches backwards from the end of the stream across the maximum EOCD/comment search window.

## Data Descriptors

When general purpose bit 3 is set, the local header CRC and size fields are placeholders. The actual values follow file data in a data descriptor.

Descriptor forms:

```text
[optional signature 0x08074b50]
crc-32                  4 bytes
compressed size         4 or 8 bytes
uncompressed size       4 or 8 bytes
```

APPNOTE says the signature was not originally assigned but is commonly used. SharpCompress handles descriptors with and without the signature in streaming reads.

Important SharpCompress behavior:

- `StreamingZipHeaderFactory` reads post-data descriptors after the previous entry stream has been consumed.
- Streaming descriptor parsing has compatibility logic for 32-bit and 64-bit sizes.
- `ZipWriter` writes a descriptor with signature for non-seekable output when Zip64 is not required.
- `ZipWriter` intentionally rejects Zip64 on non-seekable streams.

## General Purpose Bit Flags

Selected flags relevant to SharpCompress:

| Bit | Meaning |
| --- | --- |
| 0 | Entry is encrypted |
| 1 | Method-specific. For LZMA method 14, set means EOS marker is used. For Implode it has dictionary-size meaning. |
| 2 | Method-specific. For Deflate/Deflate64 it encodes compression option; for Implode it has tree-count meaning. |
| 3 | CRC and sizes are deferred to a data descriptor after file data |
| 6 | Strong encryption |
| 11 | Language encoding flag (EFS): file name and comment are UTF-8 |
| 13 | Central directory encryption masks selected local header values |

SharpCompress decodes names/comments as UTF-8 when EFS is set. For ZIP LZMA writing on non-seekable output, it sets bit 1 for EOS marker behavior.

## Compression Methods

APPNOTE compression method IDs relevant to SharpCompress and nearby unsupported methods:

| ID | APPNOTE method | SharpCompress status |
| --- | --- | --- |
| 0 | Stored | Read/write |
| 1 | Shrunk | Read |
| 2 | Reduced factor 1 | Read |
| 3 | Reduced factor 2 | Read |
| 4 | Reduced factor 3 | Read |
| 5 | Reduced factor 4 | Read |
| 6 | Imploded | Read |
| 8 | Deflated | Read/write |
| 9 | Deflate64 | Read |
| 12 | BZIP2 | Read/write |
| 14 | LZMA | Read/write |
| 93 | Zstandard | Read/write |
| 95 | XZ | Read |
| 98 | PPMd version I, Rev 1 | Read/write |
| 99 | AE-x encryption marker | Read for WinZip AES handling |

SharpCompress declares these in `ZipCompressionMethod.cs`:

```text
None = 0
Shrink = 1
Reduce1 = 2
Reduce2 = 3
Reduce3 = 4
Reduce4 = 5
Explode = 6
Deflate = 8
Deflate64 = 9
BZip2 = 12
LZMA = 14
ZStandard = 93
Xz = 95
PPMd = 98
WinzipAes = 0x63
```

ZIP has method 14 for LZMA and method 95 for XZ. There is no separate APPNOTE ZIP compression method named LZMA2 in the method table. XZ commonly uses LZMA2 internally, but a ZIP entry using APPNOTE method 95 is an XZ-compressed ZIP entry, not a separate LZMA2 ZIP method.

`ZipFilePart.ToCompressionType` maps supported read methods to public `CompressionType` values and throws for unsupported methods before decompression. `ZipEntry.CompressionType` reports the public entry compression type, including `CompressionType.Xz` for method 95.

## Extra Fields

APPNOTE extra fields use this generic structure:

```text
header id   2 bytes
data size   2 bytes
data        variable
```

SharpCompress parses extra fields in `ZipFileEntry.LoadExtra`. Unknown or unsupported extra fields become `NotImplementedExtraData` and are preserved only as raw data for internal parsing decisions.

Recognized extra fields:

| Header ID | Meaning | SharpCompress type |
| --- | --- | --- |
| `0x0001` | Zip64 extended information | `Zip64ExtendedInformationExtraField` |
| `0x5455` | Extended timestamp / Unix time | `UnixTimeExtraField` |
| `0x7075` | Info-ZIP Unicode path | `ExtraUnicodePathExtraField` |
| `0x9901` | WinZip AES | Raw `ExtraData` used by AES logic |

Zip64 extra values appear only when the corresponding 16-bit or 32-bit field in the local or central directory is set to its maximum sentinel. Values must appear in APPNOTE order:

1. Original/uncompressed size
2. Compressed size
3. Relative header offset
4. Disk start number

`Zip64ExtendedInformationExtraField.Process` enforces the required byte count for the sentinel fields being resolved.

## Zip64

Zip64 extends size, count, and offset fields beyond classic ZIP limits. Classic fields use sentinel values when the real value is stored elsewhere:

| Classic field size | Sentinel |
| --- | --- |
| 2 bytes | `0xffff` |
| 4 bytes | `0xffffffff` |

Zip64 structures:

- Zip64 extended information extra field (`0x0001`) carries per-entry sizes and offsets.
- Zip64 end of central directory record (`0x06064b50`) carries archive-level counts, central directory size, and central directory offset.
- Zip64 end of central directory locator (`0x07064b50`) points to the Zip64 EOCD record.

SharpCompress read behavior:

- Seekable reads detect Zip64 through EOCD sentinel values and then locate the Zip64 EOCD locator and record.
- Local and central entry readers apply Zip64 extra data when size/offset fields contain sentinels.

SharpCompress write behavior:

- `ZipWriterOptions.UseZip64` controls whether local headers reserve Zip64 extra data for entries.
- `ZipWriter` emits Zip64 EOCD and locator when entry counts, central directory size, or offsets require them.
- Non-seekable Zip64 writing is rejected because current post-data descriptor handling cannot safely represent the required Zip64 values in all cases.

## Names, Comments, And Encoding

APPNOTE file name and comment fields are length-prefixed byte sequences, not NUL-terminated strings.

Rules relevant to SharpCompress:

- If general purpose bit 11 (EFS) is set, file names and comments must be UTF-8.
- If EFS is not set, SharpCompress uses the configured archive encoding.
- If Info-ZIP Unicode path extra field `0x7075` is present and the caller did not force an encoding, SharpCompress uses the Unicode name from that extra field.
- `ZipWriter.NormalizeFilename` converts backslashes to `/`, removes drive prefixes before `:`, and trims leading/trailing `/` for file entries.
- `ZipWriter` ensures directory entries end with `/`.

## Encryption

APPNOTE defines traditional PKWARE encryption, strong encryption features, central directory encryption, and method 99 as an AE-x encryption marker.

SharpCompress behavior:

- Traditional PKWARE-encrypted ZIP entries can be read when a password is supplied.
- WinZip AES entries use method 99 (`WinzipAes`) and extra field `0x9901`; SharpCompress reads the actual compression method from that extra data.
- `ZipHeaderFactory.LoadHeader` rejects encrypted ZIP data that requires unsupported non-seekable handling.
- Central directory encryption and broad strong encryption records are not general-purpose supported features. Keep this explicit in support claims.

## Seekable And Streaming Reads

Seekable read path:

- `SeekableZipHeaderFactory` searches backward for EOCD.
- If EOCD indicates Zip64, it reads the Zip64 locator and Zip64 EOCD.
- It iterates central directory file headers.
- `GetLocalHeader` seeks to each entry's local header when entry data is needed.

Streaming read path:

- `StreamingZipHeaderFactory` reads local headers in archive order.
- Entry data must be consumed or skipped before the next header can be parsed.
- If bit 3 is set, descriptor values are read after entry data.
- Directory entries may be inferred from names ending in `/`, or from zero-size names ending in `\` for older .NET-produced archives.

Archive API vs Reader API:

- `ZipArchive` is appropriate for seekable streams and multi-volume/split archives.
- `ZipReader` is forward-only and does not seek across volume files.

## SharpCompress Support Matrix

Current ZIP support summary from `docs/FORMATS.md` and implementation:

| Feature | Read | Write | Notes |
| --- | --- | --- | --- |
| Stored | Yes | Yes | Method 0 |
| Deflate | Yes | Yes | Method 8 |
| Deflate64 | Yes | No | Method 9 |
| BZip2 | Yes | Yes | Method 12 |
| LZMA | Yes | Yes | Method 14 |
| PPMd | Yes | Yes | Method 98 |
| ZStandard | Yes | Yes | Method 93 |
| XZ | Yes | No | Method 95 |
| Shrink | Yes | No | Legacy method 1 |
| Reduce | Yes | No | Legacy methods 2-5 |
| Implode | Yes | No | Legacy method 6 |
| Zip64 | Yes | Yes | Writing requires seekable stream for Zip64 |
| Data descriptors | Yes | Yes | Writer uses them for non-seekable non-Zip64 output |
| Traditional PKWARE encryption | Yes | No | Password required |
| WinZip AES | Yes | No | Method 99 + extra field `0x9901` |
| Split/multi-volume ZIP | Yes | No | Use Archive API |

## SharpCompress Write Behavior

`ZipWriter` is forward-only for entry creation but writes a central directory on dispose. It tracks local header offsets and entry sizes as data is written.

Writer compression mapping in `ZipWriter.ToZipCompressionMethod` currently accepts:

- `CompressionType.None`
- `CompressionType.Deflate`
- `CompressionType.BZip2`
- `CompressionType.LZMA`
- `CompressionType.PPMd`
- `CompressionType.ZStandard`

Other compression types throw `InvalidFormatException`, including `CompressionType.Xz`.

Important writer rules:

- Local headers are written before payload data.
- Seekable output lets SharpCompress patch CRC and size fields after compression.
- Non-seekable output uses post-data descriptors for non-Zip64 entries.
- Zip64 on non-seekable output is rejected.
- Zero-byte file entries are normalized to stored/no compression in the central directory.
- Central directory entries are written on `Dispose` / `DisposeAsync`; callers must dispose writers to finalize archives.

## Known Limitations

Keep these limitations explicit in code comments, docs, and tests:

- No separate ZIP LZMA2 method support. APPNOTE method 95 is XZ; XZ may use LZMA2 internally.
- No XZ writing for ZIP entries.
- No Deflate64, Shrink, Reduce, or Implode writing.
- No general-purpose central directory encryption support.
- No broad APPNOTE strong encryption record support beyond current traditional PKWARE and WinZip AES read paths.
- No non-seekable Zip64 writing.
- Unknown extra fields are not semantically modeled unless explicitly implemented.
- ZipReader cannot seek across multi-volume/split archive parts; use ZipArchive for split archives.

## Test Fixtures

Representative fixtures in `tests/TestArchives/Archives/`:

- `Zip.none.zip`
- `Zip.deflate.zip`
- `Zip.deflate.dd.zip`
- `Zip.deflate64.zip`
- `Zip.bzip2.zip`
- `Zip.lzma.zip`
- `Zip.lzma.dd.zip`
- `Zip.ppmd.zip`
- `Zip.shrink.zip`
- `Zip.reduce1.zip`
- `Zip.reduce2.zip`
- `Zip.reduce3.zip`
- `Zip.reduce4.zip`
- `Zip.implode.zip`
- `Zip.zip64.zip`
- `Zip.zstd.WinzipAES.mixed.zip`
- `WinZip27_XZ.zipx`
- `WinZip27_ZSTD.zipx`
- `WinZip26.nocomp.multi.zip`
- `WinZip26.nocomp.multi.zipx`
- `Zip.UnicodePathExtra.zip`
- `Zip.EntryComment.zip`

Representative test files:

- `tests/SharpCompress.Test/Zip/ZipArchiveTests.cs`
- `tests/SharpCompress.Test/Zip/ZipArchiveAsyncTests.cs`
- `tests/SharpCompress.Test/Zip/ZipReaderTests.cs`
- `tests/SharpCompress.Test/Zip/ZipReaderAsyncTests.cs`
- `tests/SharpCompress.Test/Zip/ZipWriterTests.cs`
- `tests/SharpCompress.Test/Zip/ZipWriterAsyncTests.cs`
- `tests/SharpCompress.Test/Zip/Zip64Tests.cs`
- `tests/SharpCompress.Test/Zip/Zip64AsyncTests.cs`
- `tests/SharpCompress.Test/Zip/Zip64VersionConsistencyTests.cs`
- `tests/SharpCompress.Test/Zip/ZipFilePartTests.cs`
