# ZIP allocation and throughput profile

## Scope

Profiled `master` at **`7ddb0121`**, using Release net10.0, .NET 10.0.13 and
Apple M4/macOS ARM64. The checkout was rebuilt and the library copied before
measurement. The initial diagnostic results below are the baseline; the
allocation-optimization follow-up records changes and staged measurements
separately.

The payload is identical to the RAR case: four 50 MiB files alternating 64 KiB
seeded-random and structured regions, **200 MiB extracted**. Native `7zz` produced
two ordinary, unencrypted, single-volume ZIPs:

| Compression | Archive bytes | SHA-256 |
| --- | ---: | --- |
| Deflate, level 5, one thread | 109,948,058 | `5e8bbcc810ac6835a39bc9090a56c4ed186b3c0b56bc4c9ae15de01791b829b6` |
| Stored | 209,715,758 | `bebeab9c94dc4ed2fc9c5d6d5c4716b60c94a1e4b0859b5f532cbaabe266a977` |

Python's ZIP reader independently checked all entry CRCs before profiling. The
same archived bytes are used by SharpCompress and `System.IO.Compression`.
This is not a Zip64, encryption, split-archive or ZIP-writing evaluation.

## Method

- Each scenario runs in a fresh process, with two warmups and seven measured
  extractions. Tables show medians for the complete 200 MiB payload.
- SharpCompress Archive and Reader APIs are measured synchronously and
  asynchronously. Async cases use async open/enumeration/entry-stream APIs.
  The .NET 10 reference uses `ZipArchive.CreateAsync` and `ZipArchiveEntry.OpenAsync`.
- All paths use the same explicit CRC32 pass and verify each entry's CRC, length
  and the overall entry count. A separate raw control omits CRC computation but
  still verifies lengths. These are raw entry-stream drains, not high-level
  extraction-to-directory APIs.
- Memory input and the caller's 64 KiB output buffer are allocated outside the
  measurement window. Output is discarded. Each extraction opens a new archive.
- File input uses a 4 KiB FileStream buffer with `SequentialScan` and
  `Asynchronous`, including for sync tests. Filesystem caches are warm; these
  numbers are not cold-disk results.
- `LeaveStreamOpen = true` and `EnableParallelism = false` are explicit. The
  harness verifies the caller's source remains readable after archive disposal.
- Allocation deltas use `GC.GetTotalAllocatedBytes(true)` around extraction and
  disposal, including the 40-byte stopwatch but excluding logging and CPU-counter
  queries. These are managed allocations, not retained memory or process RSS.
- Separate EventPipe traces repeat ten measured extractions after warmup. No
  builds, tests, RAR profiles or native sampling overlap timed ZIP measurements.

## Deflate extraction with CRC validation

| Input / API | Allocated bytes | Elapsed |
| --- | ---: | ---: |
| Memory / SharpCompress Archive sync | 445,104 | 241.34 ms |
| Memory / SharpCompress Reader sync | 445,312 | 244.18 ms |
| Memory / SharpCompress Archive async | 2,127,304 | 245.39 ms |
| Memory / SharpCompress Reader async | 1,160,112 | 246.71 ms |
| Memory / .NET ZipArchive sync | 8,816 | 175.84 ms |
| Memory / .NET ZipArchive async | 10,808 | 175.38 ms |
| File / SharpCompress Archive sync | 449,640 | 250.90 ms |
| File / SharpCompress Reader sync | 449,600 | 254.09 ms |
| File / SharpCompress Archive async | 4,799,624 | 274.25 ms |
| File / SharpCompress Reader async | 3,135,352 | 268.42 ms |
| File / .NET ZipArchive sync | 13,104 | 184.96 ms |
| File / .NET ZipArchive async | 3,884,928 | 214.04 ms |

On memory-backed input, async Archive allocates about **1.68 MB more than sync**;
async Reader allocates about **0.71 MB more**. File-backed async allocations also
include suspended state machines. The .NET reference itself allocates substantially
more with asynchronous file input, so not all file-backed overhead is specific to
SharpCompress.

## Stored and checksum controls

