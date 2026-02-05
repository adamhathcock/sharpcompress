| Method                   | Mean       | Error     | StdDev    | Allocated |
|------------------------- |-----------:|----------:|----------:|----------:|
| &#39;GZip: Compress 100KB&#39;   | 2,422.9 μs | 826.68 μs | 546.80 μs |  519.2 KB |
| &#39;GZip: Decompress 100KB&#39; |   322.9 μs |  23.40 μs |  13.93 μs |  34.15 KB |
| Method                                   | Mean       | Error    | StdDev   | Allocated |
|----------------------------------------- |-----------:|---------:|---------:|----------:|
| &#39;Rar: Extract all entries (Archive API)&#39; |   883.7 μs | 24.98 μs | 16.52 μs |  90.83 KB |
| &#39;Rar: Extract all entries (Reader API)&#39;  | 1,138.8 μs | 22.88 μs | 15.13 μs | 149.43 KB |
| Method                            | Mean     | Error     | StdDev    | Allocated |
|---------------------------------- |---------:|----------:|----------:|----------:|
| &#39;7Zip LZMA: Extract all entries&#39;  | 5.297 ms | 0.6199 ms | 0.3689 ms |  272.7 KB |
| &#39;7Zip LZMA2: Extract all entries&#39; | 5.122 ms | 0.1581 ms | 0.1046 ms | 272.52 KB |
| Method                                   | Mean     | Error    | StdDev   | Allocated |
|----------------------------------------- |---------:|---------:|---------:|----------:|
| &#39;Tar: Extract all entries (Archive API)&#39; | 24.49 μs | 1.461 μs | 0.967 μs |  16.58 KB |
| &#39;Tar: Extract all entries (Reader API)&#39;  | 97.18 μs | 3.243 μs | 2.145 μs | 213.31 KB |
| &#39;Tar.GZip: Extract all entries&#39;          |       NA |       NA |       NA |        NA |
| &#39;Tar: Create archive with small files&#39;   | 23.65 μs | 1.263 μs | 0.752 μs |  68.18 KB |
| Method                                   | Mean     | Error    | StdDev   | Gen0     | Gen1     | Allocated  |
|----------------------------------------- |---------:|---------:|---------:|---------:|---------:|-----------:|
| &#39;Zip: Extract all entries (Archive API)&#39; | 541.1 μs | 35.05 μs | 23.18 μs |        - |        - |  181.57 KB |
| &#39;Zip: Extract all entries (Reader API)&#39;  | 515.6 μs | 36.03 μs | 21.44 μs |        - |        - |  122.72 KB |
| &#39;Zip: Create archive with small files&#39;   | 190.0 μs | 17.55 μs | 11.61 μs | 300.0000 | 100.0000 | 2806.51 KB |