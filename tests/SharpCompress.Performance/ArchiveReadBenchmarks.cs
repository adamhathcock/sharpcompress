using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using SharpCompress.Archives;

namespace SharpCompress.Performance;

/// <summary>
/// Benchmarks for Archive API operations across different formats.
/// Archive API is used for random access to entries with seekable streams.
/// </summary>
[MemoryDiagnoser]
public class ArchiveReadBenchmarks : BenchmarkBase
{
    [Benchmark]
    public void ZipArchiveRead()
    {
        var path = GetTestArchivePath("Zip.deflate.zip");
        using var archive = ArchiveFactory.Open(path);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var stream = entry.OpenEntryStream();
            stream.CopyTo(Stream.Null);
        }
    }

    [Benchmark]
    public void TarArchiveRead()
    {
        var path = GetTestArchivePath("Tar.tar");
        using var archive = ArchiveFactory.Open(path);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var stream = entry.OpenEntryStream();
            stream.CopyTo(Stream.Null);
        }
    }

    [Benchmark]
    public void TarGzArchiveRead()
    {
        var path = GetTestArchivePath("Tar.tar.gz");
        using var archive = ArchiveFactory.Open(path);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var stream = entry.OpenEntryStream();
            stream.CopyTo(Stream.Null);
        }
    }

    [Benchmark]
    public void TarBz2ArchiveRead()
    {
        var path = GetTestArchivePath("Tar.tar.bz2");
        using var archive = ArchiveFactory.Open(path);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var stream = entry.OpenEntryStream();
            stream.CopyTo(Stream.Null);
        }
    }

    [Benchmark]
    public void SevenZipArchiveRead()
    {
        var path = GetTestArchivePath("7Zip.LZMA2.7z");
        using var archive = ArchiveFactory.Open(path);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var stream = entry.OpenEntryStream();
            stream.CopyTo(Stream.Null);
        }
    }

    [Benchmark]
    public void RarArchiveRead()
    {
        var path = GetTestArchivePath("Rar.rar");
        using var archive = ArchiveFactory.Open(path);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var stream = entry.OpenEntryStream();
            stream.CopyTo(Stream.Null);
        }
    }
}
