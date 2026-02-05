```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host] : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Dry    : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

UnrollFactor=1  WarmupCount=1  

```
| Method                                   | Job        | Toolchain              | InvocationCount | IterationCount | LaunchCount | RunStrategy | Mean      | Error      | StdDev    | Median     | Allocated  |
|----------------------------------------- |----------- |----------------------- |---------------- |--------------- |------------ |------------ |----------:|-----------:|----------:|-----------:|-----------:|
| &#39;Zip: Extract all entries (Archive API)&#39; | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     |  5.829 ms |  69.397 ms | 3.8039 ms |  7.3861 ms |  186.55 KB |
| &#39;Zip: Extract all entries (Reader API)&#39;  | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     |  4.978 ms | 114.015 ms | 6.2496 ms |  1.4033 ms |  128.05 KB |
| &#39;Zip: Create archive with small files&#39;   | Job-QHCVAS | InProcessEmitToolchain | 1               | 3              | Default     | Default     |  1.175 ms |  11.325 ms | 0.6207 ms |  0.8635 ms | 2812.43 KB |
| &#39;Zip: Extract all entries (Archive API)&#39; | Dry        | Default                | Default         | 1              | 1           | ColdStart   | 43.781 ms |         NA | 0.0000 ms | 43.7807 ms |  182.66 KB |
| &#39;Zip: Extract all entries (Reader API)&#39;  | Dry        | Default                | Default         | 1              | 1           | ColdStart   | 38.885 ms |         NA | 0.0000 ms | 38.8848 ms |  123.88 KB |
| &#39;Zip: Create archive with small files&#39;   | Dry        | Default                | Default         | 1              | 1           | ColdStart   | 23.153 ms |         NA | 0.0000 ms | 23.1528 ms | 2808.54 KB |
