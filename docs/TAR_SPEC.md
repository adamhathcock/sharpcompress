# Tar Spec

## Scope

This document describes the Tar implementation that exists in SharpCompress today.

It is intentionally SharpCompress-specific. It documents actual behavior in the current codebase, including partial support and limitations. It is not a general tar format reference.

Primary implementation files:

- `src/SharpCompress/Factories/TarFactory.cs`
- `src/SharpCompress/Factories/TarWrapper.cs`
- `src/SharpCompress/Archives/Tar/TarArchive.cs`
- `src/SharpCompress/Archives/Tar/TarArchive.Async.cs`
- `src/SharpCompress/Archives/Tar/TarArchive.Factory.cs`
- `src/SharpCompress/Readers/Tar/TarReader.cs`
- `src/SharpCompress/Readers/Tar/TarReader.Async.cs`
- `src/SharpCompress/Writers/Tar/TarWriter.cs`
- `src/SharpCompress/Writers/Tar/TarWriter.Async.cs`
- `src/SharpCompress/Writers/Tar/TarWriterOptions.cs`
- `src/SharpCompress/Common/Tar/Headers/TarHeader.cs`
- `src/SharpCompress/Common/Tar/Headers/TarHeader.Async.cs`
- `src/SharpCompress/Common/Tar/TarHeaderFactory.cs`
- `src/SharpCompress/Common/Tar/TarHeaderFactory.Async.cs`

## API Surface

SharpCompress exposes Tar support through four main entry points.

| Type | Role |
| ---- | ---- |
| `TarFactory` | Format detection and factory entry point for archive, reader, and writer APIs |
| `TarArchive` | Archive API for enumerating and rewriting tar archives |
| `TarReader` | Forward-only reader API for streaming tar extraction |
| `TarWriter` | Forward-only writer API for creating tar archives |

`TarWriterOptions` controls output compression, stream ownership, archive finalization, encoding, and header write format.

## Supported Wrapper Formats

Tar wrapper detection is defined by `TarWrapper.Wrappers` in `src/SharpCompress/Factories/TarWrapper.cs`.

### Supported Extensions

| Wrapper | Extensions |
| ------- | ---------- |
| Plain tar | `tar` |
| Tar + BZip2 | `tar.bz2`, `tb2`, `tbz`, `tbz2`, `tz2` |
| Tar + GZip | `tar.gz`, `taz`, `tgz` |
| Tar + ZStandard | `tar.zst`, `tar.zstd`, `tzst`, `tzstd` |
| Tar + LZip | `tar.lz` |
| Tar + XZ | `tar.xz`, `txz` |
| Tar + LZW compress | `tar.Z`, `tZ`, `taZ` |

### API Support Matrix

| Wrapper | Detection | `TarArchive` read | `TarReader` read | `TarWriter` write |
| ------- | --------- | ----------------- | ---------------- | ----------------- |
| Plain tar | Yes | Yes | Yes | Yes |
| Tar + GZip | Yes | Yes | Yes | Yes |
| Tar + BZip2 | Yes | Yes | Yes | Yes |
| Tar + LZip | Yes | Yes | Yes | Yes |
| Tar + XZ | Yes | Yes | Yes | No |
| Tar + ZStandard | Yes | Yes | Yes | No |
| Tar + LZW compress | Yes | Yes | Yes | No |

Write support is implemented in `src/SharpCompress/Writers/Tar/TarWriter.cs` and currently accepts only `CompressionType.None`, `CompressionType.GZip`, `CompressionType.BZip2`, and `CompressionType.LZip`.

## Detection Behavior

Tar detection is implemented in `TarFactory.IsArchive`, `TarFactory.IsArchiveAsync`, `TarFactory.GetCompressionType`, and `TarFactory.GetCompressionTypeAsync`.

Detection behavior is:

1. Wrap the incoming stream in `SharpCompressStream`.
2. Start recording with a rewind buffer sized from `TarWrapper.MaximumRewindBufferSize`.
3. Probe each registered wrapper in order.
4. If a wrapper matches, create a decompression stream for that wrapper.
5. Call `TarArchive.IsTarFile` or `TarArchive.IsTarFileAsync` on the decompressed stream.
6. If the tar probe succeeds, treat the stream as tar with that wrapper compression.

