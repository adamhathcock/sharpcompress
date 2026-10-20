# 7z extraction CPU and allocation profile

## Scope and baseline

Baseline: `master` at `0fbf36d6`; changes are on `perf/7zip-profile-pooling`.
Measured on 2026-10-19 using Release net10.0, .NET 10.0.13, Apple M4/macOS ARM64.
The baseline library and harness were copied before optimization; the updated
library uses a separate harness output. No builds or tests run concurrently
with the measurements. These are small controlled measurements, not universal
throughput guarantees.

The existing synchronous `SevenZipLzma2Extract_Reader` performance benchmark
created an extraction reader but then opened archive entry streams instead.
It now advances and drains the actual reader, matching the async benchmark's
intent. The standalone profiling harness also explicitly distinguishes the two
APIs rather than relying on the old benchmark labels.

## Workload and method

Four 50 MiB files alternate 64 KiB of seeded random data and 64 KiB of structured
records, giving **200 MiB extracted**. Native `7zz` generated four archives with
an 8 MiB dictionary, one compression thread, compression level 3, and compressed
headers. Large-file profiling uses LZMA2 solid-on; the other archives provide
additional smoke coverage, not an equally repeated performance matrix.

| Archive | Compressed bytes |
| --- | ---: |
| LZMA2 solid | 106,462,260 |
| LZMA2 non-solid | 106,459,937 |
| LZMA solid | 106,855,539 |
| LZMA non-solid | 106,855,693 |

Example creation command (solidness and method varied):

```sh
7zz a -t7z -mx=3 -m0=LZMA2:d=8m -mmt=1 -ms=on LZMA2-solid-on.7z mixed-*.bin
```

The primary comparison opens a new archive for each extraction, with two warmup
extractions followed by three untraced measured iterations. Memory-backed input
is loaded once outside measurement; a 64 KiB caller buffer is also allocated
outside measurement. All four entries are drained and total output size checked.
`LeaveStreamOpen = true` and `EnableParallelism = false` are explicit. Input
loading, output storage, and between-iteration logging are excluded. Output is
discarded. Allocations use exact `GC.GetTotalAllocatedBytes(true)` deltas around
extraction and disposal, not heap snapshots.

Separate traces repeat three measured extractions after warmup. `dotnet-trace`
10.0.750501 records sampled thread time and GC allocation ticks; TraceEvent
3.1.21 resolves stack names. Thread-time samples on macOS include waits and
safepoints and are not exact CPU instruction time. Allocation-tick weights are
approximate per-type attribution, unlike the exact total-allocation deltas.

The raw entry-stream benchmarks **do not add high-level payload CRC validation**.
High-level extraction checksum behavior is tested separately. These timings must
not be compared as equivalent work to RAR's checksum-validating extraction.

## Primary before/after results

Medians for the solid LZMA2 memory-backed workload (three measured extractions):

| Path | Baseline elapsed | Updated elapsed | Baseline allocated | Updated allocated |
| --- | ---: | ---: | ---: | ---: |
| Archive entries, sync | 2.434 s | 2.489 s | 238.4 KiB | 106.4 KiB |
| Archive entries, async | 12.594 s | 12.565 s | 8.95 MiB | 0.70 MiB |
| Extraction reader, sync | 2.504 s | 2.581 s | 239.5 KiB | 106.9 KiB |
| Extraction reader, async | 12.564 s | 12.480 s | 0.83 MiB | 8.68 MiB* |

Async Archive extraction saves approximately **8.25 MiB per extraction (92%)**,
while CPU/elapsed time is essentially unchanged. Sync allocation savings are
approximately 132 KiB per extraction, consistent with releasing and reusing the
encoded-header input cache. There is no claim of a decoder throughput gain.

