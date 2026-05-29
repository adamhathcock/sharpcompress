---
name: zip-format
description: Reference the ZIP/ZIP64/PKWARE APPNOTE archive container format. Use when an AI agent needs to answer questions or make code changes involving ZIP local headers, central directory records, EOCD/Zip64 records, data descriptors, general purpose bit flags, compression method IDs, extra fields, encryption markers, split archives, or SharpCompress Zip parsing and writing behavior.
---

# Zip Format

Use this skill for ZIP container-format work. It provides a local, SharpCompress-oriented reference for PKWARE APPNOTE ZIP records, ZIP64, compression method IDs, extra fields, and the current SharpCompress Zip implementation.

## Reference

- Read [references/zip-format.md](references/zip-format.md) when the task depends on ZIP binary layout, record signatures, local vs central directory metadata, data descriptor rules, Zip64 sentinel values, extra field parsing, compression method IDs, encryption markers, split archive handling, or current SharpCompress Zip support boundaries.
- Treat the reference as an implementation guide, not a standards replacement. It summarizes PKWARE APPNOTE 6.3.10 from `https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT` and documents SharpCompress-specific behavior and limitations.
- Prefer the SharpCompress support matrix in the reference over generic ZIP assumptions when changing code. ZIP is highly extensible, and SharpCompress intentionally supports only selected records, methods, and extra fields.

## Workflow

1. Identify which layer is involved: header discovery, local file headers, central directory headers, EOCD/Zip64 records, data descriptors, extra fields, compression methods, encryption, reader/archive/writer API behavior, or tests.
2. Open the relevant section in `references/zip-format.md` and use the source-file pointers before changing code.
3. For parsing changes, cross-check sync and async header readers: `ZipHeaderFactory.cs`, `ZipHeaderFactory.Async.cs`, `SeekableZipHeaderFactory.cs`, `StreamingZipHeaderFactory.cs`, `ZipFileEntry.cs`, `LocalEntryHeader.cs`, and `DirectoryEntryHeader.cs`.
4. For writer changes, verify local header, post-data descriptor, central directory, Zip64, and sync/async paths together.
5. For support claims, keep unsupported features explicit: central directory encryption, strong encryption records, XZ writing, non-standard compression methods, broad extra-field semantics, and non-seekable Zip64 writing.
