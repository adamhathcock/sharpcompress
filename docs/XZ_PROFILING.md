# XZ BCJ pooling and decoder profiling

## Branch and changes

Evaluated on 2026-10-24 on `fix/xz-bcj-streaming`. The baseline is
`0aaf9b92` (the streaming BCJ correctness fix); the pooled version is
`5c46363f`. Both libraries are clean Release builds in separate source exports,
using the same harness and inputs. The original broken BCJ implementation is
not a meaningful performance baseline: it can omit conversions or corrupt
output, so it would not perform equivalent work.

Changes evaluated:

- The shared 8 KiB BCJ buffer is rented from `ArrayPool<byte>.Shared` and returned
  exactly once on disposal. Processing is bounded by the logical buffer size and
  valid input count, not an oversized rent's capacity.
- All six XZ executable filters use that buffer: x86, ARM, ARM-Thumb, PowerPC,
  SPARC, and IA-64. Unprocessed instruction tails remain buffered between reads.
- XZ blocks retain their filter objects for cleanup. Advancing to another block
  disposes the previous block, and disposing an XZ stream releases its current
  block. This returns the BCJ buffer and the LZMA2 decoder's existing pooled
  dictionary/range buffers; previously the filter chain was not disposed.
- Filters dispose their own wrappers, not caller-owned input streams. The LZMA2
  wrapper explicitly leaves its compressed input open. Disposed readers reject
  further reads before they can use or rent buffers again.

No range-coder, LZMA decoding, checksum, or async scheduling optimization is
included in the pooled comparison.

The saved library diff was checked against these commits byte-for-byte. The
exported baseline DLL SHA-256 is
`f7c9c5ac700315205268a0f1aec0c8517289e6f81bfd9d1459d0ec3c17d5985c`;
the pooled DLL SHA-256 is
`ab7cee7f00bcc5e37f967ca0f74ff3c34a8eb93ee128cf08ca41488987106961`.

## Workloads and measurement

### Complete XZ decoding

The controlled workload is **16.36 MiB compressed** (17,159,680 bytes),
**32 MiB decoded** (33,554,432 bytes), in one XZ block using x86 BCJ, LZMA2,
an 8 MiB dictionary, and CRC64. Python 3.9.6's liblzma-backed `lzma.compress`
created it with preset 6. A generator seeded with `1471` alternates 64 KiB of
random bytes and 64 KiB of structured x86 records. Each 16-byte structured
record contains CALL and JMP instructions targeting fixed absolute addresses.
This exercises both incompressible data and BCJ-friendly instructions.

Compressed SHA-256:
`d1b182144055a3d1767944f32081bc2142f703372dae9d28f5073b629a733e1b`.

Decoded SHA-256:
`9150fb1e85094a06b7aeb197908a30d3a8466e4d298f629d13b3f0ca99243715`.

Every extraction drains through the XZ checksum and verifies the decoded byte
count. Memory-backed scenarios preload compressed bytes outside measurement.
File-backed scenarios use a 4 KiB `FileStream` buffer, `SequentialScan`, and
`FileOptions.Asynchronous`. Filesystem-cache effects are warm, not cold-storage
performance. The synchronous API, async array API (`Task<int>`), and async memory
API (`ValueTask<int>`) all use 64 KiB requests with a nonzero destination offset.

Each version/scenario runs in a fresh process with two warmups and seven measured
extractions. Version order alternates between scenarios. Release `net10.0` runs
disable tiered compilation. No builds or tests compete with the timed runs.
Measurements use .NET 10.0.13 on Apple M4/macOS 27.0.1 ARM64.

Allocation figures are exact process-wide `GC.GetTotalAllocatedBytes(true)`
deltas around extraction **and disposal**. They include the caller buffer and
stream construction, but exclude compressed-input preloading, process startup,
and between-iteration logging. They are allocation counts, not live memory or
peak working set. The pooled decoder still needs an 8 MiB dictionary and an
8 KiB BCJ buffer while decoding.

### Isolated executable filters