| Memory-backed API | Deflate without CRC | Stored with CRC | Stored without CRC |
| --- | ---: | ---: | ---: |
| SharpCompress Archive sync | 163.15 ms | 82.70 ms | 4.00 ms |
| SharpCompress Reader sync | 165.96 ms | 83.91 ms | 4.42 ms |
| SharpCompress Archive async | 166.06 ms | 102.63 ms | 4.36 ms |
| SharpCompress Reader async | 168.62 ms | 83.11 ms | 4.16 ms |
| .NET ZipArchive sync | 103.18 ms | 82.70 ms | 3.65 ms |
| .NET ZipArchive async | 101.73 ms | 83.15 ms | 3.96 ms |

The Deflate-only gap persists without the common CRC pass: roughly **163–169 ms
versus 102–103 ms**. Stored ZIP is much closer, pointing to the codec/read chain
rather than a general ZIP-container bottleneck. The stored async Archive CRC
timing was higher in this process; it is not evidence of a separate codec cost.

Memory-backed stored extraction allocates only about **8–11 KB** per operation.
The common CRC pass costs roughly 75–85 ms on this host and dominates stored
extraction, so CRC-inclusive timings alone obscure codec performance.

## Allocation findings

### 1. Task-returning compressed-input read chain

Memory-backed allocation samples attribute about **1.7 MB/extraction** to
`Task<int>` in async Archive and about **0.7 MB** in async Reader. The principal
Archive chain is:

```text
ZlibBaseStream.ReadAsync(byte[], ...)
  -> ReadOnlySubStream.ReadAsync(byte[], ...)
  -> SharpCompressStream.ReadAsync(byte[], ...)
  -> SourceStream.ReadAsync(byte[], ...)
```

`src/SharpCompress/Compressors/Deflate/ZlibBaseStream.cs` refills its default
16 KiB compressed-input buffer through byte-array/Task overloads, even on modern
targets. The wrappers already have Memory/ValueTask overloads, but this call path
does not select them. Archive has more wrappers than Reader, explaining much of
the allocation difference.

**First candidate:** carry Memory/ValueTask reads through the compressed-input
path and provide a ValueTask-based codec read core. Preserve legacy/generated
sync behavior, input limits, stream ownership and cancellation. Savings must be
measured; existing Task-returning public overloads still need compatible adapters.

### 2. Per-block Huffman scratch arrays

Both sync and async traces attribute roughly **0.4 MB/extraction** to `int[]`
allocations in `InflateBlocks.Process`, accounting for most sync allocations.
In `src/SharpCompress/Compressors/Deflate/Inflate.cs`, dynamic-tree construction
allocates four single-element arrays (`bl`, `bd`, `tl`, `td`) for output values.
Fixed-tree construction also uses small arrays and array wrappers.

**Second candidate:** replace these temporary array-shaped outputs with ref/out
values, or bounded reusable/stack scratch. Renting a separate pool buffer for
each single integer would add overhead rather than address the representation.
The large Huffman workspace and sliding window are already pooled; do not count
them as fresh per-block allocations. Initialization and corrupt-input behavior
must remain intact.

### 3. Memory reads rent and copy decoded output

`ZlibBaseStream.ReadAsync(Memory<byte>)` currently rents an array, delegates to
the byte-array overload, copies decoded bytes into the caller's memory, and
returns the rental on every read. This is mostly a copy/dispatch cost after
warmup, not the dominant fresh-array allocation source.

**Third candidate:** decode directly into array-backed caller memory, retaining
a correct pooled fallback for non-array memory. A future core can eliminate the
intermediate Task as well. Merely changing callers to the array overload is
**not** an allocation fix: a controlled probe measured:

| Async Deflate / memory input | Memory overload | Array overload |
| --- | ---: | ---: |
| Archive allocated | 2,127,304 B | 2,357,704 B |
| Reader allocated | 1,160,112 B | 1,620,912 B |

Array-overload probes were slightly faster in some runs but allocated more
through additional Task-returning wrappers. Fix the library core rather than
recommending this as a caller-side allocation optimization.

### Caller-buffer sensitivity

| Async Deflate / memory input | 16 KiB caller buffer | 64 KiB | 256 KiB |
| --- | ---: | ---: | ---: |
| Archive allocated | 2,818,504 B | 2,127,304 B | 1,954,504 B |
| Reader allocated | 1,851,360 B | 1,160,112 B | 987,312 B |

Larger caller buffers reduce per-output-read Tasks but leave the compressed-input
chain in place. Throughput stayed around 245–250 ms; increasing output buffers
alone is not a solution to the underlying allocation cost. Caller-buffer storage
is outside these allocation deltas and larger buffers have a memory tradeoff.

