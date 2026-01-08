# SharpCompress Performance Benchmarks

This project uses [BenchmarkDotNet](https://benchmarkdotnet.org/) to measure and track performance of SharpCompress archive operations.

## Running Benchmarks

### Run All Benchmarks
```bash
cd tests/SharpCompress.Performance
dotnet run -c Release
```

### Run Specific Benchmark Classes
```bash
# Run only Archive API benchmarks
dotnet run -c Release -- --filter "*ArchiveReadBenchmarks*"

# Run only Reader API benchmarks
dotnet run -c Release -- --filter "*ReaderBenchmarks*"
```

### Run Specific Benchmark Methods
```bash
# Run only Zip benchmarks
dotnet run -c Release -- --filter "*Zip*"

# Run a specific method
dotnet run -c Release -- --filter "ArchiveReadBenchmarks.ZipArchiveRead"
```

### Quick Dry Run (for testing)
```bash
dotnet run -c Release -- --job dry
```

## Benchmark Categories

### ArchiveReadBenchmarks
Tests the **Archive API** which provides random access to entries with seekable streams. Covers:
- Zip (deflate compression)
- Tar (uncompressed)
- Tar.gz (gzip compression)
- Tar.bz2 (bzip2 compression)
- 7Zip (LZMA2 compression)
- Rar

### ReaderBenchmarks
Tests the **Reader API** which provides forward-only streaming for non-seekable streams. Covers:
- Zip
- Tar
- Tar.gz
- Tar.bz2
- Rar

### WriteBenchmarks
Tests the **Writer API** for creating archives using forward-only writing. Covers:
- Zip (deflate compression)
- Tar (uncompressed)
- Tar.gz (gzip compression)

### BaselineComparisonBenchmarks
Example benchmark showing how to compare implementations using the `[Baseline]` attribute. The baseline benchmark serves as a reference point, and BenchmarkDotNet calculates the ratio of performance between baseline and other methods.

## Comparing Against Previous Versions

### Using Baseline Attribute
Mark one benchmark with `[Baseline = true]` and BenchmarkDotNet will show relative performance:

```csharp
[Benchmark(Baseline = true)]
public void MethodA() { /* ... */ }

[Benchmark]
public void MethodB() { /* ... */ }
```

Results will show ratios like "1.5x slower" or "0.8x faster" compared to the baseline.

### Using BenchmarkDotNet.Artifacts for Historical Comparison
BenchmarkDotNet saves results to `BenchmarkDotNet.Artifacts/results/`. You can:

1. Run benchmarks and save the results
2. Keep a snapshot of the results file
3. Compare new runs against saved results

### Using Different NuGet Versions (Advanced)
To compare against a published NuGet package:

1. Create a separate benchmark project referencing the NuGet package
2. Use BenchmarkDotNet's `[SimpleJob]` attribute with different runtimes
3. Reference both the local project and NuGet package in different jobs

## Interpreting Results

BenchmarkDotNet provides:
- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of measurements
- **Allocated**: Memory allocated per operation
- **Rank**: Relative ranking (when using `[RankColumn]`)
- **Ratio**: Relative performance vs baseline (when using `[Baseline]`)

## Output Artifacts

Results are saved to `BenchmarkDotNet.Artifacts/results/`:
- `*.csv`: Raw data for further analysis
- `*-report.html`: HTML report with charts
- `*-report-github.md`: Markdown report for GitHub
- `*.log`: Detailed execution log

## Best Practices

1. **Always run in Release mode**: Debug builds have significant overhead
2. **Close other applications**: Minimize system noise during benchmarks
3. **Run multiple times**: Look for consistency across runs
4. **Use appropriate workload**: Ensure benchmarks run for at least 100ms
5. **Track trends**: Compare results over time to detect regressions
6. **Archive results**: Keep snapshots of benchmark results for historical comparison

## CI/CD Integration

Consider adding benchmarks to CI/CD to:
- Detect performance regressions automatically
- Track performance trends over time
- Compare PR performance against main branch

## Additional Resources

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/articles/overview.html)
- [BenchmarkDotNet Configuration](https://benchmarkdotnet.org/articles/configs/configs.html)
- [BenchmarkDotNet Baseline](https://benchmarkdotnet.org/articles/features/baselines.html)