Each of the six filters separately decodes 1 MiB of repeated architecture-specific
encoded instructions, without LZMA2 or XZ checksum work. Request sizes are 1, 16,
and 10,240 bytes, using sync and both async read overloads. Input setup is excluded;
the destination buffer and filter lifetime are included. These artificial inputs
isolate wrapper/transform costs and do not represent typical executable files.

### Sampling traces

Separate traces use `dotnet-trace` 10.0.750501 with `dotnet-sampled-thread-time`, CLR GC
allocation ticks, and custom measured-phase markers. XZ traces contain three
extractions after two warmups. Each isolated-filter trace contains 10,000
iterations after warmup. TraceEvent 3.1.21 performs analysis; startup and warmup
events are excluded using the phase markers.

Stack shares are sampled managed thread time, not exact CPU instruction counts.
The report separates stacks containing SharpCompress work from unrelated runtime
background stacks. File-backed stacks can include waits/safepoints, and inlining
can attribute helper costs to their caller. Allocation ticks provide sampled site
attribution; the untraced exact allocation deltas are the authoritative totals.

## Fresh results

Medians per complete **32 MiB** XZ extraction, including construction and disposal:

| Input / API | Baseline ms | Pooled ms | Baseline allocated bytes | Pooled allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| Memory / sync | 943.8 | 869.3 | 8,482,480 | 69,584 |
| Memory / async array | 6,806.8 | 6,718.0 | 8,952,504 | 523,184 |
| Memory / async memory | 6,607.1 | 6,770.7 | 8,952,504 | 523,184 |
| File / sync | 1,128.0 | 898.2 | 8,486,704 | 74,096 |
| File / async array | 7,892.0 | 7,885.2 | 15,116,192 | 8,695,936 |
| File / async memory | 7,899.9 | 17,081.7 | 14,392,056 | 10,287,064 |

The memory-backed comparison saves approximately **8 MiB per extraction**:
**99.2% of warm sync allocations** and **94.2% of warm async allocations**.
Most of this comes from returning the existing pooled LZMA2 dictionary; the BCJ
buffer alone accounts for only about 8 KiB. Pooling the BCJ buffer without fixing
filter-chain disposal would not return either lease reliably.

This is **not evidence of a general throughput improvement**. Memory-backed
async process CPU medians were 6,508 ms before versus 6,520 ms after for the
array API, and 6,466 ms versus 6,518 ms for the memory API. The file/memory-async
case was substantially slower after the change in this run (process CPU medians
7,748 ms versus 10,064 ms, with additional elapsed waiting time). That result is
retained, not discarded. File-backed scheduling, suspension allocations, warm
pool behavior, and machine state can vary; the smaller file allocation reductions
should not be treated as precise attribution to a specific buffer. A separate
repeatability/async-I/O investigation is needed before claiming file throughput
benefits or explaining that slowdown.

### All six isolated filters

Synchronous 1 MiB drains with **10,240-byte reads**, including the destination
buffer and construction/disposal:

| Filter | Baseline ms | Pooled ms | Baseline allocated bytes | Pooled allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| x86 | 0.387 | 0.388 | 19,064 | 10,528 |
| ARM | 0.614 | 0.607 | 18,992 | 11,128 |
| ARM-Thumb | 0.422 | 0.427 | 18,992 | 11,128 |
| PowerPC | 0.751 | 0.721 | 18,992 | 11,128 |
| SPARC | 0.763 | 0.817 | 18,992 | 11,128 |
| IA-64 | 0.325 | 0.328 | 18,992 | 11,128 |

