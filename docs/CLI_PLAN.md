# SharpCompress .NET CLI Tool Plan

## Goal

Build a distributable .NET command-line tool that exposes SharpCompress archive and compression features and serves as a repeatable diagnostic and regression-testing tool.

The CLI should exercise the same public library operations used by other consumers. Focused changes to SharpCompress itself are authorized when needed to provide reusable inspection, verification, or diagnostic capabilities. Do not duplicate archive parsers or checksum implementations in the CLI.

**Guiding rule:** distinguish what an archive declares, what SharpCompress observes, and what the tool actually verifies.

## Proposed Packaging and Structure

- Package ID: `SharpCompress.Tool`.
- Executable: `sharpcompress`.
- CLI project: `src/SharpCompress.Cli/`, referencing the SharpCompress project.
- CLI test project: `tests/SharpCompress.Cli.Test/`.
- Initially target `net10.0`, unless broader runtime compatibility is required. Leave the library's existing target frameworks unchanged.
- Register projects in `SharpCompress.slnx` and follow central package management.
- Keep command parsing, operation execution, and output rendering separate.
- Prefer a maintained command-line parser, such as System.CommandLine; choose its version during implementation.
- Do not introduce a separate application framework or additional core project without a concrete need.

All command names, options, and new API names below are proposed, not existing functionality or finalized signatures.

## Command Surface

| Command | Purpose |
| --- | --- |
| `formats [name]` | Describe containers, methods, read/write support, encryption, and stream requirements. |
| `detect FILE` | Identify the container, outer compression, and API availability without enumerating entries. |
| `inspect FILE` | Report archive metadata, completeness, and inspection limitations. |
| `list FILE` | Enumerate entries; `--long` includes detailed entry metadata. |
| `extract FILE -o DIRECTORY` | Extract all or selected entries. |
| `cat FILE --entry NAME` | Write one entry's decompressed bytes to stdout. |
| `test FILE` | Fully decompress and perform available integrity validation without writing extracted files. |
| `create OUTPUT INPUT...` | Create archives through supported writers. |
| `compress INPUT --codec CODEC -o OUTPUT` | Compress a single stream rather than create a multi-entry archive. |
| `decompress INPUT -o OUTPUT` | Decompress a single stream. Require explicit codec selection where framing is ambiguous. |
| `manifest FILE` | Emit entry identities, metadata, and optional content hashes for comparison. |

Examples:

```sh
sharpcompress inspect backup.tar.xz --json
sharpcompress list backup.zip --long
sharpcompress extract backup.rar -o restored --include "**/*.txt"
sharpcompress cat backup.zip --entry README.md
sharpcompress test backup.7z --io sync
sharpcompress create backup.zip ./source --compression zstd
sharpcompress compress input.bin --codec gzip -o input.bin.gz
```

### Command Semantics

- Keep archive containers and compression codecs separate: ZIP with ZStandard, standalone ZStandard, and TAR wrapped in ZStandard are different operations.
- Detection does not establish metadata validity or payload integrity.
- Metadata inspection does not imply payload verification, even if sequential inspection must decompress data to advance.
- Support exact entry selection by ordinal/index as well as name, because duplicate names are valid.
- Define include/exclude matching and case sensitivity explicitly and consistently.
- Keep raw codecs requiring additional properties or framing out of the initial stream commands until their contracts are explicit.
- Validate unsupported options before creating output whenever possible.

### Creation Options

- ZIP: supported compression method/level, ZIP64, and archive comment.
- TAR: supported wrapper compression and header dialect.
- 7z: LZMA/LZMA2, encoder properties, compressed headers, password, and header encryption.
- GZip and other stream formats: only options supported by the selected implementation.
- Do not imply RAR writing, solid 7z writing, ZIP encryption writing, or unsupported TAR wrappers.
- Compression levels are algorithm-specific; do not silently accept an option that has no effect.

## Inspection Design

### Identification: `detect`

Use `SharpCompress.Detection.ArchiveDetector` for:

- Format name and logical container.
- Outer compression wrapper.
- Available Archive and Reader APIs.

Include input properties such as size, seekability, and supplied parts when available. Detection is not necessarily a fixed-size signature read, particularly for compressed TAR identification.

### Interpreted Metadata: `inspect` and `list --long`

Use existing `ArchiveInspector` results and entry enumeration as the starting point.

Archive-level details:

- Format/version where available.
- Entry count and count of entries with initially unknown sizes.
- Physical size, compressed payload size, and uncompressed payload size as distinct values.
- Solid compression and independent shared-stream count.
- Encryption scope: headers and/or entry data.
- Multipart status, physical part count, logical volume count, and archive completeness.
- Archive comments.
- Inspection status and limitations.
- ZIP entries with deferred sizes/data descriptors.

Entry-level details:

- Ordinal/index and exact decoded name.
- File/directory/link information and link target where available.
- Compression method.
- Declared compressed and uncompressed sizes.
- Encryption, solid, split, and completeness flags where available.
- First and last volume indexes.
- Modification, creation, access, and archived timestamps.
- Attributes and available checksum metadata.

