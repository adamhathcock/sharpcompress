using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using SharpCompress.Archives;

namespace SharpCompress.Performance;

/// <summary>
/// Benchmarks comparing current code against a baseline.
/// Use [Baseline] attribute to mark the reference benchmark.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class BaselineComparisonBenchmarks : BenchmarkBase
{
    /// <summary>
    /// Baseline benchmark for Zip archive reading.
    /// This serves as the reference point for comparison.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ZipArchiveRead_Baseline()
    {
        var path = GetTestArchivePath("Zip.deflate.zip");
        using var archive = ArchiveFactory.Open(path);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var stream = entry.OpenEntryStream();
            stream.CopyTo(Stream.Null);
        }
    }

    /// <summary>
    /// Current implementation benchmark for Zip archive reading.
    /// BenchmarkDotNet will compare this against the baseline.
    /// </summary>
    [Benchmark]
    public void ZipArchiveRead_Current()
    {
        var path = GetTestArchivePath("Zip.deflate.zip");
        using var archive = ArchiveFactory.Open(path);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var stream = entry.OpenEntryStream();
            stream.CopyTo(Stream.Null);
        }
    }
}
