# XZ and LZMA/LZMA2 Reference

This reference summarizes the XZ container and LZMA/LZMA2 decoder facts that matter for SharpCompress maintenance. It is a local guide, not a full copy of the specs.

## Upstream References

- XZ file format specification: `https://raw.githubusercontent.com/tukaani-project/xz/master/doc/xz-file-format.txt`
- liblzma LZMA2 decoder: `https://raw.githubusercontent.com/tukaani-project/xz/master/src/liblzma/lzma/lzma2_decoder.c`
- liblzma LZMA decoder: `https://raw.githubusercontent.com/tukaani-project/xz/master/src/liblzma/lzma/lzma_decoder.c`
- XZ Utils test file descriptions: `https://raw.githubusercontent.com/tukaani-project/xz/master/tests/files/README`
- XZ range decoder reference, useful for `rc_is_finished` and normalization behavior: `https://raw.githubusercontent.com/tukaani-project/xz/master/src/liblzma/rangecoder/range_decoder.h`

## SharpCompress Pointers

- XZ container stream: `src/SharpCompress/Compressors/Xz/XZStream.cs`, `src/SharpCompress/Compressors/Xz/XZStream.Async.cs`
- XZ block parsing/checks: `src/SharpCompress/Compressors/Xz/XZBlock.cs`, `src/SharpCompress/Compressors/Xz/XZBlock.Async.cs`
- XZ header/footer/index: `XZHeader.cs`, `XZFooter.cs`, `XZIndex.cs`, `XZIndexRecord.cs`, and async counterparts.
- XZ LZMA2 filter wrapper: `src/SharpCompress/Compressors/Xz/Filters/Lzma2Filter.cs`, `Lzma2Filter.Async.cs`
- LZMA/LZMA2 stream and decoder: `src/SharpCompress/Compressors/LZMA/LzmaStream.cs`, `LzmaStream.Async.cs`, `LzmaDecoder.cs`, `LzmaDecoder.Async.cs`, `RangeCoder/RangeCoder.cs`, `RangeCoder/RangeCoder.Async.cs`
- Core tests: `tests/SharpCompress.Test/Xz/*`, `tests/SharpCompress.Test/Streams/LzmaStreamTests.cs`, `tests/SharpCompress.Test/Streams/LzmaStreamAsyncTests.cs`
- Corruption fixture discussed here: `tests/TestArchives/Archives/bad-1-lzma2-7.xz`

## XZ Container Structure

An XZ file is one or more XZ streams. A typical single stream is:

```text
Stream Header -> Block(s) -> Index -> Stream Footer
```

Important layout rules:

- XZ files and XZ streams are aligned to four-byte boundaries.
- Stream header magic is `FD 37 7A 58 5A 00`.
- Stream footer magic is `59 5A` (`YZ`).
- Stream header/footer flags include the check type used for every block in the stream.
- A block consists of `Block Header`, `Compressed Data`, `Block Padding`, and `Check`.
- Block padding is 0-3 null bytes and makes the block size a multiple of four.
- The Index contains one record per block: `Unpadded Size` and `Uncompressed Size`.

## Variable-Length Integers

XZ variable-length integers encode seven data bits per byte. The high bit means continuation. Current XZ limits the encoded integer to nine bytes/63 bits.

SharpCompress implementation:

- `MultiByteIntegers.ReadXZInteger` reads these values.
- It rejects overlong encodings when a continuation byte is `0x00`.
- Use this for block sizes, filter IDs, filter property sizes, index counts, and index record fields.

## XZ Checks Versus Raw LZMA

XZ checks are container-level integrity checks over uncompressed block data. Raw LZMA/LZMA2 decoding does not provide the same container-level CRC validation.

Supported XZ check IDs in SharpCompress:

- `0x00`: none, 0 bytes.
- `0x01`: CRC32, 4 bytes.
- `0x04`: CRC64/XZ, 8 bytes.
- `0x0A`: SHA-256, 32 bytes.

CRC64/XZ details:

- Polynomial: reflected ECMA polynomial `0xC96C5795D7870F42`.
- Initial value: `0xffffffffffffffff`.
- Final XOR: `0xffffffffffffffff`.
- Stored little-endian in the block check field.
- Test vector for `"123456789"`: `0x995DC9BBDF1939FA`.

Common pitfall:

- CRC64/XZ is not the older `Iso3309Polynomial = 0xD800000000000000` path that existed in SharpCompress's generic `Crc64` helper. Using that produces wrong XZ block check values.

## XZ Block Check Handling

When reading an `XZBlock`:

1. Parse and CRC-validate the block header.
2. Build the filter chain in reverse order from the List of Filter Flags.
3. Read uncompressed bytes through the filter chain and update the selected check over the uncompressed bytes.
4. At block end, skip/validate block padding.
5. Read the check field and compare to the computed value.

