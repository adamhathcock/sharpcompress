| Method                   | Mean       | Error    | StdDev   | Allocated |
|------------------------- |-----------:|---------:|---------:|----------:|
| &#39;GZip: Compress 100KB&#39;   | 3,268.7 μs | 28.50 μs | 16.96 μs |  519.2 KB |
| &#39;GZip: Decompress 100KB&#39; |   436.6 μs |  3.23 μs |  1.69 μs |  34.18 KB |
| Method                                   | Mean     | Error     | StdDev    | Allocated |
|----------------------------------------- |---------:|----------:|----------:|----------:|
| &#39;Rar: Extract all entries (Archive API)&#39; | 2.054 ms | 0.3927 ms | 0.2598 ms |  91.09 KB |
| &#39;Rar: Extract all entries (Reader API)&#39;  | 2.235 ms | 0.0253 ms | 0.0132 ms | 149.48 KB |
| Method                            | Mean     | Error     | StdDev    | Allocated |
|---------------------------------- |---------:|----------:|----------:|----------:|
| &#39;7Zip LZMA: Extract all entries&#39;  | 9.124 ms | 2.1930 ms | 1.4505 ms |  272.8 KB |
| &#39;7Zip LZMA2: Extract all entries&#39; | 7.810 ms | 0.1323 ms | 0.0788 ms | 272.58 KB |
| Method                                   | Mean      | Error    | StdDev   | Allocated |
|----------------------------------------- |----------:|---------:|---------:|----------:|
| &#39;Tar: Extract all entries (Archive API)&#39; |  56.36 μs | 3.312 μs | 1.971 μs |  16.65 KB |
| &#39;Tar: Extract all entries (Reader API)&#39;  | 175.34 μs | 2.616 μs | 1.557 μs | 213.36 KB |
| &#39;Tar.GZip: Extract all entries&#39;          |        NA |       NA |       NA |        NA |
| &#39;Tar: Create archive with small files&#39;   |  51.38 μs | 2.349 μs | 1.398 μs |   68.7 KB |
| Method                                   | Mean       | Error    | StdDev   | Gen0     | Allocated  |
|----------------------------------------- |-----------:|---------:|---------:|---------:|-----------:|
| &#39;Zip: Extract all entries (Archive API)&#39; | 1,188.4 μs | 28.62 μs | 14.97 μs |        - |  181.66 KB |
| &#39;Zip: Extract all entries (Reader API)&#39;  | 1,137.0 μs |  5.58 μs |  2.92 μs |        - |  123.19 KB |
| &#39;Zip: Create archive with small files&#39;   |   258.2 μs |  8.98 μs |  4.70 μs | 100.0000 | 2806.93 KB |