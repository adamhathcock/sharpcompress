```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host] : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Toolchain=InProcessEmitToolchain  InvocationCount=1  IterationCount=3  
UnrollFactor=1  WarmupCount=1  

```
| Method                   | Mean       | Error      | StdDev    | Allocated |
|------------------------- |-----------:|-----------:|----------:|----------:|
| &#39;GZip: Compress 100KB&#39;   | 6,090.9 μs | 1,940.6 μs | 106.37 μs | 523.37 KB |
| &#39;GZip: Decompress 100KB&#39; |   434.5 μs |   389.3 μs |  21.34 μs |  37.41 KB |
```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host] : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Toolchain=InProcessEmitToolchain  InvocationCount=1  IterationCount=3  
UnrollFactor=1  WarmupCount=1  

```
| Method                                   | Mean     | Error     | StdDev    | Allocated |
|----------------------------------------- |---------:|----------:|----------:|----------:|
| &#39;Rar: Extract all entries (Archive API)&#39; | 2.070 ms | 2.4938 ms | 0.1367 ms |  95.77 KB |
| &#39;Rar: Extract all entries (Reader API)&#39;  | 2.359 ms | 0.9123 ms | 0.0500 ms | 154.18 KB |
```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host] : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Toolchain=InProcessEmitToolchain  InvocationCount=1  IterationCount=3  
UnrollFactor=1  WarmupCount=1  

```
| Method                            | Mean      | Error      | StdDev    | Allocated |
|---------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;7Zip LZMA: Extract all entries&#39;  | 14.042 ms | 98.7800 ms | 5.4145 ms | 277.93 KB |
| &#39;7Zip LZMA2: Extract all entries&#39; |  8.654 ms |  0.6124 ms | 0.0336 ms | 277.71 KB |
```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host] : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Toolchain=InProcessEmitToolchain  InvocationCount=1  IterationCount=3  
UnrollFactor=1  WarmupCount=1  

```
| Method                                   | Mean     | Error    | StdDev   | Allocated |
|----------------------------------------- |---------:|---------:|---------:|----------:|
| &#39;Tar: Extract all entries (Archive API)&#39; | 148.0 μs | 356.4 μs | 19.53 μs |  20.73 KB |
| &#39;Tar: Extract all entries (Reader API)&#39;  | 259.5 μs | 347.1 μs | 19.02 μs | 217.47 KB |
| &#39;Tar.GZip: Extract all entries&#39;          |       NA |       NA |       NA |        NA |
| &#39;Tar: Create archive with small files&#39;   | 139.1 μs | 728.8 μs | 39.95 μs |  72.55 KB |

Benchmarks with issues:
  TarBenchmarks.'Tar.GZip: Extract all entries': Job-NHXEIE(Toolchain=InProcessEmitToolchain, InvocationCount=1, IterationCount=3, UnrollFactor=1, WarmupCount=1)
```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host] : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Toolchain=InProcessEmitToolchain  InvocationCount=1  IterationCount=3  
UnrollFactor=1  WarmupCount=1  

```
| Method                                   | Mean     | Error      | StdDev    | Median    | Allocated  |
|----------------------------------------- |---------:|-----------:|----------:|----------:|-----------:|
| &#39;Zip: Extract all entries (Archive API)&#39; | 5.629 ms |  66.953 ms | 3.6699 ms | 7.2353 ms |  186.55 KB |
| &#39;Zip: Extract all entries (Reader API)&#39;  | 4.935 ms | 114.613 ms | 6.2823 ms | 1.3354 ms |  128.05 KB |
| &#39;Zip: Create archive with small files&#39;   | 1.250 ms |  10.341 ms | 0.5668 ms | 0.9229 ms | 2812.43 KB |