Important behavior:

- A short `Stream.Read` result does not universally mean EOF. Be careful when using `bytesRead != count` as an end-of-block signal.
- Tests using `StreamReader.ReadToEnd()` often expose end-of-block behavior because they force padding/check validation.
- The check type must match the XZ stream header. For example, a fixture may have one XZ stream using CRC32 and another using CRC64; do not hard-code CRC64 in block tests.

## LZMA2 Chunks

LZMA2 is the only LZMA-family filter defined for XZ (`Filter ID 0x21`). Raw LZMA is not an XZ filter.

LZMA2 control byte categories from liblzma:

- `0x00`: LZMA2 end marker.
- `0x01` or `>= 0xE0`: dictionary reset; the next LZMA chunk must set new properties.
- `>= 0x80`: LZMA chunk. The control byte and following two bytes encode uncompressed chunk size. The next two bytes encode compressed chunk size. Some control values also provide new LZMA properties.
- `0x02`: uncompressed chunk without dictionary reset.
- `0x01`: uncompressed chunk with dictionary reset.
- `0x03..0x7F`: invalid/reserved control values.

For LZMA chunks, the LZMA2 decoder must track:

- Exact uncompressed chunk size.
- Exact compressed chunk size.
- Whether LZMA properties are needed.
- Whether dictionary reset is required.
- Whether the inner LZMA stream saw an LZMA end-of-payload marker.

## LZMA End-Of-Payload Marker In LZMA2

The XZ Utils bad-file corpus describes `bad-1-lzma2-7.xz` as:

```text
bad-1-lzma2-7.xz has EOPM at LZMA level.
```

Meaning:

- The outer XZ container can be parsed.
- The LZMA2 stream can produce all advertised uncompressed bytes.
- The inner raw LZMA decoder still reaches an LZMA end-of-payload marker (`rep0 == uint.MaxValue`).
- LZMA2 must reject this. End-of-payload markers are for raw LZMA cases with unknown size; they are not valid as an LZMA-level terminator inside an LZMA2 chunk.

liblzma behavior:

- `lzma2_decoder.c` calls the inner LZMA decoder with a known `uncompressed_size` and `allow_eopm = false`.
- `lzma_decoder.c` treats EOPM as data error when EOPM is not valid.
- The XZ Utils test README says all `bad-*` files must cause decoder errors.

SharpCompress maintenance guidance:

- If a bad LZMA2 fixture produces all output bytes but `xz --test` reports corrupt data, inspect the inner decoder state, not just output length or XZ block check.
- In SharpCompress, `Decoder.HasEndMarker => _rep0 == uint.MaxValue` is the useful signal for the `bad-1-lzma2-7.xz` case.
- Validate both sync and async paths; `Stream.CopyToAsync` can use byte-array or `Memory<byte>` read overloads depending on target framework and wrapper stream.

## Exception Expectations

`DataErrorException` is internal and derives from `SharpCompressException`. Some public XZ parsing failures throw `InvalidFormatException`; raw decoder corruption may surface as `SharpCompressException` via `DataErrorException` unless the wrapper maps it.

Testing guidance:

- Use exact `InvalidFormatException` when the code path is XZ header/footer/block/index/check validation.
- Use `Assert.ThrowsAnyAsync<SharpCompressException>` or equivalent when the desired behavior is simply that corrupt LZMA/LZMA2 data is rejected.
- Do not weaken tests to accept no exception for XZ Utils `bad-*` fixtures.

## Useful Commands

Use the system `xz` tool as an oracle when available:

```bash
xz --test --verbose tests/TestArchives/Archives/bad-1-lzma2-7.xz
xz --robot --list --verbose tests/TestArchives/Archives/bad-1-lzma2-7.xz
```

Expected for `bad-1-lzma2-7.xz`:

```text
xz: tests/TestArchives/Archives/bad-1-lzma2-7.xz: Compressed data is corrupt
```

Targeted SharpCompress tests:

```bash
dotnet test tests/SharpCompress.Test/SharpCompress.Test.csproj --framework net10.0 --filter "FullyQualifiedName~SharpCompress.Test.Streams.LzmaStream|FullyQualifiedName~SharpCompress.Test.Xz"
```

## Current SharpCompress Gotchas

- XZ index CRC32 verification may still be incomplete; check `XZIndex.VerifyCrc32` before relying on index corruption detection.
- XZ block/index size semantics are easy to confuse. `Unpadded Size` excludes block padding but includes header, compressed data, and check. `Uncompressed Size` is raw output size.
- LZMA2 chunks include their own compressed and uncompressed chunk sizes; these are separate from XZ block header/index sizes.
- `StreamReader.ReadToEnd()` and `TransferToAsync(Stream.Null, long.MaxValue)` are useful for forcing full-stream validation.
