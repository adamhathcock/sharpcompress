| Method                           | Mean       | Error     | StdDev    | Gen0    | Gen1    | Gen2    | Allocated |
|--------------------------------- |-----------:|----------:|----------:|--------:|--------:|--------:|----------:|
| &#39;GZip: Compress 100KB&#39;           | 3,903.0 μs | 299.60 μs | 448.43 μs | 33.3333 | 33.3333 | 33.3333 | 519.29 KB |
| &#39;GZip: Compress 100KB (Async)&#39;   | 3,792.5 μs | 224.39 μs | 335.86 μs | 33.3333 | 33.3333 | 33.3333 | 519.33 KB |
| &#39;GZip: Decompress 100KB&#39;         |   204.0 μs |  11.96 μs |  17.15 μs |       - |       - |       - |  33.89 KB |
| &#39;GZip: Decompress 100KB (Async)&#39; |   222.2 μs |  11.88 μs |  17.42 μs |       - |       - |       - |  34.17 KB |
| Method                                          | Mean       | Error    | StdDev   | Gen0     | Gen1     | Gen2     | Allocated  |
|------------------------------------------------ |-----------:|---------:|---------:|---------:|---------:|---------:|-----------:|
| &#39;Rar: Extract all entries (Archive API)&#39;        |   947.7 μs | 75.69 μs | 98.42 μs |        - |        - |        - |   90.59 KB |
| &#39;Rar: Extract all entries (Archive API, Async)&#39; | 1,030.6 μs | 43.69 μs | 64.04 μs |        - |        - |        - |   95.72 KB |
| &#39;Rar: Extract all entries (Reader API)&#39;         | 1,181.5 μs | 43.18 μs | 61.92 μs |        - |        - |        - |  148.75 KB |
| &#39;Rar: Extract all entries (Reader API, Async)&#39;  | 1,349.2 μs | 45.16 μs | 67.59 μs | 500.0000 | 500.0000 | 500.0000 | 4775.16 KB |
| Method                                           | Mean      | Error     | StdDev    | Gen0     | Gen1     | Gen2     | Allocated  |
|------------------------------------------------- |----------:|----------:|----------:|---------:|---------:|---------:|-----------:|
| &#39;7Zip LZMA: Extract all entries&#39;                 |  7.694 ms | 0.2467 ms | 0.3616 ms |  33.3333 |  33.3333 |  33.3333 |  272.68 KB |
| &#39;7Zip LZMA: Extract all entries (Async)&#39;         | 31.835 ms | 1.7927 ms | 2.6277 ms | 266.6667 |  66.6667 |  33.3333 |  3402.8 KB |
| &#39;7Zip LZMA2: Extract all entries&#39;                |  8.686 ms | 0.4837 ms | 0.7090 ms |  33.3333 |  33.3333 |  33.3333 |  272.42 KB |
| &#39;7Zip LZMA2: Extract all entries (Async)&#39;        | 27.521 ms | 1.4124 ms | 2.1140 ms | 266.6667 |  66.6667 |  33.3333 | 3409.68 KB |
| &#39;7Zip LZMA2 Reader: Extract all entries&#39;         |  8.047 ms | 0.3851 ms | 0.5399 ms |  33.3333 |  33.3333 |  33.3333 |  273.03 KB |
| &#39;7Zip LZMA2 Reader: Extract all entries (Async)&#39; | 16.332 ms | 0.5697 ms | 0.8351 ms | 200.0000 | 100.0000 | 100.0000 | 2420.81 KB |
| Method                                          | Mean      | Error     | StdDev    | Gen0    | Gen1    | Gen2    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|--------:|--------:|--------:|----------:|
| &#39;Tar: Extract all entries (Archive API)&#39;        |  25.41 μs |  1.016 μs |  1.424 μs |       - |       - |       - |  16.32 KB |
| &#39;Tar: Extract all entries (Archive API, Async)&#39; |  66.86 μs |  6.858 μs | 10.265 μs |       - |       - |       - |  14.56 KB |
| &#39;Tar: Extract all entries (Reader API)&#39;         | 180.28 μs | 27.631 μs | 40.500 μs | 66.6667 | 66.6667 | 66.6667 | 341.23 KB |
| &#39;Tar: Extract all entries (Reader API, Async)&#39;  | 273.87 μs | 53.148 μs | 79.549 μs | 66.6667 | 66.6667 | 66.6667 | 376.08 KB |
| &#39;Tar.GZip: Extract all entries&#39;                 |        NA |        NA |        NA |      NA |      NA |      NA |        NA |
| &#39;Tar.GZip: Extract all entries (Async)&#39;         |        NA |        NA |        NA |      NA |      NA |      NA |        NA |
| &#39;Tar: Create archive with small files&#39;          |  31.85 μs |  2.580 μs |  3.354 μs |       - |       - |       - |  68.11 KB |
| &#39;Tar: Create archive with small files (Async)&#39;  |  32.85 μs |  1.716 μs |  2.516 μs |       - |       - |       - |  68.07 KB |
| Method                                          | Mean     | Error    | StdDev   | Gen0     | Gen1    | Allocated  |
|------------------------------------------------ |---------:|---------:|---------:|---------:|--------:|-----------:|
| &#39;Zip: Extract all entries (Archive API)&#39;        | 715.3 μs | 44.24 μs | 64.84 μs |        - |       - |  180.21 KB |
| &#39;Zip: Extract all entries (Archive API, Async)&#39; | 559.5 μs | 23.80 μs | 33.36 μs |        - |       - |  125.47 KB |
| &#39;Zip: Extract all entries (Reader API)&#39;         | 631.8 μs | 60.82 μs | 89.14 μs |        - |       - |  121.06 KB |
| &#39;Zip: Extract all entries (Reader API, Async)&#39;  | 658.1 μs | 48.01 μs | 71.86 μs |        - |       - |  123.34 KB |
| &#39;Zip: Create archive with small files&#39;          | 225.4 μs | 12.51 μs | 17.94 μs | 200.0000 | 66.6667 | 2806.25 KB |
| &#39;Zip: Create archive with small files (Async)&#39;  | 310.4 μs | 13.46 μs | 20.14 μs | 200.0000 | 66.6667 |  2811.4 KB |