## CPU interpretation and lower-priority work

Managed traces place work in the inflater, `InflateFast`, CRC and table setup.
They also attribute a surprisingly large leaf share to `ZeroMemoryInternal`
under inflater/tree code. A separate macOS native sample did **not** corroborate
a dominant native memset hotspot and could not resolve most JIT frames. Do not
interpret that managed leaf percentage as proof that clearing costs half the CPU,
or remove required initialization based on it. Exact allocations and untraced
timings are the stronger evidence here.

File traces include Monitor waits and suspended read state machines; sampled
thread time is not exact instruction CPU time. All nine EventPipe traces report
zero lost events. The shared analyzer has too few relevant CPU samples for some
.NET-reference traces; that does not mean the reference consumes no CPU.

For four entries, metadata-only opens allocate about **6.4–7.0 KB** in
SharpCompress and take **16–33 microseconds**. The .NET reference takes about
3–4 microseconds. This is negligible beside full extraction, so metadata object
pooling is not the first target for this workload.

The next CPU investigation should isolate the managed inflater from stream and
output-copy overhead, then measure hot-loop changes with CRC disabled as well as
enabled. The .NET reference is a useful native-backed comparison, not a promise
that a pure managed implementation can match every timing.

## Validation and artifacts

- Every measured CRC-enabled extraction verifies four entry CRCs and 200 MiB of
  output. Raw controls still verify entry lengths and total output.
- Independent ZIP validation passed for both generated archives.
- **1,002 ZIP/Deflate targeted tests pass** on .NET 10 Release.
- The initial investigation made no library changes. Implementation changes are
  described separately below; staging and commits remain under user control.

Artifacts are outside the repository:

```text
/private/var/folders/6c/j56r6kqn5yj7q1_c6vm5c0t80000gn/T/opencode/zip-200mb-profile/
```

`manifest.json` records the commit, archive hashes and library hash. `prepare.py`
creates/validates archives and snapshots the already-built library. `run.py`
reproduces the matrix, controls, buffer sweep and traces. `probe.py` compares
read overloads; `native-sample.py` performs the separate native-stack cross-check.
Harness sources, frozen library, logs, traces and analysis JSON are retained.
Tracing uses `dotnet-trace` 10.0.750501 and the shared TraceEvent analyzer under
`rar-100mb-profile/analyzer/`; its RAR-named phase marker is reused by the ZIP
harness and does not imply RAR decoding. Open Speedscope traces at
<https://www.speedscope.app/>.

## Allocation-optimization follow-up

The follow-up saves four independent library snapshots before measurement:
`baseline`, `reads` (target 1), `trees` (targets 1–2), and `direct` (targets 1–3).
They use the original ZIPs, caller buffers, CRC checks and harness boundaries.
Artifacts and the comparison script are in the artifact directory's
`optimization/` subdirectory; `library-hashes.json` identifies each snapshot.
These exploratory snapshots precede the final generated-sync overload correction.
The `final` snapshot and `final.py` revalidate the actual retained code; use the
`verified-*` logs for the final before/after comparison, not exploratory timings.

### Retained implementation

1. **Async read chain:** compressed-input refills select Memory/ValueTask
   overloads on modern targets. A shared ValueTask read core avoids materializing
   a Task for memory-based output reads. Public byte-array signatures remain
   unchanged, and generated sync reads retain byte-array I/O. Legacy targets keep
   their existing byte-array refill path.
2. **Huffman outputs:** fixed and dynamic tree builders use scalar/ref/out
   parameters instead of allocating single-element arrays and array wrappers for
   each block. Existing bit-length initialization and invalid-tree checks remain.
   The large pooled workspace and sliding window are unchanged.
3. **Output memory:** array-backed memory decodes directly into the requested
   array segment. Non-array memory retains the pooled fallback and returns its
   rental in `finally`. The codec's output-array reference is cleared after reads,
   including cancellation/failure, so caller memory or returned rentals are not
   retained by the decoder between operations.

No public API signatures or format support change. These shared codec changes
also require ZIP, GZip, Zlib and legacy-target validation; they do not introduce
a new sync Span read override or a new global mutable codec pool.

### Allocation savings by stage

For the same memory-backed 200 MiB extraction with CRCs and a 64 KiB caller buffer:

