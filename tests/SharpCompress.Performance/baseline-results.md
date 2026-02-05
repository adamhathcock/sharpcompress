| Method                   | Mean       | Error     | StdDev    | Allocated |
|------------------------- |-----------:|----------:|----------:|----------:|
| &#39;GZip: Compress 100KB&#39;   | 2,512.0 μs | 715.08 μs | 472.98 μs |  519.2 KB |
| &#39;GZip: Decompress 100KB&#39; |   330.1 μs |  38.39 μs |  22.84 μs |  34.15 KB |
| Method                                   | Mean       | Error    | StdDev   | Allocated |
|----------------------------------------- |-----------:|---------:|---------:|----------:|
| &#39;Rar: Extract all entries (Archive API)&#39; |   853.8 μs | 29.23 μs | 19.33 μs |  90.57 KB |
| &#39;Rar: Extract all entries (Reader API)&#39;  | 1,195.2 μs |  7.20 μs |  4.76 μs | 149.43 KB |
| Method                            | Mean     | Error     | StdDev    | Allocated |
|---------------------------------- |---------:|----------:|----------:|----------:|
| &#39;7Zip LZMA: Extract all entries&#39;  | 5.408 ms | 0.2094 ms | 0.1095 ms | 272.74 KB |
| &#39;7Zip LZMA2: Extract all entries&#39; | 4.860 ms | 0.0862 ms | 0.0451 ms | 272.49 KB |
| Method                                   | Mean     | Error    | StdDev   | Allocated |
|----------------------------------------- |---------:|---------:|---------:|----------:|
| &#39;Tar: Extract all entries (Archive API)&#39; | 24.03 μs | 1.889 μs | 1.124 μs |  16.51 KB |
| &#39;Tar: Extract all entries (Reader API)&#39;  | 89.60 μs | 2.320 μs | 1.381 μs | 213.31 KB |
| &#39;Tar.GZip: Extract all entries&#39;          |       NA |       NA |       NA |        NA |
| &#39;Tar: Create archive with small files&#39;   | 23.47 μs | 2.351 μs | 1.555 μs |  68.61 KB |
| Method                                   | Mean     | Error    | StdDev  | Gen0     | Gen1     | Allocated  |
|----------------------------------------- |---------:|---------:|--------:|---------:|---------:|-----------:|
| &#39;Zip: Extract all entries (Archive API)&#39; | 500.7 μs | 12.01 μs | 7.15 μs |        - |        - |  181.21 KB |
| &#39;Zip: Extract all entries (Reader API)&#39;  | 489.9 μs | 11.39 μs | 7.54 μs |        - |        - |  123.15 KB |
| &#39;Zip: Create archive with small files&#39;   | 208.3 μs | 16.03 μs | 9.54 μs | 300.0000 | 100.0000 | 2806.41 KB |