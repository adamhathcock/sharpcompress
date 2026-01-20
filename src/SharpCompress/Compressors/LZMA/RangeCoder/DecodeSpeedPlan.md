# Decode Speed Plan (Options)

## Goals
- Reduce per-bit overhead in `BitDecoder.Decode()`.
- Improve hot-path throughput in LZMA decode loops while preserving behavior.

## Plan Steps
1. Inspect hot call sites and invariants in:
   - [src/SharpCompress/Compressors/LZMA/LzmaDecoder.cs](../../LzmaDecoder.cs)
   - [src/SharpCompress/Compressors/LZMA/RangeCoder/RangeCoderBitTree.cs](RangeCoderBitTree.cs)
   - [src/SharpCompress/Compressors/LZMA/RangeCoder/RangeCoder.cs](RangeCoder.cs)
2. Add internal buffered byte reader in `RangeCoder.Decoder` and replace per-bit `Stream.ReadByte()` usage in `BitDecoder.Decode()` and `Normalize2()`.
3. Tighten normalization in hot loops where a single step is guaranteed (`Normalize2()` vs `Normalize()`), after verifying invariants.
4. Add inlining hints in other hot decoders (`BitTreeDecoder.Decode()`, `ReverseDecode()`, `LenDecoder.Decode()`) to reduce call overhead.

## Options
### Option A (Low Risk, High ROI)
- Buffered read inside `RangeCoder.Decoder` to remove per-bit `Stream.ReadByte()` calls.
- Keep all logic intact; only change how bytes are fetched.

### Option B (Medium Risk, Higher ROI)
- Option A + replace `Normalize()` with `Normalize2()` where invariants allow single-step normalization.
- Requires careful validation for all LZMA variants.

### Option C (Low Risk, Incremental)
- Option A + aggressive inlining across decoder helpers to reduce call overhead.

## Validation
- Run decompression tests across formats and large archives.
- Measure throughput before/after and validate bit-exact output.
