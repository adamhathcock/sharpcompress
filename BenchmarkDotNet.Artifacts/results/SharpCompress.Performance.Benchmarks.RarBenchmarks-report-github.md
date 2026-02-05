```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host] : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Dry    : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

UnrollFactor=1  WarmupCount=1  

```
| Method                                   | Job        | Toolchain              | InvocationCount | IterationCount | LaunchCount | RunStrategy | Mean      | Error    | StdDev    | Allocated |
|----------------------------------------- |----------- |----------------------- |---------------- |--------------- |------------ |------------ |----------:|---------:|----------:|----------:|
| &#39;Rar: Extract all entries (Archive API)&#39; | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     |  1.986 ms | 1.050 ms | 0.0576 ms |  95.77 KB |
| &#39;Rar: Extract all entries (Reader API)&#39;  | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     |  2.325 ms | 1.576 ms | 0.0864 ms | 154.18 KB |
| &#39;Rar: Extract all entries (Archive API)&#39; | Dry        | Default                | Default         | 1              | 1           | ColdStart   | 44.016 ms |       NA | 0.0000 ms |  91.88 KB |
| &#39;Rar: Extract all entries (Reader API)&#39;  | Dry        | Default                | Default         | 1              | 1           | ColdStart   | 48.894 ms |       NA | 0.0000 ms | 150.29 KB |
