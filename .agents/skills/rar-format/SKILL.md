---
name: rar-format
description: Reference the RAR/RAR5 archive container format and UnRAR source behavior. Use when an AI agent needs to answer questions or make code changes involving RAR signatures, RAR5 vint fields, block headers, file/service headers, extra records, solid archives, multivolume parts, encryption headers, recovery records, quick-open data, or SharpCompress Rar parsing and extraction behavior.
---

# Rar Format

Use this skill for RAR container-format work. It provides a local, SharpCompress-oriented reference for RAR 5.0 archive blocks, older RAR header compatibility, UnRAR source behavior, and current SharpCompress Rar implementation boundaries.

## Reference

- Read [references/rar-format.md](references/rar-format.md) when the task depends on RAR binary layout, RAR4 vs RAR5 signatures, RAR5 vint encoding, block/header flags, file and service header fields, extra records, encrypted headers, solid archives, split volumes, redirection records, or current SharpCompress Rar support boundaries.
- Treat the reference as an implementation guide, not a standards replacement. It summarizes `reference/RAR 5.0 archive format.htm` and selected files under `reference/unrar/`, especially `headers5.hpp`, `headers.hpp`, `arcread.cpp`, `rawread.*`, `crypt*.cpp`, `qopen.*`, `volume.*`, `recvol*.cpp`, `unpack*.cpp`, and `blake2sp.*`.
- Prefer the SharpCompress support matrix in the reference over generic RAR assumptions when changing code. RAR is a read-only format in SharpCompress, and metadata/service-header support is intentionally partial.

## Workflow

1. Identify which layer is involved: signature detection, RAR4 headers, RAR5 headers, vint parsing, block flags, extra records, file/service metadata, encrypted headers, multivolume handling, decompression, reader/archive API behavior, or tests.
2. Open the relevant section in `references/rar-format.md` and use the source-file pointers before changing code.
3. For header parsing changes, cross-check sync and async implementations: `MarkHeader.cs`, `RarHeader.cs`, `RarHeaderFactory.cs`, `FileHeader.cs`, `Flags.cs`, and their async counterparts.
4. For extraction changes, verify split and solid archive behavior with `RarArchive`, `RarReader`, `RarStream`, and the unpacker files under `src/SharpCompress/Compressors/Rar/`.
5. For support claims, keep unsupported or partial features explicit: RAR writing, pre-RAR4 archives, complete service-header semantics, quick-open use, recovery reconstruction, and encryption/version constraints.
