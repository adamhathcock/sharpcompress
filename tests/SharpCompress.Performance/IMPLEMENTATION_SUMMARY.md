# Performance Benchmarks Implementation Summary

## Overview
This implementation adds comprehensive performance benchmarks using BenchmarkDotNet to the SharpCompress project. The benchmarks run automatically in CI and provide baseline comparisons to detect performance regressions.

## What Was Implemented

### 1. BenchmarkDotNet Integration
- Added BenchmarkDotNet v0.14.0 package to `Directory.Packages.props`
- Configured the Performance project to use BenchmarkDotNet with minimal iterations for CI efficiency
- Set up InProcessEmitToolchain for fast execution with:
  - 1 warmup iteration
  - 3 measurement iterations
  - Memory diagnostics enabled

### 2. Comprehensive Benchmark Suite
Created benchmarks for all major formats in `tests/SharpCompress.Performance/Benchmarks/`:

#### ZipBenchmarks
- Extract all entries using Archive API
- Extract all entries using Reader API
- Create archive with small files

#### TarBenchmarks
- Extract all entries using Archive API
- Extract all entries using Reader API
- Extract Tar.GZip archives
- Create archive with small files

#### RarBenchmarks
- Extract all entries using Archive API
- Extract all entries using Reader API

#### SevenZipBenchmarks
- Extract LZMA compressed archives
- Extract LZMA2 compressed archives

#### GZipBenchmarks
- Compress 100KB of data
- Decompress 100KB of data

### 3. GitHub Actions CI Integration
Created `.github/workflows/performance-benchmarks.yml` that:
- Runs on push to master/release branches
- Runs on pull requests to master/release branches
- Supports manual workflow dispatch
- Displays benchmark results in GitHub Actions summary
- Compares results with baseline
- Uploads results as artifacts

### 4. Baseline Results
Established baseline performance results in `tests/SharpCompress.Performance/baseline-results.md` showing expected performance characteristics for:
- CPU time (Mean, Error, StdDev, Median)
- Memory allocations
- All supported archive formats

### 5. Documentation
Created comprehensive documentation:
- `tests/SharpCompress.Performance/README.md` - Complete guide for running benchmarks
- This summary document
- Inline code comments

### 6. Code Quality
- Applied CSharpier formatting to all benchmark code
- Added BenchmarkDotNet.Artifacts/ to .gitignore to prevent artifact pollution
- Followed existing project conventions

## How to Use

### Run All Benchmarks
```bash
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release
```

### Run Specific Benchmarks
```bash
# Run only Zip benchmarks
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --filter "*ZipBenchmarks*"

# Run with different job configuration
dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --job Short
```

### View Results in CI
1. Navigate to GitHub Actions
2. Select the "Performance Benchmarks" workflow
3. View the run summary for formatted benchmark results
4. Download artifacts for detailed JSON/HTML reports

## Key Design Decisions

### Minimal Iterations for CI
- Configured with minimal iterations (1 warmup, 3 measurements) to keep CI runs fast
- Provides sufficient data for detecting major performance changes
- Users can run with longer configurations locally for more precision

### InProcessEmitToolchain
- Uses InProcessEmitToolchain for fast execution
- Avoids process spawning overhead
- Suitable for CI where speed matters more than absolute isolation

### Test Archive Usage
- Benchmarks use existing test archives from `tests/TestArchives/Archives/`
- Ensures consistency with actual test data
- Tests realistic scenarios

### Memory Diagnostics
- All benchmarks include `[MemoryDiagnoser]` attribute
- Tracks memory allocations and GC pressure
- Helps detect memory-related regressions

## Baseline Comparison Strategy
The baseline file serves as a reference for expected performance. When reviewing benchmark results:
1. Compare Mean times against baseline
2. Look for significant increases (>20% slower)
3. Check memory allocations for unexpected growth
4. Consider that some variance is normal due to different CI hardware

## Maintenance
To update baselines after intentional performance changes:
1. Run benchmarks: `dotnet run --project tests/SharpCompress.Performance/SharpCompress.Performance.csproj --configuration Release -- --exporters markdown --artifacts baseline-output`
2. Combine results: `cat baseline-output/results/*-report-github.md > tests/SharpCompress.Performance/baseline-results.md`
3. Review changes
4. Commit if appropriate

## Future Enhancements
Potential improvements for future iterations:
- Add more granular benchmarks (e.g., single file extraction)
- Benchmark async operations
- Add benchmarks for different compression levels
- Implement automated regression detection with thresholds
- Add comparative benchmarks against System.IO.Compression
- Include larger archive scenarios

## Testing
All benchmarks have been tested and verified to:
- Compile successfully
- Run without errors
- Produce consistent results
- Work with the CI workflow
- Follow project code style
