```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host] : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Dry    : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

UnrollFactor=1  WarmupCount=1  

```
| Method                            | Job        | Toolchain              | InvocationCount | IterationCount | LaunchCount | RunStrategy | Mean     | Error     | StdDev   | Allocated |
|---------------------------------- |----------- |----------------------- |---------------- |--------------- |------------ |------------ |---------:|----------:|---------:|----------:|
| &#39;7Zip LZMA: Extract all entries&#39;  | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     | 10.76 ms | 14.511 ms | 0.795 ms | 277.93 KB |
| &#39;7Zip LZMA2: Extract all entries&#39; | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     | 10.11 ms |  1.960 ms | 0.107 ms | 277.71 KB |
| &#39;7Zip LZMA: Extract all entries&#39;  | Dry        | Default                | Default         | 1              | 1           | ColdStart   | 51.59 ms |        NA | 0.000 ms | 274.04 KB |
| &#39;7Zip LZMA2: Extract all entries&#39; | Dry        | Default                | Default         | 1              | 1           | ColdStart   | 51.68 ms |        NA | 0.000 ms | 273.82 KB |
