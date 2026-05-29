# Tar Format Reference

This reference summarizes the Tar archive container format for SharpCompress work. It is locally authored from public format references and the current SharpCompress implementation.

Primary external references:

- POSIX `pax` and `ustar`: https://pubs.opengroup.org/onlinepubs/9699919799/utilities/pax.html
- GNU tar basic format: https://www.gnu.org/software/tar/manual/html_node/Standard.html
- GNU tar extensions: https://www.gnu.org/software/tar/manual/html_node/Extensions.html

Primary SharpCompress references:

- `docs/TAR_SPEC.md`
- `docs/TAR_GAP_ANALYSIS.md`
- `src/SharpCompress/Factories/TarFactory.cs`
- `src/SharpCompress/Factories/TarWrapper.cs`
- `src/SharpCompress/Common/Tar/Headers/TarHeader.cs`
- `src/SharpCompress/Common/Tar/Headers/TarHeader.Async.cs`
- `src/SharpCompress/Common/Tar/Headers/EntryType.cs`
- `src/SharpCompress/Writers/Tar/TarWriter.cs`
- `src/SharpCompress/Writers/Tar/TarWriter.Async.cs`
- `src/SharpCompress/Writers/Tar/TarWriterOptions.cs`
- `tests/SharpCompress.Test/Tar/`

## Contents