Representation requirements:

- Unknown values must not become zero or false.
- Preserve duplicate entries and archive order.
- Preserve decoded archive names; report proposed extraction paths separately.
- Do not invent per-file compressed sizes or ratios for shared solid streams.
- Distinguish inspection completeness, archive completeness, and verification status.
- Explain partial metadata caused by unavailable fields separately from corruption.
- Document that sequential formats and compressed TAR may require complete scans.
- Avoid implying that every `IEntry` property is reliably implemented by every format.

### Structural Diagnostics: `inspect --structure`

Implement later through opt-in library diagnostics, not a separate CLI parser.

| Format | Desired structural information |
| --- | --- |
| ZIP | Local and central-directory records, discrepancies, flags, method IDs, extra fields, descriptors, ZIP64, and directory records. |
| RAR | Header/block types, flags, solid/split relationships, encryption records, service records, and checksum types. |
| 7z | Encoded headers, packed streams, folders, coder chains, substreams, and entry-to-folder relationships. |
| TAR | Header dialect, typeflags, PAX/GNU control records, and raw versus effective metadata. |
| XZ | Streams, blocks, filters, check types, index records, and footer information. |

Include source part, offset, and length when known. Label physical source offsets separately from offsets inside decompressed wrappers. Recognition of recovery or service records must not imply support for their full semantics or archive repair.

## Focused SharpCompress Library Additions

### 1. Reusable Verification

Introduce a public verification operation, provisionally `ArchiveVerifier`, with synchronous and asynchronous paths.

- Support Archive and Reader execution where available.
- Fully consume entries and finish relevant trailer processing and disposal before reporting success.
- Reuse existing checksum/authentication validation logic.
- Report actual decompressed byte counts separately from declared sizes.
- Distinguish decoding success, checksum verification, authentication, and unavailable checks.
- Support streaming per-entry results instead of requiring all results in memory.
- Support cancellation and clear partial/interrupted results.
- Allow optional SHA-256 content hashing for baseline comparisons; computing a hash is not verification against an expected value unless one is supplied.

Audit the current stream-copy paths first: simply copying `OpenEntryStream()` to `Stream.Null` does not universally apply the optional checksum wrappers. Resolve missing public checksum-aware execution paths in the library and prove behavior using corrupt fixtures.

### 2. Single-Pass Detailed Inspection

Existing `ArchiveInspector` returns a summary rather than entry snapshots. Add an explicit detailed inspection path capable of emitting identification, entries, volumes, format details, and a final summary in one scan.

- Preserve existing detection and inspection behavior and signatures.
- Keep metadata-focused inspection separate from deliberate full verification.
- Support streaming output for large archives.
- Avoid scanning compressed TAR twice merely to combine summary and entry details.
- Design the result/event contract before committing to public signatures.

### 3. Explicit Metadata Availability

Introduce result/snapshot metadata that can represent:

- Present, absent, unknown, and unsupported values.
- Checksum algorithm and expected value when exposed.
- Declared values versus observed values.
- Validation performed and its outcome.

Do not infer checksum absence from `IEntry.Crc == 0`. Prefer additive result types over new mandatory members on existing public interfaces. Do not expose cryptographic secrets or mutable parser objects.

### 4. Opt-In Parser Diagnostics

- Emit immutable structural snapshots from existing parsers.
- Keep overhead negligible when diagnostics are disabled.
- Make format-specific extensions explicit rather than flattening every format into ZIP-shaped fields.
- Clearly identify diagnostics unavailable because of encryption, missing parts, or unsupported metadata.
- Do not use reflection or privileged CLI access to library internals.

### 5. Capability Descriptions

Where practical, expose reusable descriptions of read/write availability, supported methods, seek requirements, encryption capabilities, and option restrictions.

Use these for `formats` and command validation. Derive capabilities from existing factories/providers where possible; explicitly describe and test restrictions that cannot be inferred. Enum membership alone is not evidence of support.

## Developer Testing Commands

| Command | Purpose |
| --- | --- |
| `dev matrix FILE` | Compare supported execution paths, content hashes, metadata, and failures. |
| `dev corpus DIRECTORY` | Run fixtures against declared expectations. |
| `dev roundtrip DIRECTORY` | Create, reopen, validate, and compare contents. |
| `dev compare BASELINE CURRENT` | Compare normalized manifests/reports. |

Matrix dimensions:

- Archive versus Reader API.
- Sync versus async.
- Sequential archive extraction versus individual-entry access.
- File-path versus caller-owned stream overloads.
- Seekable versus genuinely non-seekable streams.
- Short reads and different copy/rewind buffer sizes.
- Default versus built-in System compression providers where applicable.
- Optional parallel decoding where implemented.
- Cancellation, partial reads, disposal, and `LeaveStreamOpen` behavior.

Requirements:

