# Tar Gap Analysis

## Scope

This document compares the current Tar documentation, tests, and code paths in SharpCompress.

It is intentionally implementation-focused. The goal is to identify mismatches, omissions, and incomplete areas in the current SharpCompress Tar support.

Primary references:

- `docs/FORMATS.md`
- `src/SharpCompress/Factories/TarFactory.cs`
- `src/SharpCompress/Factories/TarWrapper.cs`
- `src/SharpCompress/Archives/Tar/`
- `src/SharpCompress/Readers/Tar/`
- `src/SharpCompress/Writers/Tar/`
- `src/SharpCompress/Common/Tar/`
- `tests/SharpCompress.Test/Tar/`

## Implemented Since Baseline

- `Tar.XZ` is now documented as read-only (`Writer API = N/A`) in `docs/FORMATS.md`.
- Local PAX extended headers (`x`) are now implemented on the read path for selected keys.
- Tar tests now include local PAX coverage for reader/archive sync and async paths.
- `TarWriterOptions.HeaderFormat` is now honored in sync and async file and directory write paths.
- Tar tests now cover `USTAR` and `GNU_TAR_LONG_LINK`, including USTAR long-name failure scenarios.
- Symlink coverage now includes `TarWithSymlink.tar.gz` for reader sync and async paths.

## Claimed vs Actual Support

### `Tar.XZ` is read-only

`tar.xz` is supported for reading, but not for writing.

Actual implementation in `src/SharpCompress/Writers/Tar/TarWriter.cs` does not support `CompressionType.Xz`. The writer throws `InvalidFormatException` for any compression type outside:

- `None`
- `GZip`
- `BZip2`
- `LZip`

Impact:

- Tar write support is narrower than Tar read support
- `tar.xz` creation is not available through the built-in Tar writer

Recommended action:

- keep the format table marked `N/A` for Tar.XZ writer support

## Read-Path Gaps

### Local PAX headers are implemented; global PAX is still pending

Local POSIX PAX extended headers (`x`) are now supported on the read path.

Supported keys in the current implementation:

- `path`
- `linkpath`
- `size`
- `mtime`
- `uid`
- `gid`
- `mode`

Remaining gap:

- global PAX extended headers (`g`) are still not semantically implemented

Recommended action:

- keep local PAX key support documented as implemented
- implement global PAX (`g`) separately when cross-entry metadata state is added

### Sparse files are not semantically implemented

`EntryType` defines `SparseFile`, but the read path does not contain sparse map handling or sparse reconstruction logic.

Evidence:

- `src/SharpCompress/Common/Tar/Headers/EntryType.cs`
- no sparse-specific code in `TarHeader`, `TarEntry`, `TarFilePart`, or `TarArchive`
- no sparse tests

Impact:

- sparse entries may be treated as ordinary entries rather than sparse files with holes

Recommended action:

- document sparse support as unsupported or partial
- add explicit tests if future support is added

### Global extended headers are not semantically implemented

`EntryType` defines `GlobalExtendedHeader`, but no semantic handling exists in the read pipeline.

Evidence:

- `TarHeader.Read` does not special-case `GlobalExtendedHeader`
- `TarEntry` does not surface a global-header model
- no tests cover this case

Impact:

- global metadata records are not applied in a defined way

Recommended action:

- document as unsupported until explicit behavior exists

### Device and FIFO semantics are not surfaced

The entry type enum includes `CharDevice`, `BlockDevice`, and `Fifo`, but the public tar model does not expose device metadata semantics.

Impact:

- such entries may not round-trip meaningfully through the API
- behavior is undocumented and untested

Recommended action:

- either document them as raw/unmodeled entry types or add dedicated support

## Write-Path Gaps

### `HeaderFormat` consistency is resolved

`TarWriterOptions.HeaderFormat` is now applied across:

- sync file writes
- sync directory writes
- async file writes
- async directory writes

Regression tests now cover both `USTAR` and `GNU_TAR_LONG_LINK` behavior.

### No public link-writing support

The read path supports symbolic and hard link targets through `TarEntry.LinkTarget`, but the write API exposes only regular file and directory creation.

Impact:

- symlink and hardlink tar archives cannot be created through the current public Tar writer API

Recommended action:

- either document this as a deliberate limitation or add link-writing APIs

### Metadata round-trip support is incomplete

