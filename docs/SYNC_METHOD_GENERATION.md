# Generating sync methods from async ones — migration plan

SharpCompress hand-maintains a sync twin beside most async methods, usually split as `Foo.cs` +
`Foo.Async.cs`. That means every bug fix has to be made twice, and they drift: the extraction
overloads converted in commit `c303856c` had already diverged (the async path honoured
`ExtractionOptions.BufferSize`, the sync path did not).

[Zomp.SyncMethodGenerator](https://github.com/zompinc/sync-method-generator) removes the duplication
by generating the sync method from the async one. It is already referenced for every project
(`Directory.Packages.props`, `GlobalPackageReference`), so it runs in all six TFMs with no per-project
setup.

A normalised scan (strip `await`, `.ConfigureAwait(false)`, the `Async` suffix, `CancellationToken`;
map `Task<T>`→`T`) over ~130 `.cs`/`.Async.cs` pairs found **626 `XAsync`/`X` method pairs, 298 of
them byte-identical** and another 147 at ≥85% similarity — roughly **2,100 deletable lines**.

This document is the execution plan for the remaining work. The conventions themselves are
summarised in `AGENTS.md` ("Generating sync methods from async ones"); this file is the batch list,
the verification workflow, and the record of what is blocked and why.

## Status

| Batch | Area | Status |
| --- | --- | --- |
| 0 | `Archives/IArchiveEntryExtensions` (5 methods) | done — `c303856c` |
| 1 | `Compressors/Filters/Filter` + 6 XZ branch filters + `Lzma2Filter` (9 methods, 147 lines) | done — verified token-identical on net48 + net10.0 |
| 2–12 | below | to do |
| Rar reader unification | below | to do, needs its own design review |

---

## The recipe

Proven on batch 1. Per file pair:

1. **Map the signature by hand** for each async method: drop the `Async` suffix; `Task`/`ValueTask`→
   `void`, `Task<T>`/`ValueTask<T>`→`T`; drop `CancellationToken` and `IProgress<T>` (unless
   `PreserveCancellationToken` / `PreserveProgress`); `Memory<T>`→`Span<T>`,
   `ReadOnlyMemory<T>`→`ReadOnlySpan<T>`; modifiers (`public`, `override`, `virtual`, `static`,
   `sealed`) copied verbatim.
2. **Attribute only when that mapped signature already exists by hand.** Generating a member that
   did not exist before is a behaviour change, not a deduplication — a generated `Read(Span<byte>)`
   replaces `Stream`'s default rent-and-copy shim. Keep those out of a deletion batch; if they are
   wanted, add them in a separate, clearly-labelled commit.
3. Add `[Zomp.SyncMethodGenerator.CreateSyncVersion]` **per method, never on the type.** A
   type-level attribute also generates the members that must not exist, so you would need more
   `[SkipSyncVersion]` than `[CreateSyncVersion]`.
4. **Delete exactly those sync twins.** Missing one is `CS0111` on every TFM — lean on that.
5. Remove usings that the deleted sync body was the only consumer of (batch 1: `using
   SharpCompress.Compressors.Filters;` in each branch filter's `.cs`).
6. Keep the class `partial`, keep the async method where it lives, and **do not rename
   `Foo.Async.cs`** — renames destroy `git blame`/`--follow` on exactly the algorithm code that most
   needs history, and the name becomes *more* accurate after conversion (what remains in it is
   genuinely async-only). Delete `Foo.Async.cs` only if it ends up empty.
7. **XML docs**: keep one comment, on the async method, phrased tense-neutrally ("Extract entry to
   the specified stream.") — it is emitted onto both copies. If the two comments disagree on
   substance, that is a converge item, not something to resolve silently while deleting.
8. `dotnet csharpier format .` — `check-format` is the *first* step of the default build target.

One commit per concern, at most two per batch: commit 1 "converge" (behaviour alignment, itemised),
commit 2 "generate" (attribute + delete, provably no-op). Never mix them — mixing destroys the
"expected diff is empty" invariant that makes these PRs reviewable.

Keep each batch to one cohesive area, ≤ ~12 attributed methods or ≤ ~400 deleted lines.

---

## Verification

### 1. Prove the generated code equals the deleted code

The generator only sees the *active* syntax for the current compilation — code inside
`#if !LEGACY_DOTNET` is disabled trivia on net48/ns2.0/ns2.1 — so check both `#if` worlds:

```powershell
foreach ($tfm in 'net48','net10.0') {
  dotnet build src/SharpCompress/SharpCompress.csproj -c Release -f $tfm -p:EmitCompilerGeneratedFiles=true
}
```

Generated source lands in
`src/SharpCompress/obj/Release/<tfm>/generated/Zomp.SyncMethodGenerator/Zomp.SyncMethodGenerator.SyncMethodSourceGenerator/<Namespace>.<Type>.<Method>.g.cs`
— already TFM-scoped, already gitignored, and invisible to csharpier.

For each attributed method, compare the generated body against the body deleted from `HEAD`
(`git show HEAD:<path>`), normalising whitespace **and** the generator's full qualification: it emits
`global::System.Buffer.BlockCopy` where the hand-written code said `Buffer.BlockCopy`, so collapse
`global::A.B.C.Type.Member` to `Type.Member` before comparing (a greedy
`global::(?:[A-Za-z0-9_]+\.)*([A-Za-z0-9_]+\.[A-Za-z0-9_]+)` → `$1` does it). Batch 1 was verified
this way with a throwaway Python script — 18/18 bodies token-identical. **Expected result is an empty
diff**; itemise any non-empty hunk in the commit message.

If a dump has to survive `clean`, override `CompilerGeneratedFilesOutputPath` — but build one TFM at
a time (it is a global property with no `$(TargetFramework)` expansion, so a multi-TFM build races all
six inner builds into one directory) and point it **outside the repo tree** so csharpier never sees it.

### 2. The repo gate

```powershell
dotnet run --project build/build.csproj
```

check-format → `restore --locked-mode` → build all six TFMs → test on net10.0 and net48 → pack.
Note ns2.0/ns2.1/net6.0/net8.0 are compile-verified only, which is why step 1 matters.

### 3. AOT

```powershell
dotnet publish tests/SharpCompress.AotSmoke/SharpCompress.AotSmoke.csproj -c Release --runtime linux-x64 --self-contained true --output artifacts/aot-smoke
```

The trim/AOT analyzers (net8.0/net10.0 set `IsTrimmable`/`IsAotCompatible`) run during the managed
compile, so an ordinary build already covers them; the native link step needs a C++ toolchain and is
covered by CI. A `--runtime <rid>` publish rewrites `packages.lock.json` with RID-specific sections —
revert that churn before committing.

### 4. Benchmarks

`performance-benchmarks.yml` flags >25% moves against `tests/SharpCompress.Performance/baseline-results.md`.
Batches 4, 9, 10 and 11 replace hand-optimised sync paths with code derived from allocation-tolerant
async bodies. A regression there is the signal to add a `SYNC_ONLY` site or leave that method
hand-written — not noise to override.

---

## Remaining batches

Ordered by (duplication removed) / risk.

### 2 — `IO/` leaf stream shims · ~150 lines · low risk

`ReadOnlySubStream`, `BufferedSubStream`, `SourceStream`, `SeekableSharpCompressStream`,
`ProgressReportingStream`, `Common/EntryStream`.

- `ReadOnlySubStream` is the one clean case where **both** overloads are attributed — a hand-written
  `Read(Span<byte>)` exists (`ReadOnlySubStream.cs:78`) and matches the generated form.
- `[SkipSyncVersion]` on every `Stream.DisposeAsync`.
- `IO/CountingStream` needs `partial` added; then its `FlushAsync`/`ReadAsync`/`WriteAsync` map onto
  existing hand-written members.
- Leave `SharpCompressStream` for later (stateful, 274-line async partial; also needs the
  `ReadAsyncCore` rename and the `ReadAsync(Memory<byte>)` rewrite, below).

### 3 — XZ reader family · ~120 lines · low-med risk

`XZIndexRecord`, `XZIndex`, `XZFooter`, `XZHeader`, `MultiByteIntegers`, `XZBlock`.

Needs a **converge commit first**: `XZFooter.ProcessAsync` reads the CRC via
`_reader.BaseStream.ReadLittleEndianUInt32Async(...)` while the sync twin (`XZFooter.cs:34`) uses
`_reader.ReadLittleEndianUInt32()`. Those are different implementations (`BinaryUtils.cs:19` goes
through `ReadBytes(4)`, `:33` through `ReadFully`) and **throw different exception types on truncated
input** (`ArgumentOutOfRangeException` vs `IncompleteArchiveException`). Same split at
`XZIndex.Async.cs:78` vs `XZIndex.cs:84`, and `XZHeader.Async.cs:33` vs `XZHeader.cs:37`. Also
`MultiByteIntegers` names the parameter `MaxBytes` in sync and `maxBytes` in async — generation
renames it (harmless, internal, but say so).

`Xz/BinaryUtils` stays hand-written: the sync version uses `stackalloc byte[4]` + `ReadFully(Span)`,
the async one `new byte[4]`, and the method is 4 lines — `SYNC_ONLY` would be most of it.

### 4 — LZMA internals · ~140 lines · med risk

`LZ/LzOutWindow` (9 pairs, all ≥0.96), `RangeCoder/RangeCoderBitTree` (6 pairs, all identical),
`LzmaDecoder` (8 pairs incl. `CodeAsync` 181 lines), `RangeCoder/RangeCoder`.

- `DisposeAsync`→`Dispose()` is *legal* here — `OutWindow` and `Decoder` are `IDisposable`, not
  `Stream`.
- `RangeCoderBit` needs a rewrite first: `BitEncoder.EncodeAsync` is non-`async`
  (`return encoder.ShiftLowAsync(ct);` / `return default;`) and `BitDecoder.DecodeAsync` calls
  `DecodeAsyncHelper` (no `Async` *suffix*, so no rewrite). Make both real `async`/`await` and drop
  the helper. These were deliberately non-async — measure the LZMA hot path before and after.
- Perf caveats to check in the generated output: it uses 1-byte `Read(buf,0,1)`/`Write(buf,0,1)`
  where the hand-written sync code used `ReadByte`/`WriteByte`, and
  `[MethodImpl(AggressiveInlining)]` on the hot `Normalize2` is not carried over. `SYNC_ONLY` is the
  fix if the benchmarks move.

### 5 — ACE + ARJ parsing · ~200 lines · low risk

`Ace/Headers/AceFileHeader` (`ReadAsync` 94 identical lines), `AceMainHeader` (63), `AceHeader`,
`Arj/HuffmanTree`, `Arj/BitReader`, `Arj/LhaStream` (11 pairs, all ≥0.99), `Arj/LHDecoderStream`,
`Squeezed/BitReader`. Best win per line of review — pure parsing, all clean.

### 6 — Zip parts & header factories · ~250 lines · med risk

6a: `ZipFilePart` (incl. `GetCryptoStreamAsync`), `SeekableZipFilePart`, `StreamingZipFilePart`,
`GZipFilePart`, `Zip/Headers/ZipFileEntry`, `PkwareTraditionalCryptoStream`, `WinzipAesCryptoStream`.
6b: `ZipHeaderFactory` (`LoadHeaderAsync` identical), `SeekableZipHeaderFactory`.

### 7 — Concrete writers · ~250 lines · med risk

`ZipWriter`, `ZipWritingStream` (`GetWriteStreamAsync` is a 131-line duplicate), `TarWriter`,
`SevenZipWriter`, `GZipWriter`.

This is the **reachable Writers win**: `AbstractWriter` already implements *both* `IWriter` and
`IAsyncWriter` (`AbstractWriter.cs:11`), so `ZipWriter.Async.cs:52
WriteAsync(string,Stream,DateTime?,ct)` maps exactly onto `ZipWriter.cs:80
Write(string,Stream,DateTime?)`. Do not attribute `AbstractWriter`'s abstract declarations, and do not
attempt `IWriterExtensions` (see Blocked).

### 8 — 7-Zip · ~200 lines · med-high risk

`Common/SevenZip/ArchiveReader` — `ReadHeaderAsync` (230 identical lines), `ReadDatabaseAsync` (106),
`ReadAndDecodePackedStreamsAsync` (72) — and `ArchiveDatabase.GetFolderStreamAsync`,
`SevenZipFilePart`, `SevenZipSignatureHeader`.

Clean: these use the sync `DataReader`, not an async-only reader type. Keep the file split
(`ArchiveReader.cs` is 1,377 lines).

### 9 — Streaming compressors · ~300 lines · med-high risk

`Deflate64Stream`, `Deflate/ZlibBaseStream` (`ReadAsync` 192 lines, `WriteAsync`, `FlushAsync`),
`DeflateStream`, `GZipStream`, `ZlibStream`, `ZStandard/*`, `Reduce`, `Explode`, `Lzw/LzwStream`
(`ReadAsync` 208 lines), `RLE90`, `Shrink`, `ArcLzw`.

- `ZlibBaseStream` needs `partial` added.
- `[SkipSyncVersion]` on all four Deflate-family `DisposeAsync`.
- `RunLength90Stream` is the canonical `SYNC_ONLY` case (see below).
- `GZipCompressionProvider.cs:41` returns `new ValueTask<Stream>(...)` from a non-`async` method —
  not a Zomp rewrite; make it `async` first. (Non-`async` methods that just *return* an `XAsync(...)`
  call are fine — `Lzma2Filter` proved that in batch 1.)

### 10 — BZip2 · ~500 lines · high risk · two separate PRs

`CBZip2InputStream` (18 pairs, incl. `RecvDecodingTablesAsync` 142 identical lines and
`GetAndMoveToFrontDecodeAsync` 315) then, separately, `CBZip2OutputStream` (`SendMTFValuesAsync` 388
lines, `EndBlockAsync`, `WriteRunAsync`, …).

Largest mechanical win in the repo. `[SkipSyncVersion]` on `ReadByteAsync`/`WriteByteAsync` — the
generated `ReadByte()`/`WriteByte()` are emitted without `override` and would hide `Stream`'s
(`CS0108`) — and on `DisposeAsync`. Watch the benchmark job.

### 11 — Rar `UnpackV1` · ~400 lines · high risk

`Unpack20` (`unpack20Async` 152 identical lines), `Unpack15`, `Unpack` (`Unpack29Async` 304,
`UnpWriteBufAsync` 203), `Unpack50` (`Unpack5Async` 180), `UnpackV2017/Unpack`.

Fully clean — these use raw `Stream`, **not** the async reader types. The least readable diffs in the
repo, so do them last of the mechanical work.

### 12 — Providers · ~80 lines · low risk

`CompressionProviderRegistry` (4 identical pairs), `Default/*` providers (`XStream.CreateAsync(...)`
→ `Create(...)` maps cleanly), `CompressionProviderBase`,
`ContextRequiredDecompressionProviderBase`. Blocked on the `GZipCompressionProvider` rewrite above.

---

## Cross-batch fixes that unlock files

Do these in the relevant batch's converge commit:

- **Add `partial`**: `IO/CountingStream`, `Compressors/Deflate/ZlibBaseStream`.
- **Rename so `Async` is actually a suffix** (Zomp only strips a *trailing* `Async`, so these
  currently generate a sync method that still says "Async" and never finds its twin):
  `IEntryExtensions.WriteEntryToDirectoryAsyncCore` → `WriteEntryToDirectoryCoreAsync`;
  `SharpCompressStream.ReadAsyncCore` → `ReadCoreAsync`.
- **Rewrite as real `async`/`await`** (Zomp rewrites `await Task.FromResult(x)`, but not
  `new ValueTask<T>(x)`): `RangeCoderBit.BitEncoder.EncodeAsync`, `BitDecoder.DecodeAsync`,
  `GZipCompressionProvider.CreateDecompressStreamAsync`,
  `SharpCompressStream.ReadAsync(Memory<byte>)`.
- **`Utility.Skip` vs `Polyfills/StreamExtensions.Skip`**: the generated
  `Utility.Skip(this Stream,long)` would be `CS0121`-ambiguous with the polyfill. Merge or delete one
  first. Do `Utility` **last** — highest fan-in file in the library, only 2 near-matches to win. Its
  `ReadFullyAsync(byte[])` would also overwrite the `#if NET8_0_OR_GREATER` `ReadExactly` fast path,
  so that needs `SYNC_ONLY`.
- **`Common/IEntryExtensions`** relies on `Func<..,ValueTask>`→`Action<..>` conversion (undocumented
  but implemented — `IArchiveEntryExtensions` already depends on it) plus the `…CoreAsync` rename.

## `SYNC_ONLY` — when, and the budget

Use it when the difference is a **localised I/O idiom with identical semantics** and ≥80% of the body
is shared algorithm. Canonical case `Compressors/RLE90/RunLength90Stream`: sync uses
`_stream.ReadByte()` with a `-1` sentinel (`.cs:86-91`), async allocates `byte[1]` per byte
(`.Async.cs:47-55`), and the other ~55 lines are identical.

Constraints: works in statements, parameter lists and argument lists; cannot nest with `!SYNC_ONLY`
(`ZSMGEN001`); cannot combine with other symbols in one condition (`ZSMGEN002`); no `#elif`
(`ZSMGEN003`); contents are copied **verbatim**, so fully qualify names. `ZSMGEN004` tells you to use
it when the async method awaits several operations at once.

**Budget: at most two `SYNC_ONLY` sites per method, and never more than ~15% of its lines.** Past
that, two honest files beat one half-preprocessor file.

## Leave hand-written

Record the decision once, as a comment at the method, so it is not re-litigated.

- `Dispose`/`DisposeAsync`, `Flush`/`FlushAsync`, `CopyTo`/`CopyToAsync` — framework semantics
  genuinely differ, and on a `Stream` the generated `Dispose()` cannot override the non-virtual
  `Stream.Dispose()` (`CS0506`); the real override is `Dispose(bool)`.
- `ReadByteAsync`/`WriteByteAsync` on a `Stream` — generated without `override`, hides
  `Stream.ReadByte`/`WriteByte` (`CS0108`).
- Pairs whose difference is an optimisation spread over >2 sites or >20% of the body — e.g.
  `Xz/BinaryUtils`.
- `Memory`/`ReadOnlyMemory` overloads with no sync twin.

## Hard-blocked

| Area | Blocker |
| --- | --- |
| `Writers/IWriterExtensions.cs` | Sync is `extension(IWriter)`, async is `extension(IAsyncWriter)`. `IWriter : IDisposable` and `IAsyncWriter : IAsyncDisposable` are public interfaces with different base contracts; merging them breaks every external implementor for ~50 lines of thin sugar. **Leave hand-written permanently** — take batch 7 instead. |
| `Readers/IAsyncReaderExtensions.cs`, `Archives/IAsyncArchiveExtensions.cs` | Same interface split, and the sync twins live in *different* classes (`IReaderExtensions`, `IArchiveExtensions`). Plus method-vs-property mismatches: `IsSolidAsync()` vs `IArchive.IsSolid`, `TotalUncompressedSizeAsync()` vs the property, `EntriesAsync` vs `Entries`. |
| `Factories/*Factory.cs` | `OpenAsyncArchive`/`OpenAsyncReader`/`OpenAsyncWriter` don't *end* in `Async`, so no rename happens; `Factory.TryOpenReaderAsync` returns `ValueTask<IAsyncReader?>` and cannot override `internal virtual bool TryOpenReader(...)`; bodies use target-typed `return new(...)` on `ValueTask`. |
| `Archives/AbstractArchive*`, `ArchiveFactory` | Use `AsyncEnumerableEx.Empty<T>()` / `volumes.ToListAsync()` (`Polyfills/AsyncEnumerableExtensions.cs`) — no sync equivalent. |
| Rar header family | Async-only mirror *types* — see below. |

Two worries that turned out not to be real: the `#if LEGACY_DOTNET` `byte[]`-vs-`Memory<byte>`
overload groups **cannot** collide (`Memory<T>`→`Span<T>` yields a distinct signature), and where a
`#if`/`#else` pair declares the same shape twice you put the attribute inside each branch since only
one is ever active. Collisions that *are* possible (two async overloads differing only by
`CancellationToken`/`IProgress`) are caught by `ZSMGEN005`, not by review.

## Warnings to expect

`TreatWarningsAsErrors` is on. Analyzers skip `.g.cs`, but **compiler CS warnings do not**: `CS0162`
unreachable (a `return`/`break` that vanishes from the sync copy), `CS0219`/`CS0168` unused local (a
result only checked on the async side), `CS8602`/`CS8604` nullable (the generator emits
`#nullable enable` unconditionally — `OmitNullableDirective = true` is the escape hatch),
`CS0108`/`CS0114` hiding. Fix in the async source, or `#pragma warning disable`/`restore` there — it
is copied into the generated file. `CS0111`/`CS0534` are the *desired* safety net for "did I delete
the right thing".

---

## Rar reader unification (own PR, own review)

The Rar header family scores **zero** exact matches despite ~1,150 duplicated lines, purely because
the sync methods take `RarCrcBinaryReader`/`MarkingBinaryReader` and the async ones take
`AsyncRarCrcBinaryReader`/`AsyncMarkingBinaryReader`. Zomp cannot change parameter types. Unifying
the reader unlocks `FileHeader.Async.cs` (~340 lines: `ReadFromReaderV5Async` 169,
`ReadFromReaderV4Async` 171), `RarHeaderFactory.Async.cs` (~200), `MarkHeader` (~130), `RarHeader`
(~115), `ArchiveHeader`, `EndArchiveHeader`, `ProtectHeader`, `ArchiveCryptHeader`, `Rar5CryptoInfo`,
`RarVolume`.

**Do not** try to generate the sync side of today's `MarkingBinaryReader`: it derives from
`BinaryReader` and gets its whole API by `override`ing `ReadByte`/`ReadBytes`/`ReadUInt16`/… .
Modifiers are copied from the async method, which cannot be `override` because `BinaryReader` has no
async virtuals — so you get `CS0108`/`CS0114`.

**The async side is already the right shape.** `Common/Rar/AsyncMarkingBinaryReader` does *not* derive
from `BinaryReader`; it wraps `IO/AsyncBinaryReader` over a raw `Stream`. So:

1. Make `IO/AsyncBinaryReader` `partial` and attribute its async primitives (`ReadByteAsync`,
   `ReadUInt16/32/64Async`, `ReadBytesAsync`, `SkipAsync`) to generate the sync twins. It is
   `public sealed` — keep the name (renaming breaks public API) and keep it sealed; generation is
   unaffected. `Dispose`/`DisposeAsync` are already hand-written; leave them.
2. Make `AsyncMarkingBinaryReader`, `AsyncRarCrcBinaryReader`, `AsyncRarCryptoBinaryReader` `partial`
   and attribute their async methods. The `virtual`/`override` chain survives verbatim: base
   `public virtual async ValueTask<byte> ReadByteAsync` → `public virtual byte ReadByte()`, and the
   CRC subclass's `override` → `public override byte ReadByte()`.
3. Rename `AsyncRarCryptoBinaryReader.Create` → `CreateAsync`: it is a static async factory that does
   not end in `Async`, so the generated sync `Create` would differ from it only by return type
   (`CS0111`).
4. Delete `IO/MarkingBinaryReader.cs`, `Common/Rar/RarCrcBinaryReader.cs`,
   `Common/Rar/RarCryptoBinaryReader.cs`. Drop the `Async` prefix from the three remaining
   `Common/Rar/Async*BinaryReader.cs` types (all `internal`, so free) and update the ~15 Rar header
   call sites.
5. **Then** attribute the header methods and delete their sync twins, batch by batch: `RarHeader` +
   the small headers first, `FileHeader` alone, `RarHeaderFactory` alone.

**This is a behaviour change, not a deletion.** Name the deltas in the PR:

- Truncation exception type differs today: sync `MarkingBinaryReader.ReadByte` → `BinaryReader` →
  `EndOfStreamException`; the unified path → `Stream.ReadExact` → `IncompleteArchiveException`.
- `ReadBytes` truncation message differs — `"Requested: {0} Read: {1}"` (`MarkingBinaryReader.cs:47`)
  vs `"Requested: {0}"` (`AsyncMarkingBinaryReader.cs:54`). Pick one.
- `MarkingBinaryReader`'s `NotSupportedException` guards (`Read()`, `ReadChar`, `ReadDouble`,
  `ReadSingle`, `ReadString`, …) disappear with the `BinaryReader` base. Confirm nothing depends on
  them.
- `RemainingHeaderBytes(MarkingBinaryReader)` (`RarHeader.cs:109`) and
  `RemainingHeaderBytesAsync(AsyncMarkingBinaryReader)` (`:112`) — the latter isn't async at all;
  after unification they are the same signature, so delete one.

**Prerequisite**: pin today's byte-counting/CRC behaviour with tests first —
`Mark()`/`CurrentReadByteCount` through both `RarCrcBinaryReader` and `RarCryptoBinaryReader`, and
RAR5 encrypted-header reads. The comment at `IO/MarkingBinaryReader.cs:26-34` warns that the CRC and
crypto subclasses depend on which methods call the base directly; that dependency is what the tests
must lock down.

**Ranking**: below batches 10–11. BZip2 (~500 lines) and Rar `UnpackV1` (~400) deliver comparable
savings as provable no-ops, whereas this one touches CRC and RAR5 crypto byte counting.
