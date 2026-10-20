# 7z/LZMA CPU and allocation profile

## Scope

Latest comparison on `perf/7zip-profile-pooling`:

- **Baseline:** pre-merge `f41cc208`.
- **Updated:** merge commit `1ad052ed`, incorporating `origin/master` at `7ddb0121`.
- **Environment:** Release net10.0, .NET 10.0.13, Apple M4/macOS ARM64.

Both versions already contain pooled encoder workspaces, decoder model reuse,
stack scratch, direct metadata serialization and pooled LZMA2 compressed output.
This comparison measures the upstream merge, not the cumulative improvement over
the original unoptimized library.

**Outcome:** existing allocation savings are preserved. Async archive writing
allocates less, especially with large raw headers. Elapsed-time variation does
not establish a general throughput improvement or extraction regression.

## Measurement method

Libraries and harnesses are saved separately before measurement. No builds or
tests run concurrently with measured workloads. Inputs, caller buffers, metadata
objects and reflection delegates are prepared outside the measurement window.

- Allocations are exact `GC.GetTotalAllocatedBytes(true)` deltas, not heap sizes.
- Microbenchmarks use two warmups and five measured iterations; tables show medians.
- Extraction uses two warmups and three measured iterations per process.
- Writer timing repeats alternate version order across three process pairs,
  giving 15 measured iterations per version after warmup.
- Large-header writing has one untimed validating write, then two warmups and
  five measured writes. Archive parsing and header CRC checks are outside timing.
- EventPipe sampled-thread-time and allocation traces are collected separately
  from untraced measurements. On macOS, thread-time samples include waits and
  safepoints; allocation-tick weights are approximate type attribution.

These are controlled **warm-pool allocation measurements**, not guarantees of
allocation-free cold starts, lower retained capacity, or lower process RSS.

## Workloads

| Workload | Contents and measurement boundary |
| --- | --- |
| Archive extraction | Four 50 MiB files alternating seeded random and structured 64 KiB regions; 200 MiB total output, discarded through a preallocated 64 KiB buffer |
| Small-file writing | 16 independent 4 KiB random files; seekable MemoryStream output, including archive finalization |
| LZMA2 block writing | Three independently compressed 2 MiB blocks, structured or random; output discarded to `Stream.Null` |
| Metadata serialization | 10,000 prebuilt entries with long/short Unicode names, partial times, directories and empty files; serializer output discarded |
| Direct LZMA memory writes | 64 writes of the same 4 KiB caller buffer through `WriteAsync(ReadOnlyMemory<byte>)` |
| Reset-heavy decoding | 64 independently compressed 4 KiB LZMA2 chunks; decoded bytes verified |
| Large-header archive writing | 10,000 empty files with long/short Unicode names and partial times; complete async archive creation with raw or compressed headers |

The extraction archive uses solid LZMA2, an 8 MiB dictionary, one compression
thread, compression level 3 and compressed headers. It was generated with native
`7zz`; its compressed size is 106,462,260 bytes. Each extraction opens a new
archive over preloaded memory, drains all four entries and checks total output.
`LeaveStreamOpen = true` and `EnableParallelism = false` are explicit.

Raw entry-stream extraction **does not add high-level payload CRC validation**.
Checksum-validating extraction is tested separately; these timings must not be
compared as equivalent work to checksum-validating extraction in other formats.
Parallel decoding and a full codec/solidness/I/O/API matrix are outside this profile.

## Allocation results

| Workload | Baseline allocated | Updated allocated |
| --- | ---: | ---: |
| 16-file LZMA writer sync | 3,319,696 B | 3,319,696 B |
| 16-file LZMA writer async | 1,269,480 B | 1,187,584 B |
| 16-file LZMA2 writer sync | 1,071,896 B | 1,071,896 B |
| 16-file LZMA2 writer async | 1,148,112 B | 1,066,216 B |
| Three compressible 2 MiB LZMA2 blocks, sync/async | 55,144 B | 55,144 B |
| Three random 2 MiB LZMA2 blocks, sync/async | 56,704 B | 56,704 B |
| Serialize 10,000 metadata entries | 40 B | 40 B |
| 64 direct LZMA memory writes | 56,408 B | 56,408 B |
| Decode 64 reset chunks sync | 712 B | 712 B |
| Decode 64 reset chunks async | 8,784 B | 8,784 B |
| 200 MiB extraction sync | 10,952 B | 10,952 B |
| 200 MiB extraction async | 26,344 B | 26,344 B |

