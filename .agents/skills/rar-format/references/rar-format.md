# RAR Format Reference

This reference summarizes the RAR archive container format for SharpCompress work. It is locally authored from the RAR 5.0 format document, UnRAR source code, and the current SharpCompress implementation.

Primary local references:

- `reference/RAR 5.0 archive format.htm`
- `reference/unrar/headers5.hpp`
- `reference/unrar/headers.hpp`
- `reference/unrar/arcread.cpp`
- `reference/unrar/rawread.cpp`
- `reference/unrar/rawread.hpp`
- `reference/unrar/crypt.cpp`
- `reference/unrar/crypt5.cpp`
- `reference/unrar/qopen.cpp`
- `reference/unrar/qopen.hpp`
- `reference/unrar/volume.cpp`
- `reference/unrar/volume.hpp`
- `reference/unrar/recvol.cpp`
- `reference/unrar/recvol5.cpp`
- `reference/unrar/unpack.cpp`
- `reference/unrar/unpack15.cpp`
- `reference/unrar/unpack20.cpp`
- `reference/unrar/unpack30.cpp`
- `reference/unrar/unpack50.cpp`
- `reference/unrar/blake2sp.cpp`
- `reference/unrar/blake2sp.hpp`

Primary SharpCompress references:

- `docs/FORMATS.md`
- `src/SharpCompress/Factories/RarFactory.cs`
- `src/SharpCompress/Archives/Rar/RarArchive.cs`
- `src/SharpCompress/Archives/Rar/RarArchiveEntry.cs`
- `src/SharpCompress/Archives/Rar/RarArchiveVolumeFactory.cs`
- `src/SharpCompress/Readers/Rar/RarReader.cs`
- `src/SharpCompress/Readers/Rar/MultiVolumeRarReader.cs`
- `src/SharpCompress/Common/Rar/RarEntry.cs`
- `src/SharpCompress/Common/Rar/RarFilePart.cs`
- `src/SharpCompress/Common/Rar/Rar5CryptoInfo.cs`
- `src/SharpCompress/Common/Rar/RarCryptoBinaryReader.cs`
- `src/SharpCompress/Common/Rar/RarCryptoWrapper.cs`
- `src/SharpCompress/Common/Rar/Headers/MarkHeader.cs`
- `src/SharpCompress/Common/Rar/Headers/RarHeader.cs`
- `src/SharpCompress/Common/Rar/Headers/RarHeaderFactory.cs`
- `src/SharpCompress/Common/Rar/Headers/FileHeader.cs`
- `src/SharpCompress/Common/Rar/Headers/Flags.cs`
- `src/SharpCompress/Compressors/Rar/`
- `tests/SharpCompress.Test/Rar/`

## Contents

