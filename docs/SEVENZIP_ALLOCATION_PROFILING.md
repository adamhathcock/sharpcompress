# 7z writer allocation follow-up

## Scope

Compared Release libraries from `fa96277b` (PR #1476) with the allocation fixes
on 2026-10-26. The follow-up changes three sites:

- `AesEncoderStream.WriteByte` writes into the existing pending CBC block rather
  than inheriting `Stream.WriteByte`'s per-byte array allocation. The synchronous
  LZMA range encoder emits every compressed byte through this API; encrypted
  LZMA headers use it too. LZMA2 uses it for framing bytes, not every payload byte.
- The encoder's 4 KiB ciphertext scratch is pooled and returned once on disposal,
  including after failed or canceled output. Crypto initialization precedes renting;
  a failed rent disposes the transform. Encryption batches use the logical 4 KiB
  bound even if a rent is oversized or not AES-block-aligned. The private pending
  plaintext block is cleared on disposal; keys and retained coder properties are
  not pooled.
- The 7z compression-type lookup uses an indexed list scan instead of predicate
  LINQ, avoiding enumerator boxing on .NET 8. The public property still caches
  its result, so this saving applies once per entry, not every property access.

## Measurement

The comparison harness runs against matching `net8.0` and `net10.0` libraries in
separate processes, with tiered compilation disabled, two warmups and five
measured operations per scenario. .NET 8 runs before/after; .NET 10 reverses the
order. Library SHA-256 hashes are checked against the actual loaded assemblies.
No builds or tests run alongside the measurements.

Allocation deltas use process-wide `GC.GetTotalAllocatedBytes(true)` around the
operation, including construction and disposal. Inputs and compiled reflection
delegates are created outside measurement. Each sample includes the harness's
40-byte stopwatch allocation. Results are medians, not peak memory. Pool warmth
is intentional: a cold pool still allocates a scratch array on its first miss.

Isolated workloads:

- 1 MiB of `WriteByte` calls through an AES encoder to `Stream.Null`.
- 1,000 AES encoder construction/disposal cycles without writes.
- 100,000 uncached compression-type lookups against an AES+LZMA2 folder. A compiled
  delegate calls the actual private method without reflection invocation costs or
  result boxing. This isolates lookup cost rather than measuring archive parsing.

End-to-end workloads cover all 24 combinations of LZMA/LZMA2, sync/async,
unencrypted/data-encrypted/data-and-header-encrypted, and a single 256 KiB random
file versus 100 files of 128 bytes. Data is seeded with `1476`; dictionaries are
4 KiB. Each operation creates and finalizes a writer, including key derivation,
metadata and a preallocated 1 MiB output stream. The first warmup additionally
reopens the archive and checks every entry byte-for-byte, outside measurement.
These memory-backed async operations can complete synchronously; they do not
represent suspended file I/O or establish file-backed throughput improvements.

The repository's `SevenZipEncryptionBenchmarks` provides the same workload matrix
for ongoing BenchmarkDotNet runs, with filenames/options prebuilt in setup:

```sh
dotnet run --project tests/SharpCompress.Performance -c Release -- --filter '*SevenZipEncryptionBenchmarks*'
```

The controlled harness (which also includes per-archive filename/options creation),
before/after libraries, loaded-library hashes, all raw samples, and runner are in:

```text
/private/var/folders/6c/j56r6kqn5yj7q1_c6vm5c0t80000gn/T/opencode/sevenzip-allocations/
```

`Profile.csproj` / `Program.cs` build the harness; `run.py` creates isolated run
directories, selects the saved libraries, runs the comparisons sequentially, and
checks the hashes. Results are `before-net8.0.json`, `after-net8.0.json`,
`before-net10.0.json`, and `after-net10.0.json`; `hashes.json` records library identity.

## Results

Runtime versions were .NET 8.0.17 and .NET 10.0.13 on macOS ARM64.
Allocated bytes per operation, including the 40-byte measurement overhead:

| Isolated workload | .NET 8 before | .NET 8 after | .NET 10 before | .NET 10 after |
| --- | ---: | ---: | ---: | ---: |
| AES byte writes, 1 MiB | 33,559,288 | 536 | 33,559,352 | 584 |
| AES construction/disposal, 1,000 encoders | 4,608,040 | 496,040 | 4,656,264 | 544,040 |
| Uncached codec lookup, 100,000 calls | 4,000,040 | 40 | 40 | 40 |

The byte-write fix eliminates the input-sized stream of tiny arrays. Pooling saves
approximately 4,112 bytes per encoder lifetime (the new pool-reference field adds
8 bytes to the stream object). The old lookup boxes a 40-byte list enumerator on
.NET 8; .NET 10 already has a span-based LINQ fast path, so its allocation total
is unchanged. No assertion here treats the harness's allocation floor as library
allocation.

Representative complete writer allocations on .NET 10:

| Workload | Before bytes | After bytes |
| --- | ---: | ---: |
| LZMA, sync, 256 KiB, encrypted data | 9,670,408 | 1,163,128 |
| LZMA, sync, 256 KiB, encrypted data and header | 9,675,272 | 1,160,456 |
| LZMA, sync, 100 small files, encrypted data and header | 7,538,176 | 6,575,344 |
| LZMA, async, 100 small files, encrypted data and header | 6,990,992 | 6,575,456 |
| LZMA2, sync, 100 small files, encrypted data and header | 7,149,632 | 6,645,168 |
| LZMA2, async, 100 small files, encrypted data and header | 7,060,592 | 6,645,056 |

All 16 encrypted workload medians allocate less on both runtimes. The eight
unencrypted .NET 10 cases are byte-for-byte unchanged in allocation totals. Six
of the eight .NET 8 unencrypted cases are unchanged; the two async large-file
cases differ by +536 bytes, consistent with small process-wide pool/runtime noise,
not a newly introduced unencrypted buffer. All 24 workloads round-trip correctly
with both libraries on each runtime.

These are allocation results, not a general throughput claim. Isolated .NET 10
byte writes improve from 13.406 to 4.452 ms, but complete archive timings include
SHA-256 key derivation, compression and machine-state effects. For example, the
.NET 8 synchronous encrypted LZMA small-file case regressed from 40.430 to
82.145 ms in this run even while allocations fell. That adverse result is retained
in the raw logs; file-backed profiling and additional repeated timing runs would
be needed before claiming end-to-end speedups. Unencrypted synchronous LZMA's
existing byte-write costs in downstream wrappers are outside these three fixes.

## Regression coverage

New tests cover allocation bounds for byte writes, repeated encoder construction,
and uncached codec lookup; mixed byte/array/async writes against independent AES-CBC
output; aligned/unaligned/empty inputs; oversized poisoned leases; sync and async
disposal; early disposal without final writes; failed crypto initialization;
output errors/cancellation; caller-owned stream preservation; and rejection of
post-disposal writes/finalization. Existing encrypted writer round-trips now also
cover async LZMA with and without encrypted headers.

All six library target frameworks and the benchmark project build without
warnings. The net48 test project compiles; net48 tests are not executed on macOS.
The complete .NET 10 Release test suite passes: **3,766 tests, no failures or skips**.
All 24 repository benchmark cases were also smoke-tested by calling their setup
and sync/async methods directly and checking that finalization produced archive
output. Those smoke checks are not timing measurements.
