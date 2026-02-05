# SharpCompress Performance Benchmarks

This project contains performance benchmarks for SharpCompress using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Overview

The benchmarks test all major archive formats supported by SharpCompress:
- **Zip**: Read (Archive & Reader API) and Write operations
- **Tar**: Read (Archive & Reader API) and Write operations, including Tar.GZip
- **Rar**: Read operations (Archive & Reader API)
- **7Zip**: Read operations for LZMA and LZMA2 compression
- **GZip**: Compression and decompression

## Running Benchmarks

### Run all benchmarks
```bash
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release
```

### Run specific benchmark class
```bash
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --filter "*ZipBenchmarks*"
```

### Run with specific job configuration
```bash
# Quick run for testing (1 warmup, 1 iteration)
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --job Dry

# Short run (3 warmup, 3 iterations)
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --job Short

# Medium run (default)
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --job Medium
```

### Export results
```bash
# Export to JSON
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --exporters json

# Export to multiple formats
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --exporters json markdown html
```

### List available benchmarks
```bash
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --list flat
```

## Baseline Results

The baseline results are stored in `baseline-results.md` and represent the expected performance characteristics of the library. These results are used in CI to detect significant performance regressions.

To update the baseline:
1. Run the benchmarks: `dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --exporters markdown --artifacts baseline-output`
2. Combine the results: `cat baseline-output/results/*-report-github.md > baseline-results.md`
3. Review the changes and commit if appropriate

## CI Integration

The performance benchmarks run automatically in GitHub Actions on:
- Push to `master` or `release` branches
- Pull requests to `master` or `release` branches
- Manual workflow dispatch

Results are displayed in the GitHub Actions summary and uploaded as artifacts.

## Benchmark Configuration

The benchmarks are configured with minimal iterations for CI efficiency:
- **Warmup Count**: 1 iteration
- **Iteration Count**: 3 iterations
- **Invocation Count**: 1
- **Unroll Factor**: 1
- **Toolchain**: InProcessEmitToolchain (for fast execution)

These settings provide a good balance between speed and accuracy for CI purposes. For more accurate results, use the `Short`, `Medium`, or `Long` job configurations.

## Memory Diagnostics

All benchmarks include memory diagnostics using `[MemoryDiagnoser]`, which provides:
- Total allocated memory per operation
- Gen 0/1/2 collection counts

## Understanding Results

Key metrics in the benchmark results:
- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation
- **Allocated**: Total managed memory allocated per operation

## Contributing

When adding new benchmarks:
1. Create a new class in the `Benchmarks/` directory
2. Inherit from `ArchiveBenchmarkBase` for archive-related benchmarks
3. Add `[MemoryDiagnoser]` attribute to the class
4. Use `[Benchmark(Description = "...")]` for each benchmark method
5. Add `[GlobalSetup]` for one-time initialization
6. Update this README if needed