The writer does not round-trip rich tar metadata beyond the basic fields needed for file and directory entries.

Current write behavior sets fixed defaults for some fields such as mode, owner id, and group id.

Impact:

- modified or newly created tar archives may lose metadata fidelity relative to the original archive

Recommended action:

- document current metadata write behavior clearly
- expand metadata support only if needed by consumers

### No write support for some detected wrappers

The detection and read path supports wrappers that the write path does not support.

| Wrapper | Read support | Write support |
| ------- | ------------ | ------------- |
| `tar.xz` | Yes | No |
| `tar.zst` | Yes | No |
| `tar.Z` | Yes | No |

This is not inherently wrong, but it should be clearly documented everywhere support is summarized.

## Sync and Async API Inconsistencies

### Seekability requirements differ at the API boundary

Synchronous `TarArchive.OpenArchive(Stream)` explicitly throws if the stream is not seekable.

Asynchronous `TarArchive.OpenAsyncArchive(Stream)` does not perform the same public guard.

Impact:

- callers do not see the same contract from sync and async overloads
- behavior is harder to reason about from API docs alone

Recommended action:

- either align the contracts or document the difference explicitly

### Header format alignment between sync and async is resolved

Sync and async Tar writer paths now both honor `TarWriterOptions.HeaderFormat`, and matching tests are present for both paths.

## Test Coverage Gaps

### Symlink coverage is now present for reader paths

Symlink behavior is now asserted for sync and async reader paths using:

- `tests/TestArchives/Archives/TarWithSymlink.tar.gz`

Archive-path symlink assertions currently rely on small tar fixtures rather than this large compressed sample.

### Header format coverage is now present

Tar tests now cover:

- `TarWriterOptions.HeaderFormat = USTAR`
- `TarWriterOptions.HeaderFormat = GNU_TAR_LONG_LINK`
- long-name failure in USTAR mode
- long-name success in GNU mode through sync and async writer paths

### No tests for sparse or global PAX headers

Local PAX coverage now exists, but there is still no evidence of coverage for:

- global extended headers
- sparse tar entries

Impact:

- unsupported or partial behavior is neither documented by tests nor protected from regression

Recommended action:

- either add fixtures and tests or document these as unsupported with no test coverage

### No tests for unsupported write wrappers

There are negative tests for an invalid `Rar` compression type, but not for unsupported tar wrappers that a user might reasonably infer from read support.

Missing negative cases include:

- `CompressionType.Xz`
- `CompressionType.ZStandard`
- `CompressionType.Lzw`

Recommended action:

- add explicit negative tests so the supported write matrix stays intentional

## Documentation Gaps

### Current format documentation is too coarse for Tar

`docs/FORMATS.md` summarizes support at the wrapper level, but Tar behavior depends on more than wrapper compression.

Missing implementation-specific details include:

- GNU long-name and long-link support
- USTAR prefix handling
- oldgnu numeric quirk handling
- partial PAX support boundaries (local supported, global pending)
- missing sparse support
- reader vs archive behavior differences for compressed tar
- file-size requirements for writing from non-seekable sources

Recommended action:

- keep `docs/FORMATS.md` high-level
- add and maintain a dedicated Tar spec document for details

### The current docs do not call out partial support clearly

The codebase supports some tar dialect features and not others, but the docs do not separate:

- fully supported
- partially supported
- unsupported

Recommended action:

- use an explicit feature matrix in the Tar documentation

## Recommended Follow-Ups

### Priority 1

- Implement and document global PAX (`g`) support
- Decide and document the support position for sparse files
- Decide and document support boundaries for non-modeled PAX keys (`uname`, `gname`, vendor keys)

### Priority 2

- Add negative writer tests for unsupported wrapper compressions
- Evaluate whether sync and async archive open contracts should match exactly
- Improve metadata round-trip behavior only if there is a consumer need

## Summary

The SharpCompress Tar implementation is strong on common read scenarios and basic write scenarios, but the current gaps fall into four categories:

- documentation overstating or under-describing support
- incomplete feature coverage for less common tar dialect features
- archive/read behavioral differences that are not always explicit in docs
- test coverage holes around advanced tar metadata features

`docs/TAR_SPEC.md` should be treated as the implementation baseline. This document identifies where that baseline is incomplete, inconsistent, or incorrectly reflected elsewhere in the repository.