\*The async Reader allocation result was not stable. An additional diagnostic
repeat with ArrayPool events recorded 9.12 MB in its first measured iteration,
then 0.734 MB in each of the next two. Large-buffer events show matched rents
and returns (12 each in the measured phase), including the final 8 MiB dictionary
being returned and reused across all three iterations. Intermediate 1, 2 and
4 MiB growth buffers required fresh allocations in the first measured iteration.
This demonstrates pool-capacity misses rather than an unreturned dictionary;
the primary Reader allocation figure is not presented as a reliable regression
or improvement. Pool diagnostics add overhead and their timings are excluded
from the main comparison. All five traces report zero lost events.

### Metadata-only comparison

Separate runs enumerate the four entries without opening payload streams. Each
result is the median of three batches of 100 archive opens after two warmups;
compressed input is preloaded outside the measured region.

| Metadata path | Baseline bytes/open | Updated bytes/open | Baseline ms/open | Updated ms/open |
| --- | ---: | ---: | ---: | ---: |
| Sync | 192,962 | 57,754 | 0.096 | 0.076 |
| Async | 193,408 | 58,200 | 0.245 | 0.234 |

Both paths save approximately **135,208 allocated bytes per open (70%)**. This
isolates temporary encoded-header decoder cleanup from payload dictionary rents.

## Findings and implementation

### Cached folder decoder lifetime

The sync Archive API disposes its cached folder decoder, but the baseline async
Archive API did not. For this workload, allocation samples attribute about
8 MiB per measured iteration to dictionary rents in `OutWindow.PutByteAsync`.
The reader already releases its own folder decoder, exposing a large difference
between superficially similar async extraction APIs.

The updated `SevenZipArchive.DisposeAsync` disposes the cached folder stream
before releasing archive resources. Cache references and the predicted folder
position are cleared before disposal, making repeated disposal safe even when
the decoder throws. `LeaveStreamOpen` still controls archive source ownership.

### Encoded header decoder lifetime

Both metadata parsers constructed a temporary decoder for encoded headers
without disposing it. This stranded pooled input-cache and dictionary buffers.
The decoder now has a scoped sync/async lifetime, including CRC validation
failure. Decoded metadata arrays retain their normal owned lifetime; they are
not returned to a pool while parser structures may still reference them.

An internal injectable input-buffer pool permits deterministic tests that verify
all rents are returned and poison returned buffers to detect accidental reuse.

### Partial LZMA reads

The range decoder normally returns its pooled fast input buffer at the end of a
chunk. Partial reads or failed decoding can bypass that path. Both LzmaStream
cleanup paths now release the range decoder's stream/buffer as well as the
dictionary and async header buffer. This also benefits other LZMA containers;
it does not close a caller-owned source.

### CPU and remaining allocations

In the baseline sync trace, approximately **99.5%** of filtered leaf samples are
in `Decoder.CodeFastCore`; match copying and range decoding can be inlined there.
The async path uses the scalar async codec, not that fast loop: approximately
61% of baseline samples are in literal decoding, 13% in range normalization,
10% in `CodeAsync`, and 8% in the async bit decoder helper. This explains why
async extraction takes substantially longer on a memory-backed CPU-bound
workload; the lifetime fixes do not rewrite the async codec.

CRC tables are already static/shared. `Crc.From` has a temporary buffer but no
production call sites in this checkout, so pooling it would not address this
profile. Decoder probability arrays also allocate, but this investigation does
not introduce a global decoder pool or change LZMA2 reset semantics without
separate ownership/reset validation and evidence of a significant benefit.

## Integrity and compatibility

High-level entry extraction already validates 7z payload CRCs synchronously and
asynchronously. Tests now cover LZMA, LZMA2 and solid fixtures, including explicit
`CheckCrc = false` on both paths. Raw entry-stream behavior is deliberately
unchanged; no new checksum guarantee or public API is introduced.

Lifetime tests cover borrowed source ownership, partial/full entry reads,
repeated disposal, encoded-header success and CRC failure, and range-buffer
return after partial LZMA decoding. Broader XZ/LZMA tests protect shared codec
behavior. Parallel decoding is deliberately excluded from timing comparisons:
its opt-in file-backed path has different applicability and I/O costs.

## Artifacts and reproduction

All generated workloads, harnesses and traces are outside the repository:

