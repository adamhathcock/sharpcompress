# RAR pooling and async checksum evaluation

## Branch and changes

Evaluated on 2026-10-19 against `78006f94` on `adam/release-to-master`.
The comparison uses a clean build of that commit and a separate build containing
the working-tree RAR changes, not timings carried over from another branch.
The branch already contains the async unpacker-disposal fix.

The RAR changes were subsequently ported to `perf/rar-profile-pooling`, based
directly on `master` at `0fbf36d6`, without the unrelated commits from
`adam/release-to-master`. Measurements below remain explicitly tied to the
profiled `78006f94` baseline; they are not a new profile of the master-based port.

Changes:

- Async RAR Archive entry streams now select CRC32 or BLAKE2sp verification
  using the same rules as the synchronous path. Failed initialization disposes
  the newly constructed stream before propagating the exception.
- Async RAR5 and RAR2 Huffman-table scratch buffers are lazily rented, reused
  for the unpacker's lifetime, and returned on disposal. Sync stack buffers
  are unchanged.
- RAR5 zero-run decoding is bounded by the alphabet size, not a rented array's
  capacity. RAR2 table-history padding is cleared to preserve the previous
  zero-initialization behavior.
- CRC32 lookup tables were already cached and shared. They are intentionally
  not pooled: there is no per-block allocation to eliminate, and returning a
  table still referenced by checksum streams would be unsafe.

## Workload and measurement

The existing synthetic RAR5 workload is reused: **102.50 MiB compressed**
(107,473,921 bytes), **200 MiB extracted**, four 50 MiB files alternating
64 KiB of seeded random data and 64 KiB of structured records. RAR 7.22 created
it with normal compression, an 8 MiB dictionary, CRC32, no solid compression,
no encryption, and one volume:

```sh
rar a -cfg- -m3 -md8m -mt1 -s- -ep -htc -qo- -idq mixed-100mb.rar \
  input/mixed-0.bin input/mixed-1.bin input/mixed-2.bin input/mixed-3.bin
```

Archive SHA-256:
`f6dcb78eaffa527f9394f47fc869ee396a180f7cbe6e4eb1430a60fe8b925fdd`.

Release `net10.0` runs use the same harness and archive for both builds.
Each scenario uses a fresh process, two warmups and seven measured extractions.
Version order is reversed between the Archive and Reader scenarios. Output is
discarded; Archive drains verify extracted byte counts and Reader drains use
the normal entry-writing path. Caller-owned streams set `LeaveStreamOpen = true`.

Memory-backed runs preload compressed input outside the measured region.
File-backed runs use a 4 KiB FileStream buffer, `SequentialScan`, and
`FileOptions.Asynchronous`; their timings include warm filesystem-cache effects,
not cold-disk performance. No builds or test runs compete with profiling.

Managed allocations are exact `GC.GetTotalAllocatedBytes(true)` deltas around
extraction and disposal. They exclude input preloading and between-iteration
logging but include a 64 KiB caller buffer, unchanged between versions and
unused in Reader scenarios. They are allocations, not live-memory measurements.
The decoder still needs its pooled 8 MiB dictionary and may rent a 4 MiB temporary
output buffer; low warm allocations do not mean those buffers disappeared.

Separate traces perform ten measured extractions after warmup, using
`dotnet-trace` 10.0.750501 with `dotnet-sampled-thread-time` and GC allocation
ticks. The analyzer uses TraceEvent 3.1.21. CPU stack shares are sampled thread
time on macOS, not exact instruction CPU time. Inlining can attribute helper
work to the decoder loop; file-backed stacks can include waits/safepoints.

Archive async now performs checksum work absent from its baseline. Its timing
is therefore not an equivalent-work comparison; Reader async already checked
checksums and better isolates the pooling change. This experiment does not
justify claims about all RAR files, dictionary sizes, encryption, solid archives,
or cold storage.

