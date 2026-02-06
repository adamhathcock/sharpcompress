using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using SharpCompress.Archives.SevenZip;

namespace SharpCompress.Performance.Benchmarks;

[MemoryDiagnoser]
public class SevenZipBenchmarks : ArchiveBenchmarkBase
{
    private byte[] _lzmaBytes = null!;
    private byte[] _lzma2Bytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _lzmaBytes = File.ReadAllBytes(GetArchivePath("7Zip.LZMA.7z"));
        _lzma2Bytes = File.ReadAllBytes(GetArchivePath("7Zip.LZMA2.7z"));
    }

    [Benchmark(Description = "7Zip LZMA: Extract all entries")]
    public void SevenZipLzmaExtract()
    {
        using var stream = new MemoryStream(_lzmaBytes);
        using var archive = SevenZipArchive.OpenArchive(stream);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }
    }

    [Benchmark(Description = "7Zip LZMA2: Extract all entries")]
    public void SevenZipLzma2Extract()
    {
        using var stream = new MemoryStream(_lzma2Bytes);
        using var archive = SevenZipArchive.OpenArchive(stream);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }
    }
}
