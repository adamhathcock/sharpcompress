# SharpCompress Performance Benchmarks

This project contains performance benchmarks for SharpCompress using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Overview

The benchmarks test all major archive formats supported by SharpCompress:
- **Zip**: Read (Archive & Reader API) and Write operations, each with sync and async variants
- **Tar**: Read (Archive & Reader API) and Write operations, including Tar.GZip, each with sync and async variants
- **Rar**: Read operations (Archive & Reader API), each with sync and async variants
- **7Zip**: Read operations for LZMA and LZMA2 compression, each with sync and async variants
- **GZip**: Compression and decompression, each with sync and async variants

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

### Generate Baseline (Automated)

Use the build target to generate baseline results:
```bash
dotnet run --project build/build.csproj -- generate-baseline
```

This will:
1. Build the performance project
2. Run all benchmarks
3. Combine the markdown reports into `baseline-results.md`
4. Clean up temporary artifacts

### Generate Baseline (Manual)

To manually update the baseline:
1. Run the benchmarks: `dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --exporters markdown --artifacts baseline-output`
2. Combine the results: `cat baseline-output/results/*-report-github.md > baseline-results.md`
3. Review the changes and commit if appropriate

## JetBrains Profiler Integration

The performance project supports JetBrains profiler for detailed CPU and memory profiling during local development.

### Prerequisites

Install JetBrains profiler tools from: https://www.jetbrains.com/profiler/

### Run with CPU Profiling
```bash
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --profile --type cpu --output ./my-cpu-snapshots
```

### Run with Memory Profiling
```bash
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --profile --type memory --output ./my-memory-snapshots
```

### Profiler Options
- `--profile`: Enable profiler mode
- `--type cpu|memory`: Choose profiling type (default: cpu)
- `--output <path>`: Specify snapshot output directory (default: ./profiler-snapshots)

The profiler will run a sample benchmark and save snapshots that can be opened in JetBrains profiler tools for detailed analysis.

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
