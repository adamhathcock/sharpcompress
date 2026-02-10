using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Providers;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace SharpCompress.Performance.Benchmarks;

[MemoryDiagnoser]
public class TarBenchmarks : ArchiveBenchmarkBase
{
    private byte[] _tarBytes = null!;
    private byte[] _tarGzBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tarBytes = File.ReadAllBytes(GetArchivePath("Tar.tar"));
        _tarGzBytes = File.ReadAllBytes(GetArchivePath("Tar.tar.gz"));
    }

    [Benchmark(Description = "Tar: Extract all entries (Archive API)")]
    public void TarExtractArchiveApi()
    {
        using var stream = new MemoryStream(_tarBytes);
        using var archive = TarArchive.OpenArchive(stream);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }
    }

    [Benchmark(Description = "Tar: Extract all entries (Reader API)")]
    public void TarExtractReaderApi()
    {
        using var stream = new MemoryStream(_tarBytes);
        using var reader = ReaderFactory.OpenReader(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryTo(Stream.Null);
            }
        }
    }

    [Benchmark(Description = "Tar: Extract all entries (Archive API) - SystemGzip")]
    public void SystemTarExtractArchiveApi()
    {
        using var stream = new MemoryStream(_tarBytes);
        using var archive = TarArchive.OpenArchive(
            stream,
            new ReaderOptions().WithProviders(
                CompressionProviderRegistry.Empty.With(new SystemGZipCompressionProvider())
            )
        );
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }
    }

    [Benchmark(Description = "Tar: Extract all entries (Reader API) - SystemGzip")]
    public void SystemTarExtractReaderApi()
    {
        using var stream = new MemoryStream(_tarBytes);
        using var reader = ReaderFactory.OpenReader(
            stream,
            new ReaderOptions().WithProviders(
                CompressionProviderRegistry.Empty.With(new SystemGZipCompressionProvider())
            )
        );
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryTo(Stream.Null);
            }
        }
    }

    [Benchmark(Description = "Tar.GZip: Extract all entries")]
    public void TarGzipExtract()
    {
        using var stream = new MemoryStream(_tarGzBytes);
        using var archive = TarArchive.OpenArchive(stream);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }
    }

    [Benchmark(Description = "Tar: Create archive with small files")]
    public void TarCreateSmallFiles()
    {
        using var outputStream = new MemoryStream();
        using var writer = WriterFactory.OpenWriter(
            outputStream,
            ArchiveType.Tar,
            new WriterOptions(CompressionType.None) { LeaveStreamOpen = true }
        );

        // Create 10 small files
        for (int i = 0; i < 10; i++)
        {
            var data = new byte[1024]; // 1KB each
            using var entryStream = new MemoryStream(data);
            writer.Write($"file{i}.txt", entryStream);
        }
    }
}
