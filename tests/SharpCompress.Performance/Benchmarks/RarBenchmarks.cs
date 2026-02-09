using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using SharpCompress.Archives.Rar;
using SharpCompress.Readers;

namespace SharpCompress.Performance.Benchmarks;

[MemoryDiagnoser]
public class RarBenchmarks : ArchiveBenchmarkBase
{
    private byte[] _rarBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _rarBytes = File.ReadAllBytes(GetArchivePath("Rar.rar"));
    }

    [Benchmark(Description = "Rar: Extract all entries (Archive API)")]
    public void RarExtractArchiveApi()
    {
        using var stream = new MemoryStream(_rarBytes);
        using var archive = RarArchive.OpenArchive(stream);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }
    }

    [Benchmark(Description = "Rar: Extract all entries (Reader API)")]
    public void RarExtractReaderApi()
    {
        using var stream = new MemoryStream(_rarBytes);
        using var reader = ReaderFactory.OpenReader(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryTo(Stream.Null);
            }
        }
    }
}
