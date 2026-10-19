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