The async memory API similarly saves about 8 KiB across all six architectures;
the non-x86 cases allocate roughly 19,064 bytes before versus 11,200 bytes after
(SPARC's baseline median was 18,776). Their pooled elapsed medians range from
0.327 ms for IA-64 to 0.798 ms for SPARC. The async array path still allocates
roughly 26 KiB per drain because its non-cached integer results require tasks.
Full per-architecture/per-overload results and all seven samples are in
`results.json`; these short microbenchmarks do not establish a CPU speedup.

### Small-read overhead remains

Isolated x86, **16-byte requests**, again per 1 MiB:

| API | Baseline allocated bytes | Pooled allocated bytes | Baseline ms | Pooled ms |
| --- | ---: | ---: | ---: | ---: |
| Sync | 8,504 | 304 | 0.660 | 0.743 |
| Async array | 9,446,240 | 9,438,376 | 1.709 | 1.796 |
| Async memory | 9,056 | 1,192 | 3.437 | 3.436 |

The array API's approximately 9 MiB remains after pooling: each 16-byte result
is outside the runtime's small integer task cache and passes through multiple
`Task<int>` wrappers. By contrast, 1-byte results can use cached tasks. The memory
path has low allocations but substantial call/state-machine overhead for tiny
reads; low allocations do not imply it is faster than the array path.

### CPU and sampled allocation attribution

All **18 traces** contain one complete measured phase and report **zero lost
events**. The pooled memory-backed sync trace attributes 82.6% of workload leaf
samples to `Decoder.CodeFastCore`, 8.8% to CRC64, 4.4% to the fast matched-literal
helper, and 2.1% to x86 BCJ conversion. The corresponding baseline shares are
83.2%, 8.4%, 4.8%, and 2.0%: pooling does not move the main CPU bottleneck.

The pooled memory-backed async trace instead attributes 30.6% of workload leaf
samples to literal `DecodeNormalAsync`, 16.7% to `BitDecoder.DecodeAsync`, 12.6%
to async builder `Start`, and 11.3% to `Normalize2Async`. These per-symbol async
operations dominate despite the input being in memory. The corresponding
baseline distribution is similar. There is no optimized `CodeFastCore` hotspot
on this path.

The isolated pooled-filter traces attribute 88.4–94.5% of workload leaf samples
to the architecture's converter (x86 91.5%, ARM 93.6%, ARM-Thumb 92.4%, PowerPC
94.4%, SPARC 94.5%, IA-64 88.4%). Buffer copying accounts for roughly 4–8%.
These are instruction-heavy synthetic inputs; their rankings are not a
cross-architecture throughput prediction for real executables.

Baseline memory-backed XZ allocation ticks attribute approximately 24 MiB across
three measured extractions to dictionary arrays at `OutWindow.EnsureCapacity`.
The pooled memory-backed measured phases contain no sampled allocations at that
site; remaining async samples principally identify `Task<int>` results in the
read chain. All six pooled isolated-filter traces similarly contain no sampled
recurring BCJ-buffer allocations at `SetBaseStream`; their output buffers are
still allocated by the harness. Per-site sampling weights are approximate and
must not be substituted for the exact allocation table above.

File-backed traces still contain dictionary pool misses and suspended async
state-machine allocations. Most file-backed thread-time leaf samples are in
`Monitor.Enter_Slowpath`; these sampled stacks include waits/safepoints and do
**not** demonstrate that a lock consumes 98% of instruction CPU time. They do
not explain the adverse elapsed result by themselves, nor justify a lock rewrite.

## Remaining opportunities

- Async LZMA2 currently calls the per-symbol async decoder, whereas sync decoding
  can use the optimized range-decoder path. A future experiment should separate
  asynchronous input refills from CPU-only decoding. It must not replace async I/O
  with blocking reads, weaken cancellation, overread a chunk, or bypass corruption
  checks. This evaluation does not make that change.
- Small async array reads can allocate a `Task<int>` at multiple wrapper layers
  for a non-cached read result. Pooling an 8 KiB buffer cannot remove that cost.
  Pure delegation and a ValueTask-based internal read chain are separate candidates,
  with exception/cancellation semantics requiring regression coverage.
- Calling the memory overload on `XZStream` does not make its whole chain
  allocation-free: the stream still uses its array-based async implementation.
  The isolated BCJ memory path and complete XZ memory path must be measured
  separately rather than assuming the overload name guarantees equivalent costs.
- Checksum lookup tables are shared, not per-block scratch allocations. There is
  no evidence here to justify pooling them. Checksum work must remain enabled in
  future throughput comparisons.

