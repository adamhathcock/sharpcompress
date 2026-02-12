| Method                      | Mean     | Error    | StdDev   | Allocated |
|---------------------------- |---------:|---------:|---------:|----------:|
| SharpCompress_0_44_Original | 581.8 ms | 11.56 ms | 17.65 ms |  48.77 MB |
| Method              | Mean       | Error     | StdDev    | Median     | Gen0     | Gen1     | Gen2     | Allocated |
|-------------------- |-----------:|----------:|----------:|-----------:|---------:|---------:|---------:|----------:|
| ZipArchiveRead      |   959.2 μs |  52.16 μs | 153.78 μs |   928.7 μs |  27.3438 |   5.8594 |        - | 345.75 KB |
| TarArchiveRead      |   252.1 μs |  20.97 μs |  61.82 μs |   251.9 μs |  12.2070 |   5.8594 |        - | 154.78 KB |
| TarGzArchiveRead    |   600.9 μs |  19.25 μs |  53.98 μs |   607.8 μs |  16.6016 |   6.8359 |        - | 204.95 KB |
| TarBz2ArchiveRead   |         NA |        NA |        NA |         NA |       NA |       NA |       NA |        NA |
| SevenZipArchiveRead | 8,354.4 μs | 273.01 μs | 747.35 μs | 8,093.2 μs | 109.3750 | 109.3750 | 109.3750 | 787.99 KB |
| RarArchiveRead      | 1,648.6 μs | 131.91 μs | 388.94 μs | 1,617.6 μs |  17.5781 |   5.8594 |        - | 222.62 KB |
| Method                           | Mean       | Error   | StdDev   | Gen0    | Gen1    | Gen2    | Allocated |
|--------------------------------- |-----------:|--------:|---------:|--------:|--------:|--------:|----------:|
| &#39;GZip: Compress 100KB&#39;           | 3,317.1 μs | 7.15 μs | 10.02 μs | 33.3333 | 33.3333 | 33.3333 | 519.31 KB |
| &#39;GZip: Compress 100KB (Async)&#39;   | 3,280.3 μs | 8.30 μs | 11.63 μs | 33.3333 | 33.3333 | 33.3333 | 519.46 KB |
| &#39;GZip: Decompress 100KB&#39;         |   432.5 μs | 2.43 μs |  3.56 μs |       - |       - |       - |  33.92 KB |
| &#39;GZip: Decompress 100KB (Async)&#39; |   442.8 μs | 1.20 μs |  1.76 μs |       - |       - |       - |  34.24 KB |
| Method                                          | Mean       | Error     | StdDev    | Gen0     | Gen1     | Gen2     | Allocated  |
|------------------------------------------------ |-----------:|----------:|----------:|---------:|---------:|---------:|-----------:|
| &#39;Rar: Extract all entries (Archive API)&#39;        |   908.2 μs |  12.42 μs |  17.01 μs |        - |        - |        - |   90.68 KB |
| &#39;Rar: Extract all entries (Archive API, Async)&#39; | 1,175.4 μs | 118.74 μs | 177.72 μs |        - |        - |        - |   96.09 KB |
| &#39;Rar: Extract all entries (Reader API)&#39;         | 1,215.1 μs |   2.26 μs |   3.09 μs |        - |        - |        - |  148.85 KB |
| &#39;Rar: Extract all entries (Reader API, Async)&#39;  | 1,592.0 μs |  22.58 μs |  33.10 μs | 500.0000 | 500.0000 | 500.0000 | 4776.76 KB |
| Method                                           | Mean      | Error     | StdDev    | Gen0     | Gen1     | Gen2     | Allocated  |
|------------------------------------------------- |----------:|----------:|----------:|---------:|---------:|---------:|-----------:|
| &#39;7Zip LZMA: Extract all entries&#39;                 |  7.723 ms | 0.0111 ms | 0.0152 ms |  33.3333 |  33.3333 |  33.3333 |  272.68 KB |
| &#39;7Zip LZMA: Extract all entries (Async)&#39;         | 35.827 ms | 0.0381 ms | 0.0546 ms | 200.0000 |  33.3333 |  33.3333 | 3402.82 KB |
| &#39;7Zip LZMA2: Extract all entries&#39;                |  7.758 ms | 0.0074 ms | 0.0104 ms |  33.3333 |  33.3333 |  33.3333 |  272.46 KB |
| &#39;7Zip LZMA2: Extract all entries (Async)&#39;        | 36.317 ms | 0.0345 ms | 0.0506 ms | 200.0000 |  33.3333 |  33.3333 | 3409.72 KB |
| &#39;7Zip LZMA2 Reader: Extract all entries&#39;         |  7.706 ms | 0.0114 ms | 0.0163 ms |  33.3333 |  33.3333 |  33.3333 |  273.03 KB |
| &#39;7Zip LZMA2 Reader: Extract all entries (Async)&#39; | 22.951 ms | 0.0973 ms | 0.1426 ms | 100.0000 | 100.0000 | 100.0000 | 2420.81 KB |
| Method                                          | Mean      | Error    | StdDev   | Gen0    | Gen1    | Gen2    | Allocated |
|------------------------------------------------ |----------:|---------:|---------:|--------:|--------:|--------:|----------:|
| &#39;Tar: Extract all entries (Archive API)&#39;        |  40.82 μs | 0.292 μs | 0.427 μs |       - |       - |       - |  16.36 KB |
| &#39;Tar: Extract all entries (Archive API, Async)&#39; | 105.12 μs | 6.183 μs | 9.254 μs |       - |       - |       - |  14.57 KB |
| &#39;Tar: Extract all entries (Reader API)&#39;         | 187.89 μs | 1.571 μs | 2.254 μs | 66.6667 | 66.6667 | 66.6667 | 341.24 KB |
| &#39;Tar: Extract all entries (Reader API, Async)&#39;  | 229.78 μs | 4.852 μs | 6.802 μs | 66.6667 | 66.6667 | 66.6667 | 376.64 KB |
| &#39;Tar.GZip: Extract all entries&#39;                 |        NA |       NA |       NA |      NA |      NA |      NA |        NA |
| &#39;Tar.GZip: Extract all entries (Async)&#39;         |        NA |       NA |       NA |      NA |      NA |      NA |        NA |
| &#39;Tar: Create archive with small files&#39;          |  46.98 μs | 0.287 μs | 0.394 μs |       - |       - |       - |  68.11 KB |
| &#39;Tar: Create archive with small files (Async)&#39;  |  53.14 μs | 0.352 μs | 0.493 μs |       - |       - |       - |  68.11 KB |
| Method                                          | Mean     | Error    | StdDev   | Gen0     | Gen1    | Allocated  |
|------------------------------------------------ |---------:|---------:|---------:|---------:|--------:|-----------:|
| &#39;Zip: Extract all entries (Archive API)&#39;        | 556.7 μs |  3.38 μs |  4.74 μs |        - |       - |  180.22 KB |
| &#39;Zip: Extract all entries (Archive API, Async)&#39; | 615.7 μs | 15.98 μs | 22.92 μs |        - |       - |  125.52 KB |
| &#39;Zip: Extract all entries (Reader API)&#39;         | 542.2 μs |  1.10 μs |  1.46 μs |        - |       - |  121.04 KB |
| &#39;Zip: Extract all entries (Reader API, Async)&#39;  | 562.8 μs |  2.42 μs |  3.55 μs |        - |       - |  123.34 KB |
| &#39;Zip: Create archive with small files&#39;          | 271.1 μs | 12.93 μs | 18.95 μs | 166.6667 | 33.3333 | 2806.28 KB |
| &#39;Zip: Create archive with small files (Async)&#39;  | 394.3 μs | 25.59 μs | 36.71 μs | 166.6667 | 33.3333 | 2811.42 KB |