---
name: tar-format
description: Reference the Tar/USTAR/PAX/GNU tar archive container format. Use when an AI agent needs to answer questions or make code changes involving tar headers, 512-byte blocks, checksums, typeflags, USTAR prefixes, PAX local/global extended headers, GNU long names/links, sparse entries, wrapper compression, or SharpCompress Tar parsing and writing behavior.
---

# Tar Format

Use this skill for tar container-format work. It provides a local, SharpCompress-oriented reference for POSIX USTAR, POSIX PAX, GNU tar extensions, and the current SharpCompress Tar implementation.

## Reference

- Read [references/tar-format.md](references/tar-format.md) when the task depends on tar binary layout, header field offsets, typeflag behavior, checksum rules, PAX records, GNU long-name/link records, wrapper compression support, or current SharpCompress Tar support boundaries.
- Treat the reference as an implementation guide, not a standards replacement. It cites POSIX and GNU tar sources, but it also documents SharpCompress-specific behavior and limitations.
- Prefer the SharpCompress support matrix in the reference over generic tar assumptions when changing code. Tar dialect support is intentionally partial in some areas.

## Workflow

1. Identify which layer is involved: raw tar block/header parsing, POSIX USTAR fields, POSIX PAX metadata, GNU tar extensions, wrapper compression, reader/archive/writer API behavior, or tests.
2. Open the relevant section in `references/tar-format.md` and use the source-file pointers before changing code.
3. For parsing changes, cross-check `TarHeader.cs`, `TarHeader.Async.cs`, `EntryType.cs`, and matching sync/async tests.
4. For writer changes, verify both sync and async file/directory paths and `TarWriterOptions.HeaderFormat` behavior.
5. For support claims, keep unsupported features explicit: PAX write, sparse reconstruction, device/FIFO semantics, link-writing APIs, and writing `tar.xz`, `tar.zst`, or `tar.Z`.