Async archive writers save **81,896 B per 16-file batch**. Other allocation totals
are unchanged. The metadata serializer's remaining 40 bytes are the harness
stopwatch; this workload excludes entry construction, header compression and
archive finalization, so it is not an allocation-free archive-writing claim.

### Complete large-header archive writing

| Header | Baseline allocated | Updated allocated | Baseline elapsed | Updated elapsed | Archive bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| Raw | 11,867,624 B | 9,164,288 B | 7.31 ms | 7.47 ms | 2,688,632 |
| Compressed | 1,076,032 B | 994,136 B | 142.71 ms | 136.91 ms | 14,033 |

Raw-header writing saves **2,703,336 B (23%)**, primarily by avoiding
materialization of pooled header data when calculating CRCs. Both versions
produce identical archive lengths; all names parse correctly and header CRC
validation succeeds. Identical lengths alone are not a byte-for-byte comparison.

## Throughput and CPU observations

The interleaved small-file writer repeat measured:

| Writer | Baseline elapsed | Updated elapsed |
| --- | ---: | ---: |
| LZMA sync | 22.79 ms | 24.55 ms |
| LZMA async | 43.03 ms | 43.56 ms |
| LZMA2 sync | 30.55 ms | 29.98 ms |
| LZMA2 async | 29.73 ms | 30.59 ms |

Timings vary in both directions. The extraction comparison also shows execution-
order sensitivity:

| Extraction comparison | Baseline elapsed | Updated elapsed |
| --- | ---: | ---: |
| Sync, initial run | 2.472 s | 2.486 s |
| Async, baseline process first | 12.653 s | 15.437 s |
| Async, updated process first | 13.600 s | 12.714 s |

The initial async slowdown did not reproduce when process order was reversed.
The reverse-order allocation medians were 26,296 B baseline and 26,344 B updated,
only 48 B apart. These results do not establish an extraction speedup or regression.

Separate baseline/updated random-block traces report **zero lost events**, with
four and three allocation ticks respectively. Neither samples recurring
per-block compressed byte arrays or encoder optimization workspaces. Both CPU
profiles remain dominated by binary-tree matching, encoder block processing and
trial decoding. Sparse allocation samples do not support a detailed percentage
split; exact allocation deltas are stronger evidence.

## Implementation and correctness

The merge retains reusable trial decoders and explicit pooled-model ownership,
while adopting upstream's validated/disposable read-only memory adapter and
pooled-header CRC handling. Compressed-chunk async copies keep a logical 64 KiB
limit rather than requesting 80 KiB and rounding into a 128 KiB pool bucket.
Public signatures, dictionary-growth policy, stream ownership defaults and
checksum-validation semantics remain unchanged.

- **497 targeted tests** and **3,028 complete .NET 10 Release tests** pass.
- All six library targets, net48 tests and the performance project compile
  without warnings/errors. net48 tests were not executed on macOS.
- Coverage includes compressed/uncompressed block boundaries, partial and failed
  decoding, disposal and buffer returns, property resets and shrinking model
  banks, non-array memory, cancellation and caller-memory reuse, Unicode names,
  partial metadata, digest vectors and header CRCs.
- CSharpier and Git whitespace checks pass.

## Artifacts and reproduction

Generated archives, library snapshots, harnesses, logs and traces remain outside
the repository. The latest comparison is in:

```text
/private/var/folders/6c/j56r6kqn5yj7q1_c6vm5c0t80000gn/T/opencode/sevenzip-profile/round4/
```

- `manifest.json`: source revisions and library SHA-256 hashes.
- `baseline-library/` and `final-library/`: separate pre-merge and merged builds.
- `micro/`, `headers/` and the parent directory's `harness/`: benchmark sources.
- `run.py`: microbenchmarks, extraction, EventPipe traces and writer timing repeats.
- `repeat.py`: interleaved small-file writer timing check.
- `headers.py`: complete raw/compressed-header writing and untimed validation.
- `extraction-repeat.py`: reverse-order async extraction check.
- `*.log` and `*-analysis.json`: raw measurements and trace analysis.

Run scripts from `round4/`; they use the saved library snapshots. `run.py` builds
each harness before its measurement phase. The parent directory's `generate.py`
creates archive workloads. Open `.speedscope.json` traces at
<https://www.speedscope.app/>. Tracing uses `dotnet-trace` 10.0.750501 and the
shared TraceEvent 3.1.21 analyzer in `rar-100mb-profile/analyzer/`.