Implications:

- Tar detection is content-based, not extension-based.
- Wrapper detection is not sufficient by itself. The decompressed payload must also parse as tar.
- Non-seekable detection is supported through the recording and rewind mechanism.
- The largest rewind requirement currently comes from BZip2, which declares a larger minimum probe buffer in `TarWrapper`.

`TarArchive.IsTarFile` and `TarArchive.IsTarFileAsync` attempt to read a single tar header and return `false` on any exception. They also treat an all-zero empty archive block as a valid empty tar archive when the entry type is defined.

## Reader Behavior

`TarReader` is the forward-only streaming API.

Implementation files:

- `src/SharpCompress/Readers/Tar/TarReader.cs`
- `src/SharpCompress/Readers/Tar/TarReader.Async.cs`
- `src/SharpCompress/Common/Tar/TarEntry.cs`
- `src/SharpCompress/Common/Tar/TarEntry.Async.cs`
- `src/SharpCompress/Common/Tar/TarReadOnlySubStream.cs`

Reader behavior:

- The reader always enumerates entries in streaming mode.
- It works with non-seekable input streams.
- It applies decompression based on the detected wrapper compression type before parsing tar headers.
- Entry streams are backed by `TarReadOnlySubStream`.

`TarReadOnlySubStream` has an important behavior: disposing an entry stream consumes any unread entry bytes and any required 512-byte padding so that the next header can be read correctly. This is what makes skipping entries work in streaming mode.

### Reader Compression Mapping

`TarReader.RequestInitialStream` and `RequestInitialStreamAsync` map the detected wrapper to the corresponding decompression stream:

- `None`
- `BZip2`
- `GZip`
- `ZStandard`
- `LZip`
- `Xz`
- `Lzw`

### Reader Entry Semantics

For each entry, SharpCompress exposes:

- `Key` from the parsed tar name
- `LinkTarget` for symbolic and hard links
- `Size`
- `CompressedSize`
- `LastModifiedTime`
- `IsDirectory`
- `Mode`
- `UserID`
- `GroupId`

Tar entries are always reported as unencrypted and CRC is always `0`.

## Archive Behavior

`TarArchive` is the archive API.

Implementation files:

- `src/SharpCompress/Archives/Tar/TarArchive.cs`
- `src/SharpCompress/Archives/Tar/TarArchive.Async.cs`
- `src/SharpCompress/Archives/Tar/TarArchive.Factory.cs`

### Open Behavior

Synchronous `TarArchive.OpenArchive(Stream)` requires a seekable stream and throws `ArgumentException` when `CanSeek` is `false`.

`TarArchive.OpenArchive(FileInfo)` and the list-based overloads use `SourceStream` and determine wrapper compression by calling `TarFactory.GetCompressionType`.

Asynchronous `OpenAsyncArchive` overloads use `TarFactory.GetCompressionTypeAsync` and do not enforce the same explicit seekability check at the public API boundary.

### Entry Loading

`TarArchive.LoadEntries` and `LoadEntriesAsync` parse entries differently depending on wrapper compression:

- Uncompressed tar uses `StreamingMode.Seekable`.
- Wrapped tar uses `StreamingMode.Streaming` because the decompressed stream is not treated as random-access.

When seekable mode is used, the header stores `DataStartPosition`, and entries reopen data through `TarFilePart` by seeking back to the data position.

When streaming mode is used, the header stores a `PackedStream`, and entry access follows streaming semantics over the decompressed stream.

### Archive Rewrite Behavior

`TarArchive` supports creating and modifying archives through `AbstractWritableArchive`:

- add file entries
- add directory entries
- remove entries
- save to a new stream or path

Archive rewrite is implemented by enumerating the existing and new entries and writing them back out through `TarWriter`.

## Writer Behavior

`TarWriter` is the forward-only tar writer.

Implementation files:

- `src/SharpCompress/Writers/Tar/TarWriter.cs`
- `src/SharpCompress/Writers/Tar/TarWriter.Async.cs`
- `src/SharpCompress/Writers/Tar/TarWriterOptions.cs`

