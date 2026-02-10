| Method                   | Mean       | Error     | StdDev    | Allocated |
|------------------------- |-----------:|----------:|----------:|----------:|
| &#39;GZip: Compress 100KB&#39;   | 2,358.2 μs | 792.96 μs | 524.49 μs |  519.2 KB |
| &#39;GZip: Decompress 100KB&#39; |   303.4 μs |   5.93 μs |   3.53 μs |  34.15 KB |
| Method                                   | Mean       | Error    | StdDev   | Allocated |
|----------------------------------------- |-----------:|---------:|---------:|----------:|
| &#39;Rar: Extract all entries (Archive API)&#39; |   827.5 μs | 13.75 μs |  9.10 μs |  91.09 KB |
| &#39;Rar: Extract all entries (Reader API)&#39;  | 1,083.1 μs | 41.82 μs | 27.66 μs | 149.55 KB |
| Method                            | Mean     | Error     | StdDev    | Allocated |
|---------------------------------- |---------:|----------:|----------:|----------:|
| &#39;7Zip LZMA: Extract all entries&#39;  | 5.227 ms | 0.4163 ms | 0.2477 ms | 272.86 KB |
| &#39;7Zip LZMA2: Extract all entries&#39; | 4.899 ms | 0.1388 ms | 0.0826 ms | 272.41 KB |
| Method                                                | Mean     | Error    | StdDev   | Allocated |
|------------------------------------------------------ |---------:|---------:|---------:|----------:|
| &#39;Tar: Extract all entries (Archive API)&#39;              | 23.43 μs | 1.533 μs | 0.913 μs |  16.65 KB |
| &#39;Tar: Extract all entries (Reader API)&#39;               | 93.34 μs | 3.640 μs | 2.166 μs | 341.47 KB |
| &#39;Tar: Extract all entries (Archive API) - SystemGzip&#39; | 26.18 μs | 2.360 μs | 1.561 μs |  16.99 KB |
| &#39;Tar: Extract all entries (Reader API) - SystemGzip&#39;  | 93.80 μs | 2.729 μs | 1.805 μs | 341.79 KB |
| &#39;Tar.GZip: Extract all entries&#39;                       |       NA |       NA |       NA |        NA |
| &#39;Tar: Create archive with small files&#39;                | 22.73 μs | 1.167 μs | 0.772 μs |  68.64 KB |
| Method                                                   | Mean     | Error   | StdDev  | Gen0     | Gen1     | Allocated  |
|--------------------------------------------------------- |---------:|--------:|--------:|---------:|---------:|-----------:|
| &#39;Zip: Extract all entries (Archive API) - SystemDeflate&#39; | 146.9 μs | 2.95 μs | 1.76 μs |        - |        - |   72.98 KB |
| &#39;Zip: Extract all entries (Archive API)&#39;                 | 481.3 μs | 7.98 μs | 4.17 μs |        - |        - |  181.87 KB |
| &#39;Zip: Extract all entries (Reader API) - SystemDeflate&#39;  | 135.5 μs | 4.25 μs | 2.53 μs |        - |        - |   14.53 KB |
| &#39;Zip: Extract all entries (Reader API)&#39;                  | 462.0 μs | 4.81 μs | 2.86 μs |        - |        - |  123.58 KB |
| &#39;Zip: Create archive with small files&#39;                   | 175.3 μs | 5.96 μs | 3.94 μs | 300.0000 | 100.0000 | 2806.84 KB |