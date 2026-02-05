```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host] : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Dry    : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

UnrollFactor=1  WarmupCount=1  

```
| Method                                   | Job        | Toolchain              | InvocationCount | IterationCount | LaunchCount | RunStrategy | Mean        | Error    | StdDev   | Allocated |
|----------------------------------------- |----------- |----------------------- |---------------- |--------------- |------------ |------------ |------------:|---------:|---------:|----------:|
| &#39;Tar: Extract all entries (Archive API)&#39; | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     |    128.4 μs | 452.3 μs | 24.79 μs |  20.73 KB |
| &#39;Tar: Extract all entries (Reader API)&#39;  | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     |    286.7 μs | 390.3 μs | 21.40 μs | 217.47 KB |
| &#39;Tar.GZip: Extract all entries&#39;          | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     |          NA |       NA |       NA |        NA |
| &#39;Tar: Create archive with small files&#39;   | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     |    117.9 μs | 485.1 μs | 26.59 μs |  72.55 KB |
| &#39;Tar: Extract all entries (Archive API)&#39; | Dry        | Default                | Default         | 1              | 1           | ColdStart   | 16,228.5 μs |       NA |  0.00 μs |  16.84 KB |
| &#39;Tar: Extract all entries (Reader API)&#39;  | Dry        | Default                | Default         | 1              | 1           | ColdStart   | 24,383.0 μs |       NA |  0.00 μs | 213.58 KB |
| &#39;Tar.GZip: Extract all entries&#39;          | Dry        | Default                | Default         | 1              | 1           | ColdStart   |          NA |       NA |       NA |        NA |
| &#39;Tar: Create archive with small files&#39;   | Dry        | Default                | Default         | 1              | 1           | ColdStart   |  8,466.1 μs |       NA |  0.00 μs |  68.38 KB |

Benchmarks with issues:
  TarBenchmarks.'Tar.GZip: Extract all entries': Job-QHCVAS(Toolchain=InProcessEmitToolchain, InvocationCount=1, IterationCount=3, UnrollFactor=1, WarmupCount=1)
  TarBenchmarks.'Tar.GZip: Extract all entries': Dry(IterationCount=1, LaunchCount=1, RunStrategy=ColdStart, UnrollFactor=1, WarmupCount=1)