### Supported Output Compression

The writer supports these output compression types:

- `CompressionType.None`
- `CompressionType.GZip`
- `CompressionType.BZip2`
- `CompressionType.LZip`

Any other compression type causes `InvalidFormatException`.

### Stream Ownership

If `LeaveStreamOpen` is `true`, `TarWriter` wraps the destination in a non-disposing stream.

### File Writing

`TarWriter.Write` and `WriteAsync` write a tar header followed by file contents, then pad the payload to the next 512-byte boundary.

If the source stream is non-seekable and the caller does not supply `size`, the writer throws `ArgumentException` because tar requires the file size in the header.

### Directory Writing

`WriteDirectory` and `WriteDirectoryAsync` normalize the directory name to use forward slashes and ensure the key ends with `/`.

Empty or root-equivalent directory names are skipped.

### Archive Finalization

If `FinalizeArchiveOnClose` is `true`, disposing the writer writes two 512-byte zero blocks to terminate the archive.

If the output stream implements `IFinishable`, dispose also calls `Finish()`.

## Header Write Formats

`TarHeaderWriteFormat` is defined in `src/SharpCompress/Common/Tar/Headers/TarHeaderWriteFormat.cs`.

Supported write formats:

- `GNU_TAR_LONG_LINK`
- `USTAR`

`TarWriterOptions.HeaderFormat` defaults to `GNU_TAR_LONG_LINK`.

Current implementation behavior is narrower than the option surface suggests:

- sync file writes use the configured `HeaderFormat`
- sync directory writes currently construct the default tar header format
- async file writes currently construct the default tar header format
- async directory writes currently construct the default tar header format

In practice, this means the configured `HeaderFormat` is currently honored only by the synchronous file write path.

### GNU Long Name Write Behavior

In GNU mode, when a file name exceeds the 100-byte field, `TarHeader.WriteGnuTarLongLink` writes a synthetic long-name header using `././@LongLink` and `EntryType.LongName`, then writes the long name payload, and finally writes the actual file entry.

GNU mode also writes large file sizes using binary size encoding when the size does not fit the standard octal field.

### USTAR Write Behavior

When the synchronous file write path is configured for `USTAR`, `TarHeader.WriteUstar` attempts to split a long path into:

- the main `name` field
- the `prefix` field

If the name cannot be represented in USTAR field limits, the writer throws `InvalidFormatException` and instructs the caller to use GNU Tar format instead.

## Header Read Behavior

Tar header parsing is implemented in `TarHeader.Read` and `TarHeader.ReadAsync`.

### Implemented Read Features

| Feature | Read support |
| ------- | ------------ |
| Regular file entries | Yes |
| Directory entries | Yes |
| Symbolic link target reading | Yes |
| Hard link target reading | Yes |
| GNU long name (`L`) | Yes |
| GNU long link (`K`) | Yes |
| PAX local extended header (`x`) | Yes (selected keys) |
| USTAR prefix reconstruction | Yes |
| Binary size field parsing | Yes |
| oldgnu uid/gid numeric quirk parsing | Yes |
| POSIX and signed checksum validation | Yes |

### Entry Types Recognized by the Code

`EntryType` currently declares these values in `src/SharpCompress/Common/Tar/Headers/EntryType.cs`:

- `File`
- `OldFile`
- `HardLink`
- `SymLink`
- `CharDevice`
- `BlockDevice`
- `Directory`
- `Fifo`
- `LongLink`
- `LongName`
- `SparseFile`
- `VolumeHeader`
- `LocalExtendedHeader`
- `GlobalExtendedHeader`

SharpCompress currently has explicit handling for only a subset of those values during read and write.

### Long Name and Long Link Reads

When `TarHeader.Read` encounters `EntryType.LongName` or `EntryType.LongLink`, it reads the payload and applies it to the next real header.

Long-name payload reads are capped at `32768` bytes to avoid memory exhaustion from malformed archives.

### PAX Local Header Reads

SharpCompress now consumes local PAX extended headers (`x`) and applies supported key overrides to the next real entry.

