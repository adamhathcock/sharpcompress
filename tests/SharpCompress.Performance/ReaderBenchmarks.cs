using System.IO;
using BenchmarkDotNet.Attributes;
using SharpCompress.Readers;

namespace SharpCompress.Performance;

/// <summary>
/// Benchmarks for Reader API operations across different formats.
/// Reader API is used for forward-only streaming with non-seekable streams.
/// </summary>
[MemoryDiagnoser]
public class ReaderBenchmarks : BenchmarkBase
{
    [Benchmark]
    public void ZipReaderRead()
    {
        var path = GetTestArchivePath("Zip.deflate.zip");
        using var stream = File.OpenRead(path);
        using var reader = ReaderFactory.Open(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryTo(Stream.Null);
            }
        }
    }

    [Benchmark]
    public void TarReaderRead()
    {
        var path = GetTestArchivePath("Tar.tar");
        using var stream = File.OpenRead(path);
        using var reader = ReaderFactory.Open(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryTo(Stream.Null);
            }
        }
    }

    [Benchmark]
    public void TarGzReaderRead()
    {
        var path = GetTestArchivePath("Tar.tar.gz");
        using var stream = File.OpenRead(path);
        using var reader = ReaderFactory.Open(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryTo(Stream.Null);
            }
        }
    }

    [Benchmark]
    public void TarBz2ReaderRead()
    {
        var path = GetTestArchivePath("Tar.tar.bz2");
        using var stream = File.OpenRead(path);
        using var reader = ReaderFactory.Open(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryTo(Stream.Null);
            }
        }
    }

    [Benchmark]
    public void RarReaderRead()
    {
        var path = GetTestArchivePath("Rar.rar");
        using var stream = File.OpenRead(path);
        using var reader = ReaderFactory.Open(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryTo(Stream.Null);
            }
        }
    }
}