```text
/private/var/folders/6c/j56r6kqn5yj7q1_c6vm5c0t80000gn/T/opencode/sevenzip-profile/
```

`generate.py` creates the workloads. `baseline.py` and `updated.py` run the
comparison and traces. `baseline-library/`, `baseline-harness/`, and
`updated-harness/` preserve the separate builds. Logs and analysis JSON identify
version and API. Open `.speedscope.json` files in <https://www.speedscope.app/>.
The analyzer is reused from the separate `rar-100mb-profile/analyzer` directory;
its phase-marker protocol is shared by the standalone harnesses.

`post.py` builds the metadata-only harness, verifies the actual payload CRCs for
all four generated archives outside the timed region, and collects the extra
Reader pool diagnostic trace. `pool-analysis/` summarizes large-buffer events.

## Validation and remaining work

- **428 targeted 7z/LZMA/XZ tests passed**, including the new lifetime and CRC
  regressions. The complete .NET 10 Release suite reports **2,989 passed, no
  failures**.
- All six library targets, the performance project, and the net48 test assembly
  build successfully. net48 tests were not executed on macOS.
- Large LZMA/LZMA2 solid/non-solid smoke extractions completed, including sync
  and async file-backed cases. Independent, untimed payload CRC checks passed
  for every file in all four generated archives.
- CSharpier formatting/check and Git whitespace checks pass.

No decoder algorithm rewrite, global model pooling, or raw-stream checksum API
change was made. A fully repeated cross-product of codec, solidness, input and
API remains future benchmarking work; the large-file primary before/after
profile is explicitly limited to the solid LZMA2 memory-backed case. Optional
parallel decoding needs a separate evaluation. Generated archives and raw traces
are kept outside the repository.

## Follow-up: read-chain, copy-buffer and model allocations

This comparison starts at `14d77b67`, **after** the lifetime fixes above. It
evaluates the five remaining candidates, with separate saved libraries for the
baseline, pooled-copy/memory-read/decoder-reuse changes, lazy model initialization,
and an experimental dictionary growth policy. The growth experiment was rejected;
the retained implementation corresponds to the lazy-model stage.

### Retained changes

1. Writer CRC copies rent a 64 KiB buffer and return it in `finally`, including
   cancellation/failure. Reads use the logical size rather than rented capacity.
   This avoids rounding the old 80 KiB allocation up to a 128 KiB pool bucket.
2. On modern targets, `OutWindow.CopyStreamAsync` and buffered-input refills call
   the existing `Memory<byte>` / `ValueTask<int>` overloads. Legacy targets keep
   their byte-array path; generated synchronous refills retain their original
   byte-array calls. Public signatures and buffering limits are unchanged.
3. LZMA2 property/state reset controls reuse the stream's decoder instead of
   constructing a new probability-model object graph. Properties still reset
   probabilities, match state and distances on every required reset.
4. Literal/length scalar models and fast probability models initialize only
   when that backend is first used after a reset. This is lazy initialization,
   not a global mutable decoder pool. Shared match state is not reset merely
   because a direct decoder caller activates a backend.

### Measurements

Each library/harness is built before measurement. The large-file comparison uses
the same solid LZMA2 200 MiB payload, two warmups and three measured extractions;
microbenchmarks use two warmups and five measured iterations. Tables are medians
of exact managed allocation deltas. Input buffers and encoded reset fixtures are
prepared outside measurement; no builds/tests compete with measurements.

| Large extraction | Baseline allocated | Retained changes allocated | Baseline elapsed | Retained changes elapsed |
| --- | ---: | ---: | ---: | ---: |
| Archive sync | 108,920 B | 57,640 B | 2.670 s | 2.747 s |
| Archive async | 729,952 B | 76,024 B | 13.845 s | 13.832 s |

This is approximately **47% lower sync** and **90% lower async** allocation.
The memory-read/reuse stage alone measured 108,568 B async; lazy models reduced
that further to 76,024 B. Elapsed time does not establish a large-file speedup.