- [Format Overview](#format-overview)
- [Signatures And SFX](#signatures-and-sfx)
- [RAR5 Vint Encoding](#rar5-vint-encoding)
- [RAR5 General Block Format](#rar5-general-block-format)
- [RAR5 Header Types](#rar5-header-types)
- [RAR5 Common Header Flags](#rar5-common-header-flags)
- [RAR5 Main Archive Header](#rar5-main-archive-header)
- [RAR5 File And Service Headers](#rar5-file-and-service-headers)
- [RAR5 Extra Area Records](#rar5-extra-area-records)
- [RAR5 Compression Info](#rar5-compression-info)
- [Encryption](#encryption)
- [Checksums And Hashes](#checksums-and-hashes)
- [Service Headers](#service-headers)
- [RAR4 Compatibility](#rar4-compatibility)
- [Multivolume And Solid Archives](#multivolume-and-solid-archives)
- [SharpCompress Support Matrix](#sharpcompress-support-matrix)
- [SharpCompress Read Behavior](#sharpcompress-read-behavior)
- [Known Limitations](#known-limitations)
- [Test Fixtures](#test-fixtures)

## Format Overview

RAR is a block-oriented archive format. SharpCompress supports RAR as a read-only archive/reader format and delegates decompression to its RAR unpacker implementation under `src/SharpCompress/Compressors/Rar/`.

RAR 5.0 general layout:

```text
self-extracting module (optional)
RAR 5.0 signature
archive encryption header (optional)
main archive header
archive comment service header (optional)
file header 1
service headers for preceding file (optional)
...
file header N
service headers for preceding file (optional)
recovery record (optional)
end of archive header
```

RAR has no ZIP-style central directory. Readers scan blocks in sequence. Random access is limited by solid compression, split volumes, and the need to process preceding compressed data for solid archives.

## Signatures And SFX

RAR signatures:

| Format | Bytes | SharpCompress behavior |
| --- | --- | --- |
| Old pre-RAR4 marker | `52 45 7e 5e` | Explicitly unsupported |
| RAR4 marker | `52 61 72 21 1a 07 00` | Supported read path |
| RAR5 marker | `52 61 72 21 1a 07 01 00` | Supported read path |

RAR archives can be preceded by a self-extracting module. `MarkHeader.Read` scans for the marker when `ReaderOptions.LookForHeader` is enabled. SharpCompress uses a maximum SFX scan size from the UnRAR implementation notes.

## RAR5 Vint Encoding

RAR5 uses variable-length integers, called `vint` in the format document.

Rules:

- Each byte contributes 7 data bits.
- The high bit is the continuation flag.
- If the high bit is `0`, the byte is the last byte in the sequence.
- The first byte contains the least significant 7 bits.
- RAR currently uses vint for up to 64-bit integers, so values can occupy up to 10 bytes.
- Writers can preallocate more bytes than needed by using leading `0x80` bytes, which encode zero with continuation set.

SharpCompress reads these through `ReadRarVInt*` helpers on marking readers. For RAR5 header size, SharpCompress limits the size field to 3 vint bytes to match the current format implementation limit of a 2 MB maximum header size.

## RAR5 General Block Format

RAR5 blocks share a common header shape:

| Field | Size | Notes |
| --- | --- | --- |
| Header CRC32 | `uint32` | CRC32 of header data starting at header size through optional extra area |
| Header size | `vint` | Size from header type through optional extra area; current max 3 bytes for 2 MB headers |
| Header type | `vint` | See [RAR5 Header Types](#rar5-header-types) |
| Header flags | `vint` | Common flags for all headers |
| Extra area size | `vint` | Present only when common flag `0x0001` is set |
| Data size | `vint` | Present only when common flag `0x0002` is set |
| Type-specific fields | variable | Depends on header type |
| Extra area | variable | Present only when common flag `0x0001` is set |
| Data area | variable | Present only when common flag `0x0002` is set; not included in header CRC/size |

SharpCompress reads the common fields in `RarHeader.Initialize`, then creates typed headers in `RarHeaderFactory`.

## RAR5 Header Types

RAR5 header type values:

| Type | Meaning | SharpCompress code path |
| --- | --- | --- |
| `1` | Main archive header | `ArchiveHeader` |
| `2` | File header | `FileHeader` with `HeaderType.File` |
| `3` | Service header | `FileHeader` with `HeaderType.Service` |
| `4` | Archive encryption header | `ArchiveCryptHeader` |
| `5` | End of archive header | `EndArchiveHeader` |

SharpCompress constants live in `HeaderCodeV` in `Flags.cs`.

## RAR5 Common Header Flags

Common RAR5 header flags from `headers5.hpp` and `Flags.cs`:

| Flag | Meaning |
| --- | --- |
| `0x0001` | Extra area is present |
| `0x0002` | Data area is present |
| `0x0004` | Unknown blocks with this flag must be skipped when updating |
| `0x0008` | Data area continues from previous volume |
| `0x0010` | Data area continues in next volume |
| `0x0020` | Block depends on preceding file block |
| `0x0040` | Preserve child block if host block is modified |

SharpCompress exposes split status through `FileHeader.IsSplitBefore`, `FileHeader.IsSplitAfter`, and `RarEntry.IsSplitAfter`.

## RAR5 Main Archive Header

Main archive header fields after the common block fields:

| Field | Size | Notes |
| --- | --- | --- |
| Archive flags | `vint` | Volume, solid, recovery, locked flags |
| Volume number | `vint` | Present only when archive flag `0x0002` is set |
| Extra area | variable | Optional records, currently locator is defined |

Main archive flags:

| Flag | Meaning |
| --- | --- |
| `0x0001` | Volume, archive is part of a multivolume set |
| `0x0002` | Volume number field is present |
| `0x0004` | Solid archive |
| `0x0008` | Recovery record is present |
| `0x0010` | Locked archive |

Main header extra record types:

| Type | Name | Meaning |
| --- | --- | --- |
| `0x01` | Locator | Optional offsets to quick-open and recovery-record blocks |

SharpCompress parses archive flags in `ArchiveHeader` and uses volume/solid information in archive and reader flows.

## RAR5 File And Service Headers

File and service headers share the same base layout. Header type `2` is a file header and header type `3` is a service header.

Fields after common block fields:

| Field | Size | Notes |
| --- | --- | --- |
| File flags | `vint` | Directory, time, CRC, unknown unpacked size |
| Unpacked size | `vint` | Present even when unknown-size flag is set, but ignored then |
| Attributes | `vint` | OS-specific file attributes |
| mtime | `uint32` | Unix time, present only when file flag `0x0002` is set |
| Data CRC32 | `uint32` | Present only when file flag `0x0004` is set |
| Compression information | `vint` | Algorithm version, solid flag, method, dictionary size |
| Host OS | `vint` | `0` Windows, `1` Unix |
| Name length | `vint` | Byte count |
| Name | variable | UTF-8, no trailing zero |
| Extra area | variable | Optional file/service extra records |
| Data area | variable | File data or service data |

RAR5 file flags:

| Flag | Meaning |
| --- | --- |
| `0x0001` | Directory filesystem object |
| `0x0002` | Unix mtime field is present |
| `0x0004` | CRC32 field is present |
| `0x0008` | Unpacked size is unknown; extract until compression stream ends |

SharpCompress parses these in `FileHeader.ReadFromReaderV5`.

## RAR5 Extra Area Records

Each extra record has this shape:

```text
record size   vint   size from type through record data
record type   vint
record data   variable
```

File and service header extra record types:

| Type | Name | SharpCompress behavior |
| --- | --- | --- |
| `0x01` | File encryption | Parses `Rar5CryptoInfo` |
| `0x02` | File hash | Reads BLAKE2sp digest when present |
| `0x03` | File time | Reads high precision mtime/ctime/atime |
| `0x04` | File version | Currently skipped/drained |
| `0x05` | Redirection | Parses symlink/junction/hard link/file copy metadata |
| `0x06` | Unix owner | Currently skipped/drained |
| `0x07` | Service data | Currently skipped/drained except service header data handling |

Unknown records must be skipped without interrupting normal operation. SharpCompress drains unhandled extra record bytes after each record.

Redirection types:

| Value | Meaning |
| --- | --- |
| `0x0001` | Unix symlink |
| `0x0002` | Windows symlink |
| `0x0003` | Windows junction |
| `0x0004` | Hard link |
| `0x0005` | File copy |

`RarEntry.IsRedir` and `RarEntry.RedirTargetName` expose part of this metadata.

## RAR5 Compression Info

RAR5 file compression information is a packed vint bit field:

| Bits/mask | Meaning |
| --- | --- |
| `0x003f` | Compression algorithm version, currently 0 in RAR5; SharpCompress stores it as value + 50 |
| `0x0040` | Solid flag; dictionary continues from preceding files |
| `0x0380` | Compression method, values 0-5 are used; 0 is store/no compression |
| `0x3c00` | Minimum dictionary size: 0 means 128 KB, 1 means 256 KB, through 15 meaning 4096 MB |

SharpCompress maps these to `FileHeader.CompressionAlgorithm`, `FileHeader.IsSolid`, `FileHeader.CompressionMethod`, and `FileHeader.WindowSize`.

RAR4 and older algorithm values are distinct. `RarEntry.IsRarV3` currently treats algorithm values `15`, `20`, `26`, `29`, and `36` as legacy RAR code paths.

## Encryption

RAR5 archive encryption header fields:

| Field | Size | Notes |
| --- | --- | --- |
| Encryption version | `vint` | Current supported version is 0, AES-256 |
| Encryption flags | `vint` | `0x0001` means password check data is present |
| KDF count | 1 byte | Binary logarithm of PBKDF2 iteration count |
| Salt | 16 bytes | Header encryption salt |
| Check value | 12 bytes | Optional password check plus checksum |

When archive headers are encrypted, each following header starts with a 16-byte AES-256 initialization vector followed by encrypted header data aligned to 16 bytes.

RAR5 file encryption extra record fields include encryption version, flags, KDF count, 16-byte salt, 16-byte IV, and optional 12-byte password check data.

SharpCompress behavior:

- Archive encryption header is parsed by `ArchiveCryptHeader`.
- File encryption extra records are parsed into `Rar5CryptoInfo`.
- Header decryption uses `RarCryptoBinaryReader` with `CryptKey5`.
- File data decryption uses `RarCryptoWrapper` around the packed stream.
- Password is required through `ReaderOptions.Password`; missing password throws `CryptographicException`.
- RAR4 encrypted file data uses RAR3 crypto classes and salt handling.

## Checksums And Hashes

RAR5 checksum/hash locations:

- Header CRC32 covers header data starting at the header size field through optional extra area. It does not include the data area.
- File header can contain CRC32 of unpacked data when file flag `0x0004` is set.
- File hash extra record type `0x02` can contain BLAKE2sp hash data. The defined RAR5 hash type value is `0x00` for BLAKE2sp.
- For split files, hashes can apply to packed data for non-final volume parts.

SharpCompress verifies header CRC in `RarHeader.VerifyHeaderCrc`. RAR CRC logic is in `RarCrcBinaryReader`, `AsyncRarCrcBinaryReader`, and `RarCRC`.

## Service Headers

RAR5 service headers use the file-header structure with header type `3`. Known service names from the RAR5 document and UnRAR source include:

| Name | Meaning |
| --- | --- |
| `CMT` | Archive comment |
| `QO` | Quick-open data |
| `ACL` | NTFS permissions |
| `STM` | NTFS alternate data stream |
| `RR` | Recovery record |

SharpCompress handles `CMT` specially by exposing its packed stream in `RarHeaderFactory`; most other service data is skipped or only partially modeled.

Quick-open caution from the RAR5 format document:

- Quick-open data can store copies of headers for faster listing.
- If quick-open data is used to display names, extraction must use the same source. Otherwise malicious archives could show one name and extract another.
- SharpCompress should not use quick-open data for one path and ordinary headers for another without carefully preserving this invariant.

## RAR4 Compatibility

SharpCompress supports RAR4-style headers as well as RAR5.

RAR4 header codes from UnRAR and `Flags.cs`:

| Code | Meaning |
| --- | --- |
| `0x72` | Mark header |
| `0x73` | Archive/main header |
| `0x74` | File header |
| `0x75` | Comment header |
| `0x76` | AV header |
| `0x77` | Old subheader |
| `0x78` | Protect/recovery header |
| `0x79` | Sign header |
| `0x7a` | New subheader/service header |
| `0x7b` | End archive header |

RAR4 file-header behavior differs from RAR5:

- Fixed-size base fields rather than RAR5 vint-heavy layout.
- Optional large-file high-size fields when `LARGE` flag is set.
- File names can use older RAR Unicode name encoding.
- DOS timestamps and RAR4 extended time flags are used.
- Directory status is encoded through the window mask.
- Path separators differ: RAR4 can use backslashes as separators, while RAR5 uses `/` as the universal separator.

SharpCompress parses this in `FileHeader.ReadFromReaderV4` and maps paths through `ConvertPathV4`.

## Multivolume And Solid Archives

RAR supports multivolume archives and solid compression.

Relevant flags:

- Main archive `0x0001`: archive is part of a volume set.
- Main archive `0x0002`: RAR5 volume number field is present.
- Common block `0x0008`: data continues from previous volume.
- Common block `0x0010`: data continues in next volume.
- RAR4 file flags `SPLIT_BEFORE` and `SPLIT_AFTER` indicate split file data.
- RAR5 compression info `0x0040` indicates solid compression.

SharpCompress behavior:

- `RarFactory` implements `IMultiArchiveFactory`.
- `RarArchiveVolumeFactory` resolves volume file parts.
- `RarEntry.IsSplitAfter` exposes split status.
- Solid archives should be extracted sequentially. Prefer `ExtractAllEntries()` for solid RAR archives rather than extracting arbitrary entries independently.

## SharpCompress Support Matrix

| Feature | Support | Notes |
| --- | --- | --- |
| RAR read | Yes | Archive and Reader APIs |
| RAR write | No | RAR is read-only |
| RAR4 headers | Yes | Supported parsing path |
| RAR5 headers | Yes | Supported parsing path |
| Pre-RAR4 marker | No | `MarkHeader` throws unsupported format |
| RAR5 vint fields | Yes | Reader helpers parse vint values |
| File/service extra records | Partial | Encryption/hash/time/redirection modeled; others mostly skipped |
| Solid archives | Yes, with sequencing constraints | Use sequential extraction |
| Multivolume archives | Yes | Archive/multi-volume factory paths |
| Encrypted archives | Partial | Password required; format/version constraints apply |
| Archive comments | Partial | `CMT` service header handled specially |
| Quick-open records | Not a public feature | Avoid inconsistent listing/extraction behavior |
| Recovery records | Skipped/partial | Not recovery reconstruction API |

## SharpCompress Read Behavior

Header flow:

1. `MarkHeader.Read` scans for RAR4 or RAR5 signature and rejects old pre-RAR4 signatures.
2. `RarHeaderFactory.ReadHeaders` records whether the archive is RAR5.
3. `RarHeader.TryReadBase` reads common header fields and CRC state.
4. `RarHeaderFactory` creates typed headers based on header code.
5. File data is either skipped, wrapped in `ReadOnlySubStream`, or wrapped in decryption stream depending on mode and encryption metadata.

Seekable mode:

- File data positions are recorded in `FileHeader.DataStartPosition`.
- Streams can seek over packed data while collecting headers.

Streaming mode:

- File data is exposed as a substream for file headers.
- Non-file service data is skipped unless specially handled.
- Consumers must process entries in archive order.

Decompression:

- `RarStream` wraps the packed stream and an `IRarUnpack` implementation.
- `RarStream` is non-seekable.
- `RarStream.Initialize` starts unpacking based on the parsed `FileHeader`.

## Known Limitations

Keep these limitations explicit in code comments, docs, and tests:

- No RAR writing support.
- No support for pre-RAR4 archives.
- Service headers are not fully modeled by public APIs.
- Quick-open records are not a general public feature and have security-sensitive listing/extraction consistency requirements.
- Recovery record reconstruction is not exposed as a full repair feature.
- RAR5 Unix owner records and service data records are mostly skipped unless a specific behavior is implemented.
- Redirection metadata is surfaced, but extraction semantics for all link types should be treated carefully and tested for security.
- Encrypted archive support depends on password, encryption version, and KDF limits.
- Solid and multivolume archives must be tested with sequential extraction scenarios.

## Test Fixtures

Representative RAR test files live under `tests/TestArchives/Archives/` and related test archive folders. Use existing fixtures when possible instead of adding new binary archives.

Representative test files:

- `tests/SharpCompress.Test/Rar/RarArchiveTests.cs`
- `tests/SharpCompress.Test/Rar/RarArchiveAsyncTests.cs`
- `tests/SharpCompress.Test/Rar/RarReaderTests.cs`
- `tests/SharpCompress.Test/Rar/RarReaderAsyncTests.cs`
- `tests/SharpCompress.Test/Rar/RarHeaderFactoryTest.cs`
- `tests/SharpCompress.Test/Rar/RarCRCTest.cs`

When changing RAR behavior, include both Archive and Reader API coverage where applicable. For solid or multivolume behavior, prefer tests that extract entries sequentially and verify stream ownership and volume transitions.