## Follow-up: remove duplicate BCJ result tasks

On `perf/xz-async-decoding`, the first follow-up keeps the internal BCJ read as
a `ValueTask<int>` and creates a task only at the existing public array boundary.
Already-buffered reads complete without entering an async state machine. Custom
subclasses installing a synchronously throwing input stream retain the previous
faulted/canceled-task behavior; sync signatures are unchanged.

A fresh before/after comparison against the pooled baseline uses the same isolated
1 MiB x86 workload, 16-byte array reads, two warmups, seven measured drains,
Release .NET 10, and disabled tiered compilation. Median allocations fall from
**9,438,376 to 4,719,784 bytes**; median elapsed time is **1.695 to 0.963 ms**.
This is a small-read wrapper result, not a complete XZ throughput claim. Logs and
the follow-up harness are in the parent artifact directory as `fix1-before.log`,
`fix1-after.log`, and `fix1-harness/`.

All **728 targeted Release tests** pass, including allocation guards for all six
architectures and fault/cancellation regressions. All six library targets build,
and net48 test compilation passes. The remaining public `Task<int>` result cannot
be removed without changing that API; a native memory-based XZ chain remains a
separate follow-up.

## Artifacts and reproduction

The controlled comparison is isolated from preliminary sample-file measurements:

```text
/private/var/folders/6c/j56r6kqn5yj7q1_c6vm5c0t80000gn/T/opencode/xz-bcj-profile/evaluate/
```

This directory contains `prepare.py`, `run.py`, `trace.py`, the shared harness
source, separate baseline/pooled source exports and harness builds, library
hashes, the evaluated library diff, repository state, fixture generator and
hashes, raw timing logs, JSON results, traces, Speedscope files, and analyzer
source. Preparation and runs do not change the repository checkout.

```sh
python3 prepare.py
python3 run.py
python3 trace.py
```

`prepare.py` anchors its source comparison to `0aaf9b92`, builds both libraries
and harnesses, and creates the fixture. `run.py` performs the sequential untraced
comparison. `trace.py` separately rebuilds the harness/analyzer before collecting
traces; do not run it concurrently with measurements. Open `.speedscope.json`
files in <https://www.speedscope.app/>. Filenames identify version, filter/API,
input source, and read size. Raw Speedscope files include warmup; the analysis JSON
uses only the measured phase.

`XzBcjBenchmarks` provides repeatable repository benchmarks with built-in inputs:

```sh
dotnet run --project tests/SharpCompress.Performance -c Release -- --filter '*XzBcjBenchmarks*'
```

These BenchmarkDotNet cases are not the controlled before/after harness above.
They cover all six isolated filters and a built-in 1 MiB x86+LZMA2 XZ fixture, with
1-byte, 16-byte, 10,240-byte, and 1 MiB requests. Set
`SHARPCOMPRESS_XZ_PROFILE_FILE` to substitute a local `.xz` file for the XZ cases;
isolated-filter cases are unchanged.

## Validation

- The complete **.NET 10 Release suite passes: 3,571 tests, no failures or skips**.
- All **718 targeted XZ, branch-converter, LZMA, and Tar tests** pass on .NET 10.
- New lifetime tests verify oversized poisoned rents, exact-once return, reads
  after disposal, cancellation/read failures, early disposal, block completion,
  corrupt checksums, and caller-owned input preservation. Repeated filter
  construction has a bounded-allocation regression test.
- The issue #1471 `7z2605-linux-x64.tar.xz` sample matches liblzma byte-for-byte in
  all **24** combinations of sync/async overload, request size, and destination
  offset. All 6,958,080 decoded bytes and the XZ checksum match.
- Both isolated comparison builds decode the complete synthetic workload to the
  expected SHA-256 through sync and both async read overloads.
- All **84 repository benchmark cases** were smoke-tested for output lengths and
  XZ checks. These smoke runs are not the before/after timing measurements.
- All six library target frameworks build without warnings; net48 tests compile.
  net48 tests were not executed on macOS.