The copy microbenchmark invokes the actual private CRC-copy methods through
delegates, using 1,000 independent 4 KiB sources and `Stream.Null` output:

| Copy workload | Baseline allocated | Retained changes allocated |
| --- | ---: | ---: |
| 1,000 sync copies | 82,008,040 B | 64,040 B |
| 1,000 async copies | 82,080,040 B | 64,040 B |

The remaining allocation includes the 1,000 caller-created MemoryStream objects;
the warm copy buffers no longer allocate per call. This isolated copy benchmark
does not include encoder/model or archive-metadata costs.

For an end-to-end writer check, one iteration writes 16 independent 4 KiB random
files to a seekable MemoryStream, including finalization:

| Writer workload | Baseline allocated | Updated allocated | Baseline elapsed | Updated elapsed |
| --- | ---: | ---: | ---: | ---: |
| LZMA sync | 7,782,240 B | 6,389,192 B | 21.0 ms | 18.3 ms |
| LZMA async | 5,733,136 B | 4,338,864 B | 35.5 ms | 35.8 ms |
| LZMA2 sync | 6,386,152 B | 4,582,864 B | 23.0 ms | 23.3 ms |
| LZMA2 async | 6,463,480 B | 4,658,968 B | 23.7 ms | 24.7 ms |

Those writer runs use the experimental-growth build, but the changed decoder
growth policy is not exercised by writing. Pool-state variability affects total
encoder allocations, so do not attribute every byte saved to the copy buffer.
There is no general writer throughput claim.

A reset-heavy microbenchmark concatenates 64 independently compressed 4 KiB
LZMA2 chunks (256 KiB output), verifies the decoded bytes and drains the stream:

| Reset workload | Baseline | Decoder reuse + read changes | With lazy models |
| --- | ---: | ---: | ---: |
| Sync allocated | 3,151,832 B | 49,712 B | 24,072 B |
| Async allocated | 3,151,864 B | 49,744 B | 33,472 B |

### Growth policies rejected

An initial read-limit-based lookahead failed the existing truncated-input
allocation test: it allocated a 128 KiB bucket where the guard permits at most
8 KiB. That approach was removed without weakening the test.

A second candidate retained 2x growth below 64 KiB capacity, then used 4x growth.
A counting, non-retaining pool isolated cold allocation/copy costs. At an 8 MiB
history, rents fell from 12 to 9 and requested bytes from 16,773,120 to 14,020,608.
However, a 70 KiB history increased requested bytes from 258,048 to 389,120 and
retained a 256 KiB instead of 128 KiB buffer. That **128 KiB small-file regression**
outweighed a cold-only large-history win. Production retains its existing 2x,
history-driven growth; a regression test protects the small-history case.

### Follow-up artifacts and coverage

The original artifact directory's `round2/` subdirectory contains each library
and harness snapshot, microbenchmark source, raw logs and follow-up traces.
`run.py` runs the staged extraction/copy/reset/cold-growth comparisons; `post.py`
checks the adverse small-history case and full writers. `trace.py` records
baseline/final copy allocations and final extraction stacks separately.

New regression tests cover LZMA2 dictionary/property/state reset controls
(`0xE0`, `0xC0`, `0xA0`), changing lc/lp/pb, reuse of the same decoder instance,
unused model banks, memory-only async sources, small-history buffer ownership,
and LZMA/LZMA2 writer round-trips across 64 KiB boundaries with verified CRCs.

Follow-up EventPipe traces report zero lost events. The copy trace's measured
allocation samples change from about 410 MB of temporary byte arrays across
five batches to MemoryStream objects from the harness, with no corresponding
recurring copy-buffer samples. The final extraction trace no longer samples
the previously dominant `Task<int>` allocation sites. Its remaining allocation
samples are sparse at this smaller total, so exact byte deltas and memory-only
source tests are stronger evidence than a per-type percentage breakdown.

