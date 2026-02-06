| Method                   | Mean       | Error     | StdDev    | Allocated |
|------------------------- |-----------:|----------:|----------:|----------:|
| &#39;GZip: Compress 100KB&#39;   | 2,722.9 μs | 724.15 μs | 478.98 μs |  519.2 KB |
| &#39;GZip: Decompress 100KB&#39; |   327.0 μs |   9.58 μs |   5.01 μs |  34.15 KB |
| Method                                   | Mean       | Error    | StdDev   | Allocated |
|----------------------------------------- |-----------:|---------:|---------:|----------:|
| &#39;Rar: Extract all entries (Archive API)&#39; |   880.0 μs | 16.06 μs |  9.55 μs |     91 KB |
| &#39;Rar: Extract all entries (Reader API)&#39;  | 1,143.5 μs | 39.77 μs | 26.30 μs | 149.28 KB |
| Method                            | Mean     | Error     | StdDev    | Allocated |
|---------------------------------- |---------:|----------:|----------:|----------:|
| &#39;7Zip LZMA: Extract all entries&#39;  | 6.237 ms | 0.7392 ms | 0.3866 ms | 272.78 KB |
| &#39;7Zip LZMA2: Extract all entries&#39; | 5.352 ms | 0.2780 ms | 0.1839 ms | 272.46 KB |
| Method                                   | Mean     | Error    | StdDev   | Allocated |
|----------------------------------------- |---------:|---------:|---------:|----------:|
| &#39;Tar: Extract all entries (Archive API)&#39; | 20.82 μs | 2.958 μs | 1.957 μs |  16.52 KB |
| &#39;Tar: Extract all entries (Reader API)&#39;  | 91.98 μs | 5.306 μs | 3.158 μs |  213.1 KB |
| &#39;Tar.GZip: Extract all entries&#39;          |       NA |       NA |       NA |        NA |
| &#39;Tar: Create archive with small files&#39;   | 24.89 μs | 2.117 μs | 1.260 μs |  68.31 KB |
| Method                                   | Mean     | Error    | StdDev   | Gen0     | Gen1     | Allocated  |
|----------------------------------------- |---------:|---------:|---------:|---------:|---------:|-----------:|
| &#39;Zip: Extract all entries (Archive API)&#39; | 518.5 μs | 24.19 μs | 14.40 μs |        - |        - |   181.6 KB |
| &#39;Zip: Extract all entries (Reader API)&#39;  | 465.8 μs | 18.97 μs | 11.29 μs |        - |        - |   123.1 KB |
| &#39;Zip: Create archive with small files&#39;   | 197.3 μs | 13.78 μs |  9.11 μs | 300.0000 | 100.0000 | 2806.87 KB |