| API | Baseline | Target 1 | Targets 1–2 | All three targets |
| --- | ---: | ---: | ---: | ---: |
| Archive sync | 445,104 B | 445,104 B | 34,448 B | 34,448 B |
| Reader sync | 445,312 B | 445,312 B | 34,656 B | 34,656 B |
| Archive async | 2,127,304 B | 445,896 B | 35,240 B | 35,240 B |
| Reader async | 1,160,112 B | 445,872 B | 35,216 B | 35,216 B |

Target 1 removes the recurring Task allocation premium. Target 2 removes
**410,656 B/extraction** of tree scratch in this workload. Target 3 avoids the
per-output-read rental/copy but adds no further warm allocation reduction.
The final build reproduces these allocation totals while preserving byte-array
I/O for generated sync reads.

### Final before/after comparison

| Deflate / API | Baseline allocated | Final allocated | Baseline elapsed | Final elapsed |
| --- | ---: | ---: | ---: | ---: |
| Memory / Archive sync | 445,104 B | 34,448 B | 279.58 ms | 273.74 ms |
| Memory / Reader sync | 445,312 B | 34,656 B | 266.99 ms | 259.80 ms |
| Memory / Archive async | 2,127,304 B | 35,240 B | 287.89 ms | 265.48 ms |
| Memory / Reader async | 1,160,112 B | 35,216 B | 272.96 ms | 264.22 ms |
| File / Archive sync | 450,280 B | 38,984 B | 280.99 ms | 277.79 ms |
| File / Reader sync | 449,600 B | 38,944 B | 268.66 ms | 274.04 ms |
| File / Archive async | 4,799,632 B | 2,897,656 B | 288.79 ms | 286.93 ms |
| File / Reader async | 3,133,968 B | 2,129,952 B | 291.82 ms | 302.76 ms |

Memory-backed allocation savings are approximately **92% sync**, **98% async
Archive**, and **97% async Reader**. File-backed async allocations remain affected
by suspension and scheduling: their remaining samples are primarily async state
machine boxes rather than the removed per-block arrays and completed-read Tasks.

Two additional process pairs reversed execution order with CRC computation
disabled. Medians across their 14 measured extractions per version:

| Memory / raw Deflate | Baseline elapsed | Final elapsed |
| --- | ---: | ---: |
| Archive sync | 178.85 ms | 173.08 ms |
| Archive async | 178.33 ms | 173.01 ms |
| Reader async | 182.00 ms | 175.60 ms |

The repeat suggests a modest 3–4% improvement for these memory-backed cases,
not a general throughput guarantee. File results vary in both directions.
Target 3's isolated timing benefit is small/variable, so it is retained as copy
elimination without claiming a separate universal speedup. These changes do not
establish that the original native-backed .NET inflater throughput gap is closed.

Final async allocation totals are unchanged across 16, 64 and 256 KiB caller
buffers: **35,240 B Archive / 35,216 B Reader**. The former buffer-size-dependent
Task allocation cost is gone. Stored controls retain their original allocation
totals; the changes target the Deflate decoder.

### Final traces and validation

All six exploratory and four final EventPipe traces report zero lost events.
The baseline memory-backed Archive trace samples approximately 1.7 MB/extraction
of `Task<int>` and 0.4 MB of `int[]`. Final memory traces no longer sample the
recurring Task sites and have only four/five allocation ticks across ten measured
extractions. Some per-codec setup arrays remain; sparse samples and harness JSON
initialization do not support a detailed percentage split at this smaller total.

- **1,018 targeted ZIP/Deflate/GZip/Zlib tests** and **2,989 complete .NET 10
  Release tests** pass.
- All six library targets, net48 tests and the performance project compile
  without warnings/errors. net48 tests are not executed on macOS.
- New tests cover fixed/stored block transitions and read-ahead, empty and
  oversubscribed trees, sliced array memory, non-array memory, fragmented and
  genuinely suspended input, cancellation, corrupt input, I/O failures, caller
  memory reuse and clearing codec output references. A many-read allocation
  guard checks that the non-array fallback reuses its rental.
- Emitted sync code was inspected. The explicit `SYNC_ONLY` branch keeps the
  original `Read(byte[], offset, count)` input call; a regression source that
  rejects Span reads protects this behavior.
- CSharpier formatting/check and Git whitespace checks pass.
- No staging or commit is performed by this follow-up.