Validation for this follow-up: **466 targeted tests** and **2,997 complete .NET
10 Release tests** pass. All six library targets and net48 test compilation
succeed; net48 tests were not run on macOS. Formatting and whitespace checks
pass. No public signatures or CRC-validation semantics changed. This phase did
not introduce a global pool or increase the production dictionary-growth factor.

## Follow-up: encoder workspaces and metadata scratch

This round starts at `03056961`, after the preceding changes. Saved libraries,
benchmark sources, JSON logs and EventPipe traces live in the external artifact
directory's `round3/` subdirectory. Comparisons use Release net10.0, two warmups
and five measured microbenchmark iterations. Large extraction uses the existing
200 MiB solid LZMA2 archive, two warmups and three measured iterations. Inputs,
metadata objects and reflection delegates are prepared outside measurement.
Builds and tests do not run concurrently with measured workloads.

### Implementation

- The encoder rents its 4,096-element optimization workspace on first use,
  clears its logical range on initialization and returns it on disposal. This
  removes a recurring large-object allocation without pooling encoder objects.
- LZMA2 block compression keeps its pooled output stream alive through trial
  decoding and chunk writing instead of materializing it with `ToArray()`.
  The output is truncated to the trial decoder's consumed byte count, preserving
  range-coder flush-byte handling. Uncompressed fallback still works; exceptions
  return the temporary output and invalidate failed trial-decoder state.
- A trial decoder is reused within each LZMA2 encoder stream. Fixed scalar
  decoder tables initialize only when scalar decoding is actually needed.
  Scalar literal probabilities are pooled. Fast probabilities share one pooled
  bank containing fixed tables followed by literals, keeping the default bank
  within a 16 KiB pool bucket. Initialization uses logical lengths, not capacity.
  Both stream-disposal paths now dispose their decoder to return model rentals.
- Direct `LzmaStream.WriteAsync(ReadOnlyMemory<byte>)` borrows memory through an
  internal read-only adapter until encoding completes, rather than copying it.
  This includes memory not backed by an array. The current 7z copy loop calls the
  array overload, so this benefit is measured separately from archive writing.
- Synchronous integer, digest, coder-ID, timestamp, attribute and signature-header
  scratch uses bounded stack buffers. Folder validation's 32-word stack mask is
  explicitly cleared. Async signature headers retain owned storage across awaits.
  Legacy span-write compatibility can still rent/copy; it is not a zero-copy claim.
- FilesInfo computes property sizes and writes directly, eliminating temporary
  property streams, closures and bool arrays. Names use 512-byte stack scratch
  for short names on modern targets and a reusable rented buffer for long names.
  Legacy encoding uses a rented buffer. CRC-defined vectors also write directly.

No public API signatures, dictionary-growth policy, stream ownership defaults
or checksum-validation semantics change.

### Match-finder bucket experiment rejected

The binary-tree match finder needs `2 * (dictionarySize + 1)` uint entries.
For a 1 MiB dictionary, its logical payload is 8 MiB + 8 bytes, crossing into a
16 MiB shared-pool bucket on modern .NET. An experimental build replaced this
default rental with an exact-size owned array, retaining custom-pool behavior.
It reduced capacity per active encoder but lost warm buffer reuse: 16 small
files allocated **145,934,592 B for LZMA** and **143,684,800 B for LZMA2**, with
median elapsed times of 20.7 ms and 26.7 ms respectively. This is substantially
worse allocation behavior than the retained shared-pool implementation.

The exact-array experiment was removed. A bounded exact-size reusable cache
could address both concerns, but was not introduced: it requires a separate
retention/concurrency policy rather than silently replacing shared pooling with
an unbounded second cache or reducing the effective dictionary size. Likewise,
the 17 KiB length-price tables are not independently pooled into 32 KiB buckets;
this round prioritizes the existing large optimization workspace and model bank.

### Results

Exact managed allocation deltas are medians after warmup. These results describe
pool reuse, not guaranteed allocation-free cold starts or lower process RSS.
Pool capacity can exceed logical data sizes; oversized literal banks are released
when switching to substantially smaller context properties rather than retained
for the decoder's entire remaining lifetime.