- No silent fallback for forced diagnostic paths.
- Report unsupported combinations as unsupported/skipped, never passed.
- Reopen sources for independent runs; do not let earlier enumeration alter later cases.
- Never silently spool a non-seekable test input into a seekable stream.
- Compare metadata with awareness of deferred values and format limitations.
- Preserve duplicate entry identities in manifests and comparisons.
- Corpus expectations must distinguish valid, corrupt, unsupported, encrypted, and missing-volume fixtures.
- Save enough effective configuration to reproduce failures without saving passwords.
- Keep rigorous performance benchmarks in the existing BenchmarkDotNet project; CLI timings are diagnostic measurements.
- Changing `--io` does not change the compiled target framework. Runtime/TFM coverage requires separate builds and executions.

## Output, Safety, and Operational Contracts

- Human-readable output by default; versioned `--json` and streaming `--ndjson` formats.
- Define structured errors and exit codes before shipping; distinguish invalid arguments, unsupported operations, corruption/verification failure, incomplete input, cancellation, and operational I/O errors.
- Keep machine-readable error output consistent with the selected output mode.
- stdout contains results or payload; stderr contains progress and diagnostics.
- Disable terminal-only decoration in machine-readable output.
- Include tool, library, and runtime versions plus effective execution options in diagnostic reports.
- Exclude timing and machine-specific paths from normalized comparison output.
- Prefer password prompts or environment/file sources. Never include secrets in reports or reproduction commands.
- Default to no overwrite, with explicit overwrite and link policies.
- Reuse SharpCompress extraction path protections; do not weaken traversal or link checks.
- Enforce limits against actual decompressed bytes and entry counts, not only untrusted header declarations.
- Use explicit `--spool` when an operation requires seekable storage for a pipe; use temporary disk storage rather than unbounded memory buffering.
- Current public position-preserving detector/inspector stream paths require seekable input. Do not advertise direct non-seekable support without implementing it deliberately.
- Preserve streaming operation for Reader-supported formats. Keep 7z read and split ZIP seek requirements explicit.
- Cancellation is cooperative; hard timeouts for hostile corpus inputs require process isolation.
- Finalize writers before reporting success. Clean up tool-owned temporary artifacts without deleting user-owned files.

## Delivery Phases

### Phase 1: Verification Foundation and Useful CLI

1. Audit verification coverage and define decoding/checksum/authentication result semantics.
2. Add focused public verification support and corruption tests for applicable formats and API paths.
3. Add tool packaging, parsing, renderers, structured errors, and cancellation handling.
4. Implement `formats`, `detect`, `inspect`, `list`, `extract`, `cat`, and `test`.
5. Add process-level CLI tests and initial JSON contract tests using existing archive fixtures.

### Phase 2: Creation and Regression Workflows

1. Add single-pass detailed inspection and explicit availability metadata where needed.
2. Implement creation and supported compression-stream commands.
3. Add content manifests and normalized comparisons.
4. Implement matrix, corpus, and roundtrip commands.
5. Expand stream, cancellation, ownership, multipart, encryption, and option-compatibility coverage.

### Phase 3: Structural Inspection

1. Define the opt-in diagnostic snapshot/event contract.
2. Add parser diagnostics incrementally, format by format.
3. Implement `inspect --structure` renderers and stable structured output.
4. Add structural fixtures and verify no meaningful disabled-diagnostics performance regression.

## Validation and Documentation

- Follow repository `AGENTS.md` guidance and existing code style.
- Prefer available Rider project-aware tooling; use CLI fallbacks when unavailable or insufficient.
- Reuse `tests/TestArchives` fixtures; do not assume corrupt fixtures should succeed.
- Run targeted tests first, then broader relevant tests on `net10.0` on this platform.
- Cover Archive and Reader paths where supported, sync/async parity, checksum failures, authentication failures, unknown sizes, solid archives, missing volumes, and stream disposal.
- Test process exit codes, clean stdout, binary `cat` output, password redaction, duplicate names, selection, overwrite behavior, and cancellation.
- Verify .NET tool packaging and invocation, not only direct project execution.
- Run `dotnet csharpier format .` after code edits and `dotnet csharpier check .` before handoff, reviewing for unrelated changes.
- Update `docs/API.md` for public library additions, `docs/USAGE.md` for usage, `docs/FORMATS.md` for capability changes, and `README.md` for the tool summary/install guidance.
- Preserve existing public signatures and behavior; avoid new public metadata that unnecessarily forces consumers onto newer C# language versions.
- Do not stage or commit changes without explicit instruction.

## Scope Boundaries

The library owns format facts, parsing, compression, extraction mechanics, verification, and reusable inspection. The CLI owns commands, terminal presentation, password sourcing, user-facing policy, corpus orchestration, and process isolation.

Do not turn this work into an unrelated library rewrite, add an external archiver dependency to normal operations, or promise unsupported repair, writing, or metadata semantics. The initial tool should be useful before exhaustive structural diagnostics are complete.
