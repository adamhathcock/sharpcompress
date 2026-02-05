```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host] : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Dry    : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

UnrollFactor=1  WarmupCount=1  

```
| Method                   | Job        | Toolchain              | InvocationCount | IterationCount | LaunchCount | RunStrategy | Mean       | Error      | StdDev    | Allocated |
|------------------------- |----------- |----------------------- |---------------- |--------------- |------------ |------------ |-----------:|-----------:|----------:|----------:|
| &#39;GZip: Compress 100KB&#39;   | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     | 6,080.9 μs | 1,949.7 μs | 106.87 μs | 523.98 KB |
| &#39;GZip: Decompress 100KB&#39; | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     |   428.4 μs |   364.3 μs |  19.97 μs |  37.74 KB |
| &#39;GZip: Compress 100KB&#39;   | Dry        | Default                | Default         | 1              | 1           | ColdStart   | 6,951.5 μs |         NA |   0.00 μs | 521.07 KB |
| &#39;GZip: Decompress 100KB&#39; | Dry        | Default                | Default         | 1              | 1           | ColdStart   | 6,179.5 μs |         NA |   0.00 μs |  34.51 KB |