- [Format Overview](#format-overview)
- [USTAR Header Layout](#ustar-header-layout)
- [Entry Type Flags](#entry-type-flags)
- [Numeric Fields](#numeric-fields)
- [Checksum](#checksum)
- [Path Names](#path-names)
- [PAX Extended Headers](#pax-extended-headers)
- [GNU Tar Extensions](#gnu-tar-extensions)
- [SharpCompress Support Matrix](#sharpcompress-support-matrix)
- [SharpCompress Read Behavior](#sharpcompress-read-behavior)
- [SharpCompress Write Behavior](#sharpcompress-write-behavior)
- [Known Limitations](#known-limitations)
- [Test Fixtures](#test-fixtures)

## Format Overview

A tar archive is a sequence of 512-byte blocks. Each archive member is represented by:

```text
header block (512 bytes)
payload blocks, padded to a 512-byte boundary
```

The archive should end with two 512-byte blocks filled with zero bytes. Readers should be tolerant of missing end markers because real-world tar tools may produce archives without them.

The header contains file metadata and the payload size. Tar has no central directory. Streaming readers must consume or skip each payload and its padding before the next header can be parsed.

SharpCompress relies on this in `TarReadOnlySubStream`: disposing an entry stream consumes unread entry bytes plus 512-byte padding so the next header remains aligned.

## USTAR Header Layout

The POSIX USTAR header is exactly 512 bytes. Field offsets are byte offsets from the beginning of the header block.

| Field | Offset | Length | Notes |
| ----- | ------ | ------ | ----- |
| `name` | 0 | 100 | File name or final path component |
| `mode` | 100 | 8 | Octal file mode |
| `uid` | 108 | 8 | Octal owner id |
| `gid` | 116 | 8 | Octal group id |
| `size` | 124 | 12 | Octal payload size, or GNU base-256 in some archives |
| `mtime` | 136 | 12 | Octal seconds since Unix epoch |
| `chksum` | 148 | 8 | Header checksum |
| `typeflag` | 156 | 1 | Entry type |
| `linkname` | 157 | 100 | Link target for hard/symbolic links |
| `magic` | 257 | 6 | Usually `ustar` followed by NUL |
| `version` | 263 | 2 | Usually `00` |
| `uname` | 265 | 32 | Owner name |
| `gname` | 297 | 32 | Group name |
| `devmajor` | 329 | 8 | Character/block device major number |
| `devminor` | 337 | 8 | Character/block device minor number |
| `prefix` | 345 | 155 | USTAR path prefix |
| padding | 500 | 12 | Unused padding to 512 bytes |

SharpCompress parses the core fields in `TarHeader.Read` and `TarHeader.ReadAsync`. It reconstructs USTAR paths as `prefix + "/" + name` when `magic` is exactly `ustar` and `prefix` is non-empty.

## Entry Type Flags

Common POSIX typeflags:

| Typeflag | Meaning |
| -------- | ------- |
| NUL | Regular file, older tar form |
| `0` | Regular file |
| `1` | Hard link |
| `2` | Symbolic link |
| `3` | Character device |
| `4` | Block device |
| `5` | Directory |
| `6` | FIFO |
| `7` | Contiguous file, reserved by POSIX historical usage |
| `x` | POSIX PAX local extended header for the following file |
| `g` | POSIX PAX global extended header for following files |

GNU and other extension typeflags relevant to SharpCompress:

| Typeflag | Meaning |
| -------- | ------- |
| `K` | GNU long link target for the next real entry |
| `L` | GNU long path name for the next real entry |
| `S` | GNU sparse file |
| `V` | GNU volume header |

SharpCompress declares these in `EntryType.cs`:

```text
File = 0
OldFile = '0'
HardLink = '1'
SymLink = '2'
CharDevice = '3'
BlockDevice = '4'
Directory = '5'
Fifo = '6'
LongLink = 'K'
LongName = 'L'
SparseFile = 'S'
VolumeHeader = 'V'
LocalExtendedHeader = 'x'
GlobalExtendedHeader = 'g'
```

Declaration does not mean full semantic support. Sparse, device, FIFO, and volume-header semantics are not fully modeled by the public API.

## Numeric Fields

Standard tar numeric fields are ASCII octal values, usually NUL-terminated or space-padded depending on writer. Important fields include `mode`, `uid`, `gid`, `size`, `mtime`, `chksum`, `devmajor`, and `devminor`.

GNU tar can use base-256 binary encoding for values that exceed the octal field range:

- A leading byte with bit `0x80` indicates a positive binary value.
- GNU documentation also describes `0xff` as a negative two's-complement marker.
- The value bytes are big-endian.

SharpCompress currently handles binary `size` fields when bit `0x80` is set and writes large sizes in GNU long-link mode using a base-256-style binary `size` field. It also has an old GNU uid/gid quirk reader for fields beginning with `0x80 0x00`.

## Checksum

The checksum is the simple sum of all 512 header bytes, treating the 8-byte checksum field at offset 148 as spaces (`0x20`) during calculation.

SharpCompress checksum behavior:

- `RecalculateChecksum` fills the checksum field with eight spaces and sums bytes as unsigned values.
- `checkChecksum` accepts both POSIX unsigned sums and signed-byte sums used by some historical tar implementations.
- An all-zero block is treated as an empty/end marker case.

When editing parser code, keep checksum compatibility broad enough for old archives. When editing writer code, use POSIX unsigned checksum output.

## Path Names

Classic tar has a 100-byte `name` field. POSIX USTAR extends this with a 155-byte `prefix` field.

USTAR path reconstruction:

```text
full path = prefix + "/" + name
```

USTAR writer constraints:

- `name` must fit within the 100-byte name field.
- `prefix` must fit within the 155-byte prefix field.
- Splitting is normally done at a directory separator.

SharpCompress write behavior:

- `TarHeaderWriteFormat.USTAR` tries to split long paths into `prefix` and `name`.
- If a path cannot fit, SharpCompress throws `InvalidFormatException` and tells callers to use GNU tar format.
- `TarHeaderWriteFormat.GNU_TAR_LONG_LINK` writes GNU long-name metadata for names over 100 bytes.

SharpCompress path normalization in `TarWriter`:

- Converts backslashes to `/`.
- Removes drive prefixes before `:`.
- Trims leading and trailing `/` for file entries.
- Ensures directory entries end with `/`.
- Skips empty or root-equivalent directory names.

## PAX Extended Headers

POSIX PAX uses regular tar header blocks with special typeflags and a payload of UTF-8 key/value records.

PAX header typeflags:

- `x`: local extended header, applies to the next real file entry.
- `g`: global extended header, applies to subsequent entries until overridden by another global or local header.

Each record has this form:

```text
<decimal-length> <keyword>=<value>\n
```

The decimal length includes every byte in the record, including the digits of the length itself, the space, key, equals sign, value, and newline.

SharpCompress PAX read support is intentionally limited to selected keys:

| Key | Effect |
| --- | ------ |
| `path` | Overrides entry path/name |
| `linkpath` | Overrides hard/symbolic link target |
| `size` | Overrides payload size |
| `mtime` | Overrides modification time |
| `uid` | Overrides owner id |
| `gid` | Overrides group id |
| `mode` | Overrides mode |

Local metadata overrides global metadata. Unknown PAX keys are ignored. PAX payload reads are capped at 65536 bytes to avoid memory exhaustion from malformed archives.

SharpCompress does not currently write PAX headers.

## GNU Tar Extensions

SharpCompress supports the most common GNU extensions needed for interoperability.

### Long Name and Long Link

GNU long-name and long-link records are synthetic entries that apply to the next real entry:

| Typeflag | Purpose |
| -------- | ------- |
| `L` | Long file name/path for next entry |
| `K` | Long link target for next entry |

The payload contains the long name or link target, padded to a 512-byte boundary. SharpCompress caps long-name payload reads at 32768 bytes.

Writer behavior in `GNU_TAR_LONG_LINK` mode:

1. Write a synthetic `././@LongLink` header with `typeflag = 'L'` when the name exceeds 100 bytes.
2. Write the long-name payload and 512-byte padding.
3. Write the actual file or directory header.

### Sparse Files

GNU sparse tar uses `typeflag = 'S'` and additional sparse-map metadata. POSIX PAX sparse variants use keys such as `GNU.sparse.*`.

SharpCompress currently recognizes the sparse entry type enum value but does not reconstruct sparse holes or semantically expose sparse maps. Treat sparse support as unsupported unless implementing full reconstruction and tests.

### Base-256 Numeric Fields

GNU tar uses base-256 binary fields for out-of-range numeric values. SharpCompress reads binary `size` fields and writes large sizes in GNU mode.

## SharpCompress Support Matrix

Wrapper detection is defined in `TarWrapper.Wrappers`. Detection is content-based: wrapper detection is followed by a tar-header probe of the decompressed payload.

| Wrapper | Extensions | Read | Write |
| ------- | ---------- | ---- | ----- |
| Plain tar | `tar` | Yes | Yes |
| Tar + GZip | `tar.gz`, `taz`, `tgz` | Yes | Yes |
| Tar + BZip2 | `tar.bz2`, `tb2`, `tbz`, `tbz2`, `tz2` | Yes | Yes |
| Tar + LZip | `tar.lz` | Yes | Yes |
| Tar + XZ | `tar.xz`, `txz` | Yes | No |
| Tar + ZStandard | `tar.zst`, `tar.zstd`, `tzst`, `tzstd` | Yes | No |
| Tar + LZW compress | `tar.Z`, `tZ`, `taZ` | Yes | No |

Writer support currently accepts only these compression types:

- `CompressionType.None`
- `CompressionType.GZip`
- `CompressionType.BZip2`
- `CompressionType.LZip`

Other compression types throw `InvalidFormatException`.

## SharpCompress Read Behavior

Reader API:

- `TarReader` is forward-only and supports non-seekable streams.
- `ReaderFactory.OpenReader` can auto-detect tar and wrapper compression.
- Entry streams must be consumed or disposed so the next header can be aligned.

Archive API:

- `TarArchive.OpenArchive(Stream)` and `TarArchive.OpenAsyncArchive(Stream)` require seekable streams.
- File/path overloads own the opened file stream.
- Compressed tar archive access follows streaming semantics over the decompressed stream rather than full random-access semantics.

Parsed metadata surfaced through entries includes:

- `Key`
- `LinkTarget`
- `Size`
- `CompressedSize`
- `LastModifiedTime`
- `IsDirectory`
- `Mode`
- `UserID`
- `GroupId`

Tar entries are always reported as unencrypted and CRC is always `0`.

## SharpCompress Write Behavior

`TarWriter` is forward-only. It writes a header, payload, payload padding, and finally two zero blocks on dispose when `FinalizeArchiveOnClose` is true.

Important writer rules:

- Tar requires payload size in the header.
- If the source stream is non-seekable and no `size` is supplied, `TarWriter` throws `ArgumentException`.
- `TarWriterOptions.HeaderFormat` defaults to `GNU_TAR_LONG_LINK`.
- Current sync and async file and directory write paths honor `HeaderFormat`.
- `USTAR` mode writes USTAR headers and throws when paths cannot fit.
- `GNU_TAR_LONG_LINK` mode writes GNU long-name records for long paths.
- The public writer supports regular files and directories, not links, devices, FIFOs, sparse maps, or PAX metadata.

Writer metadata is narrower than reader metadata. It writes name, size, last modified time, and file/directory type, and uses fixed mode/user/group defaults in the header.

## Known Limitations

Keep these limitations explicit in code comments, docs, and tests:

- No PAX write support.
- No sparse-file reconstruction or sparse write support.
- No public API for writing symbolic links or hard links.
- No device or FIFO metadata object model beyond internal type recognition.
- No write support for `tar.xz`, `tar.zst`, or `tar.Z`.
- PAX read support is limited to `path`, `linkpath`, `size`, `mtime`, `uid`, `gid`, and `mode`.
- Unknown PAX keys are ignored.
- Stream-based `TarArchive` open requires seekable input.

## Test Fixtures

Representative fixtures in `tests/TestArchives/Archives/`:

- `Tar.tar`
- `Tar.tar.gz`
- `Tar.tar.bz2`
- `Tar.tar.lz`
- `Tar.tar.xz`
- `Tar.tar.zst`
- `Tar.tar.Z`
- `Tar.oldgnu.tar.gz`
- `very long filename.tar`
- `ustar with long names.tar`
- `Tar.LongPathsWithLongNameExtension.tar`
- `Tar.PaxLocalHeader.tar`
- `Tar.PaxLocalHeader.Link.tar`
- `Tar.PaxGlobalHeader.tar`
- `Tar.PaxGlobalHeader.Link.tar`
- `Tar.Empty.tar`
- `TarCorrupted.tar`
- `TarWithSymlink.tar.gz`

Representative test files:

- `tests/SharpCompress.Test/Tar/TarReaderTests.cs`
- `tests/SharpCompress.Test/Tar/TarReaderAsyncTests.cs`
- `tests/SharpCompress.Test/Tar/TarArchiveTests.cs`
- `tests/SharpCompress.Test/Tar/TarArchiveAsyncTests.cs`
- `tests/SharpCompress.Test/Tar/TarWriterTests.cs`
- `tests/SharpCompress.Test/Tar/TarWriterAsyncTests.cs`
- `tests/SharpCompress.Test/Tar/TarWriterDirectoryTests.cs`
- `tests/SharpCompress.Test/Tar/TarArchiveDirectoryTests.cs`
