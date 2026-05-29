---
name: sevenzip-format
description: Reference the 7z/7zip archive container format. Use when an AI agent needs to answer questions or make code changes involving 7z signatures, headers, encoded headers, NID/property IDs, packed streams, folders/coders, bind pairs, substreams, file metadata properties, or SharpCompress 7Zip parsing behavior.
---

# Sevenzip Format

Use this skill for 7z container-format work. It provides a local Markdown conversion of the LZMA SDK `7zFormat.txt` reference.

## Reference

- Read [references/7z-format.md](references/7z-format.md) when the task depends on 7z binary layout, header property IDs, stream/folder relationships, metadata fields, or encoded headers.
- Treat the reference as the LZMA SDK 7z format description version 4.59. It describes the container grammar, not compression method internals; method-specific codec details are outside this skill.
- Preserve source field names and numeric IDs when mapping the spec to code. The converted reference keeps source spelling inside syntax blocks where exact matching may matter.

## Workflow

1. Identify which part of the 7z container is involved: signature/start header, packed streams, coders/folders, substreams, files info, or encoded headers.
2. Open the relevant section in `references/7z-format.md` and use the table of contents to avoid loading unrelated details.
3. When implementing or reviewing parsing logic, pay special attention to optional blocks marked with `[]`, 7z's variable-length `UINT64` encoding, and little-endian `REAL_UINT64` fields.
4. Cross-check behavior against SharpCompress tests and existing parser conventions before changing public API or stream behavior.