| Workload | Baseline allocated | Updated allocated |
| --- | ---: | ---: |
| 16 independent 4 KiB files, LZMA writer sync | 6,389,192 B | 3,319,696 B |
| Same files, LZMA writer async | 4,338,864 B | 1,269,480 B |
| Same files, LZMA2 writer sync | 4,582,864 B | 1,071,896 B |
| Same files, LZMA2 writer async | 4,658,968 B | 1,148,112 B |
| Three compressible 2 MiB LZMA2 blocks, sync/async | 307,360 B | 55,144 B |
| Three random 2 MiB LZMA2 blocks, sync/async | 6,684,832 B | 56,704 B |
| Serialize 10,000 mixed metadata entries | 2,868,992 B | 40 B |
| 64 direct 4 KiB LZMA memory writes | 501,360 B | 56,408 B |
| Decode 64 independent reset chunks, sync | 24,072 B | 712 B |
| Decode 64 independent reset chunks, async | 33,472 B | 8,784 B |

The metadata microbenchmark includes long/short Unicode names, partial times,
directories and empty files. Metadata objects and delegates already exist; it
does not include archive creation, header compression, or public entry objects.
The remaining 40 bytes are the harness stopwatch. The block-compression benchmark
discards output to `Stream.Null`; round-trip tests independently validate emitted
data. The writer benchmark includes finalization and seekable MemoryStream output.

| 200 MiB extraction | Baseline allocated | Updated allocated | Baseline elapsed | Updated elapsed |
| --- | ---: | ---: | ---: | ---: |
| Archive entries sync | 57,640 B | 9,992 B | 2.612 s | 2.554 s |
| Archive entries async | 76,024 B | 26,344 B | 14.420 s | 14.130 s |

Allocation savings are approximately **83% sync / 65% async** relative to this
round's already-optimized baseline. These raw entry-stream extractions retain
the earlier report's checksum-validation caveat.

Writer elapsed times were noisy in the main run. A separate three-process-pair
repeat alternated baseline/final execution order; each process warmed twice and
measured five iterations. Medians across the 15 measured iterations per version:

| Small-file writer | Baseline elapsed | Updated elapsed |
| --- | ---: | ---: |
| LZMA sync | 18.57 ms | 18.66 ms |
| LZMA async | 34.49 ms | 34.87 ms |
| LZMA2 sync | 22.51 ms | 23.52 ms |
| LZMA2 async | 22.36 ms | 23.15 ms |

LZMA is essentially unchanged; **small-file LZMA2 costs about 3–5% elapsed time**
in this repeat while allocating 75–77% less. The allocation-focused changes are
retained with that tradeoff explicit, not described as a universal throughput
improvement. Large/block timing variation likewise does not justify a general
speedup claim.

Separate baseline/final EventPipe sampled-thread-time and allocation traces of
random-block writing report **zero lost events**. Baseline measured-phase samples
include about 31.9 MB of byte arrays and 0.90 MB of `Optimal[]`; the final trace
has no corresponding recurring samples. Remaining samples are sparse. CPU leaves
remain dominated by encoder block processing, binary-tree matching and trial
decoding. Exact allocation deltas are stronger evidence than sampled percentages.

`run.py` reproduces microbenchmarks, extraction comparisons and traces;
`repeat.py` reproduces the interleaved writer timing check; `exact.py` measures
the rejected exact-array match-finder snapshot. Baseline, intermediate,
consolidated-final and exact-array libraries are saved separately.

### Validation

- **485 targeted tests** and **3,016 complete .NET 10 Release tests** pass.
- All six library targets, net48 tests, and the performance project compile
  without warnings/errors. net48 tests are not executed on macOS.
- New coverage exercises multiple compressed/uncompressed LZMA2 blocks, partial
  decoder disposal, failed raw decoding, pooled workspace disposal, shrinking
  context banks, non-array memory writes, cancellation and caller-memory reuse,
  long/invalid-surrogate names, partial metadata and digest-vector byte boundaries.
- CSharpier formatting/check and whitespace validation pass.
