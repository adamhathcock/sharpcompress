# Encrypted RAR allocation profiling

Baseline: `e61a335d` (the RAR5 HMAC allocation fix), compared with the buffered
decryption follow-up. Measurements were taken on macOS 27.0.1 ARM64 / .NET 10.0.13,
Release mode, with tiered compilation disabled. These are not Alpine results.

## Allocation-focused measurements

An isolated harness exercised `RarCryptoWrapper` over 1 MiB of AES-CBC ciphertext
with a fixed key (excluding PBKDF2), plus repeated `RarHeaderFactory` scans of
existing in-memory fixtures. Each workload was warmed up, then measured five
times. Tables show median elapsed time and median managed bytes allocated using
process-wide counters, including wrapper/reader construction and disposal but
excluding fixture/encryption/output-buffer setup. No other workloads ran during
the final measurements. Header scans reused a factory to exercise warm KDF caches.

| Workload | Before bytes | After bytes | Before ms | After ms |
| --- | ---: | ---: | ---: | ---: |
| Decrypt 1 MiB, sync, 1-byte reads | 5,244,392 | 1,416 | 7.972 | 4.010 |
| Decrypt 1 MiB, async, 1-byte reads | 7,865,832 | 1,416 | 15.281 | 8.147 |
| Decrypt 1 MiB, sync, 16-byte reads | 5,244,392 | 1,416 | 6.330 | 2.899 |
| Decrypt 1 MiB, async, 16-byte reads | 12,584,424 | 4,720,008 | 8.346 | 5.929 |
| Decrypt 1 MiB, sync, 8192-byte reads | 5,260,960 | 9,632 | 6.705 | 0.110 |
| Decrypt 1 MiB, async, 8192-byte reads | 5,275,296 | 18,512 | 8.132 | 0.138 |
| RAR5 encrypted headers, sync, 100 scans | 2,425,456 | 1,403,056 | 1.331 | 1.169 |
| RAR5 encrypted headers, async, 100 scans | 2,552,656 | 1,530,256 | 3.250 | 2.983 |
| RAR4 encrypted headers, sync, 1 scan | 34,908,464 | 34,903,512 | 98.269 | 105.439 |
| RAR4 encrypted headers, async, 1 scan | 34,909,488 | 34,904,768 | 97.998 | 106.596 |
| Unencrypted RAR5 headers, sync, 100 scans | 518,608 | 518,608 | 0.217 | 0.236 |
| Unencrypted RAR5 headers, async, 100 scans | 597,808 | 597,808 | 2.289 | 2.340 |

Async decryption here uses the `Task<int>` array overload. Returning a non-cached
integer such as 16 still allocates a Task per call; the `Memory<byte>` overload
uses `ValueTask<int>` and avoids that conversion. This distinction matters when
interpreting the remaining allocations.

## Changes and remaining opportunities

- Transform AES blocks in place rather than allocating two arrays per block.
- Batch file-data transformations in a reusable buffer bounded to 8 KiB; start
  with 16 bytes to avoid imposing the larger allocation on tiny entries.
- Remove plaintext queues, per-byte header-result arrays, per-block ciphertext
  arrays, and the pooled-copy adapter for `Memory<byte>` reads.
- Dispose owned crypto transforms and per-header readers deterministically.
  Native memory retention was not quantified by these managed allocation counters.
- Leave the RAR3/4 KDF unchanged: its large working array dominates the roughly
  35 MB per encrypted-header scan. The measured legacy workloads were slightly
  slower, not faster. Any KDF rewrite needs separate correctness/performance work.
- Primitive-read arrays and fixed KDF/cache setup costs remain secondary targets.

## Repeatable repository benchmarks

`RarEncryptedBenchmarks` provides warm/cold header parsing, async warm parsing,
and sync/async archive extraction, with encrypted-header, encrypted-data-only,
and unencrypted fixtures. Load fixture bytes outside measured iterations.

```sh
dotnet run --project tests/SharpCompress.Performance -c Release -- --filter '*RarEncryptedBenchmarks*'
```

All 15 benchmark cases were smoke-tested with one warmup and one measured
invocation; those smoke-test timings are not statistically meaningful and are
not the before/after measurements above. Dedicated regression tests cover
bounded allocations, CRC/read counts, block boundaries, large and fragmented
reads, span/memory overloads, stream ownership, and transform disposal.