The recorded runtime is .NET 10.0.13 on the same Apple M4/macOS ARM64 host used
for the earlier evaluation.

## Fresh results

Medians per extraction of the complete 200 MiB payload:

| Input / async API | Baseline elapsed | Updated elapsed | Baseline allocations | Updated allocations |
| --- | ---: | ---: | ---: | ---: |
| Memory / Archive | 702 ms | 755 ms | 5.09 MiB | 1.96 MiB |
| Memory / Reader | 726 ms | 764 ms | 4.64 MiB | 1.51 MiB |
| File / Archive | 716 ms | 800 ms | 8.37 MiB | 5.25 MiB |
| File / Reader | 805 ms | 770 ms | 11.44 MiB | 4.34 MiB |

The memory-backed results save approximately **3.13 MiB per extraction**,
reducing warm allocations by approximately **62% for Archive** and **67% for
Reader**. File-backed allocations include variable async suspension and pool
misses. In particular, the file Reader baseline is higher than in the previous
branch's run; do not attribute its entire allocation difference to scratch
pooling. The memory-backed comparison better isolates that change.

This is not evidence of a CPU speedup. Archive now does additional checksum
work, and Reader memory-backed median process CPU was 734 ms before and 770 ms
after in this run. Timings vary with JIT code generation and machine state;
these short sequential measurements demonstrate allocation savings, not a
general throughput improvement.

All six traces report zero lost events. Baseline allocation samples attribute
approximately 3.0–3.1 MiB per extraction to `ReadTablesAsync` arrays; the updated
measured phases contain **no sampled recurring allocations at those sites**.
The exact total-allocation deltas above are more precise than the sampled
per-type attribution.

The updated memory-backed Archive trace attributes approximately 83.8% of leaf
samples to the RAR5 decoder loop, 10.2% to `Crc32Stream.CalculateCrc`, and 3.4% to
`MakeDecodeTables`. The baseline Archive trace has no bulk CRC hotspot, confirming
that checksum work is now present. Remaining memory-backed allocations are
primarily `Task<int>` results; file-backed reads also allocate suspended async
state machines and task adapters. Those read-chain allocations are not changed
by scratch pooling.

## Artifacts and reproduction

Fresh artifacts are isolated from previous branch evaluations:

```text
/private/var/folders/6c/j56r6kqn5yj7q1_c6vm5c0t80000gn/T/opencode/rar-100mb-profile/evaluation-78006f94/
```

This contains the baseline source export, separate `head-harness/` and
`pooled-harness/` builds, library hashes, repository-state snapshot, RAR diff,
untraced logs, raw `.nettrace` files, `.speedscope.json` files, and analysis JSON.
Open the Speedscope files in <https://www.speedscope.app/>. Filenames identify
version, API, and input source. The original archive and harness are in the
parent directory.

From that parent directory, `prepare-evaluation.py` builds the current HEAD
baseline and working-tree harnesses without changing the repository, and
`evaluate-current.py evaluation-78006f94` runs the comparison. The preparation
step assumes the updated library has already been built in Release for net10.0.

## Validation

- All **544 targeted RAR tests** pass on .NET 10 Release, including CRC32 and
  BLAKE2sp mismatch detection, both async read overloads, and entry-copy APIs.
- New scratch-buffer tests cover reuse with poisoned buffers, solid entries,
  oversized rents, RAR2 history padding, cancellation/checksum-failure cleanup,
  repeated disposal, and non-seekable input ownership.
- The complete .NET 10 Release run on the profiling branch reported **2,995
  passed, no failures**. Validation of the master-based port is recorded below.
- All six library targets build; net48 test compilation succeeds. net48 tests
  were not executed on macOS.
- CSharpier formatting/check and `git diff --check` pass. The RAR source diff
  matches the saved profiling snapshot.

The master-based port also passes all 544 targeted RAR tests, all six library
builds, net48 test compilation, and the CSharpier formatting check. Its complete
.NET 10 Release run reports **2,983 passed, no failures**.