Currently supported keys:

- `path`
- `linkpath`
- `size`
- `mtime`
- `uid`
- `gid`
- `mode`

Unknown PAX keys are ignored.

### Name Reconstruction

For USTAR headers, if the magic field is `ustar` and the prefix field is populated, SharpCompress reconstructs the entry name as `prefix + "/" + name`.

## Name and Metadata Handling

### Path Normalization

Writer path normalization is implemented in `TarWriter.NormalizeFilename` and `NormalizeDirectoryName`.

Behavior:

- backslashes are converted to `/`
- drive prefixes before `:` are removed
- leading and trailing `/` are trimmed for file entries
- directory entries are normalized to end with `/`

### Encoding

Tar name encoding and decoding is controlled by `IArchiveEncoding`.

- reader APIs decode names with `ReaderOptions.ArchiveEncoding`
- writer APIs encode names with `TarWriterOptions.ArchiveEncoding`

The tests include UTF-8 and code page coverage for tar name handling.

### Metadata Surface

Tar metadata currently surfaced through `TarEntry` includes:

- name
- link target
- mode
- uid
- gid
- size
- last modified time

Writer metadata is narrower. The writer sets:

- `LastModifiedTime`
- `Name`
- `Size`
- entry type for file or directory

The current writer writes fixed mode, owner id, and group id defaults rather than round-tripping full metadata.

## Async Behavior

Async tar support is provided by:

- `TarArchive.OpenAsyncArchive`
- `TarReader.OpenAsyncReader`
- `TarWriter.WriteAsync`
- `TarWriter.WriteDirectoryAsync`
- `TarHeader.ReadAsync`
- `TarHeader.WriteAsync`

The async implementations generally mirror the sync implementations while using async header parsing, decompression, and stream copy paths. The most important current exception is `TarWriterOptions.HeaderFormat`, which is not consistently honored outside the synchronous file write path.

## Known Limitations

This section documents current implementation limits, not desired future behavior.

### Write limitations

- No write support for `tar.xz`
- No write support for `tar.zst`
- No write support for `tar.Z`
- No public API for writing symbolic links or hard links
- No PAX write support
- No sparse file write support
- No device or FIFO write support

### Read limitations or partial support

- No PAX global extended header (`g`) support
- Local PAX support is limited to selected keys (`path`, `linkpath`, `size`, `mtime`, `uid`, `gid`, `mode`)
- No semantic sparse file handling beyond recognizing the entry type enum value
- No semantic global extended header handling beyond recognizing the entry type enum value
- No special device or FIFO object model beyond the raw entry type information available internally

### Archive behavior limitations

- Sync archive open requires a seekable input stream
- Compressed tar archive access is not full random-access in the same sense as uncompressed seekable tar

## Test Coverage Map

Tar tests live in `tests/SharpCompress.Test/Tar/`.

Representative coverage:

| Area | Tests |
| ---- | ----- |
| Wrapper detection and reading | `TarReaderTests.cs`, `TarReaderAsyncTests.cs` |
| Archive open and rewrite | `TarArchiveTests.cs`, `TarArchiveAsyncTests.cs` |
| Writer behavior | `TarWriterTests.cs`, `TarWriterAsyncTests.cs` |
| Directory entry behavior | `TarWriterDirectoryTests.cs`, `TarArchiveDirectoryTests.cs` |
| Long-name behavior | `TarArchiveTests.cs`, `TarReaderTests.cs` |
| Corruption and broken stream handling | `TarReaderTests.cs`, `TarReaderAsyncTests.cs` |

Representative tar test archives in `tests/TestArchives/Archives/`:

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
- `Tar.Empty.tar`
- `TarCorrupted.tar`
- `TarWithSymlink.tar.gz`

## Summary

SharpCompress Tar support is centered around:

- broad read support for common tar wrappers
- forward-only reader behavior for streamed extraction
- seekable archive support for uncompressed tar and archive rewrite workflows
- narrower write support than read support
- GNU long-name and USTAR write support
- PAX local header (`x`) read support for selected metadata keys
- partial coverage for less common tar dialect features
