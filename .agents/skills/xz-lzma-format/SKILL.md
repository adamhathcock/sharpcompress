---
name: xz-lzma-format
description: Reference the XZ container format and LZMA/LZMA2 decoder behavior. Use when an AI agent needs to answer questions or make code changes involving XZ headers, blocks, indexes, checks, CRC64/XZ, LZMA2 chunks, LZMA end markers, corrupt .xz test files, or SharpCompress XZ/LZMA parsing and decompression behavior.
---

# XZ and LZMA Format

Use this skill for work at the boundary between the XZ container and the LZMA/LZMA2 compression streams. It captures the key details needed for SharpCompress XZ block parsing, XZ integrity checks, and LZMA2 decoder corruption handling.

## Reference

- Read [references/xz-lzma-format.md](references/xz-lzma-format.md) when the task depends on XZ binary layout, XZ block checks, CRC64/XZ parameters, LZMA2 chunk control bytes, LZMA end-of-payload markers, or XZ Utils bad-file expectations.
- Treat XZ as a container around filter chains. Do not assume raw LZMA/LZMA2 behavior is equivalent to XZ stream validation.
- Use the linked XZ Utils/liblzma sources in the reference when matching corruption behavior. The test corpus contains intentionally bad files that must throw even when they can produce all expected output bytes.

## Workflow

1. Identify the layer involved: XZ stream header/footer, block header, compressed data padding, block check, index, filter chain, LZMA2 chunk framing, or raw LZMA range decoding.
2. Open `references/xz-lzma-format.md` and cross-check the relevant spec/source section before changing parser or decoder code.
3. For XZ checksum work, verify the stream check type from the XZ header. Use CRC32, CRC64/XZ, SHA-256, or no check according to the header, not according to a test fixture assumption.
4. For LZMA2 corruption work, compare SharpCompress behavior against the XZ Utils test corpus notes and liblzma decoder state model.
5. Test both sync and async paths. Relevant files are `XZBlock.cs`, `XZBlock.Async.cs`, `XZStream.cs`, `XZStream.Async.cs`, `LzmaStream.cs`, `LzmaStream.Async.cs`, `LzmaDecoder.cs`, and `LzmaDecoder.Async.cs`.
