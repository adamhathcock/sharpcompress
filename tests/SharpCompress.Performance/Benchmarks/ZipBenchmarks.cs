using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace SharpCompress.Performance.Benchmarks;

[MemoryDiagnoser]
public class ZipBenchmarks : ArchiveBenchmarkBase
{
    private string _archivePath = null!;
    private byte[] _archiveBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _archivePath = GetArchivePath("Zip.deflate.zip");
        _archiveBytes = File.ReadAllBytes(_archivePath);
    }

    [Benchmark(Description = "Zip: Extract all entries (Archive API)")]
    public void ZipExtractArchiveApi()
    {
        using var stream = new MemoryStream(_archiveBytes);
        using var archive = ZipArchive.OpenArchive(stream);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }
    }

    [Benchmark(Description = "Zip: Extract all entries (Archive API, Async)")]
    public async Task ZipExtractArchiveApiAsync()
    {
        using var stream = new MemoryStream(_archiveBytes);
        await using var archive = await ZipArchive.OpenAsyncArchive(stream).ConfigureAwait(false);
        await foreach (var entry in archive.EntriesAsync.Where(e => !e.IsDirectory))
        {
            await using var entryStream = await entry.OpenEntryStreamAsync().ConfigureAwait(false);
            await entryStream.CopyToAsync(Stream.Null).ConfigureAwait(false);
        }
    }

    [Benchmark(Description = "Zip: Extract all entries (Reader API)")]
    public void ZipExtractReaderApi()
    {
        using var stream = new MemoryStream(_archiveBytes);
        using var reader = ReaderFactory.OpenReader(stream);
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryTo(Stream.Null);
            }
        }
    }

    [Benchmark(Description = "Zip: Extract all entries (Reader API, Async)")]
    public async Task ZipExtractReaderApiAsync()
    {
        using var stream = new MemoryStream(_archiveBytes);
        await using var reader = await ReaderFactory.OpenAsyncReader(stream).ConfigureAwait(false);
        while (await reader.MoveToNextEntryAsync().ConfigureAwait(false))
        {
            if (!reader.Entry.IsDirectory)
            {
                await reader.WriteEntryToAsync(Stream.Null).ConfigureAwait(false);
            }
        }
    }

    [Benchmark(Description = "Zip: Create archive with small files")]
    public void ZipCreateSmallFiles()
    {
        using var outputStream = new MemoryStream();
        using var writer = WriterFactory.OpenWriter(
            outputStream,
            ArchiveType.Zip,
            new WriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }
        );

        // Create 10 small files
        for (int i = 0; i < 10; i++)
        {
            var data = new byte[1024]; // 1KB each
            using var entryStream = new MemoryStream(data);
            writer.Write($"file{i}.txt", entryStream);
        }
    }

    [Benchmark(Description = "Zip: Create archive with small files (Async)")]
    public async Task ZipCreateSmallFilesAsync()
    {
        using var outputStream = new MemoryStream();
        await using var writer = await WriterFactory.OpenAsyncWriter(
            outputStream,
            ArchiveType.Zip,
            new WriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }
        );

        for (int i = 0; i < 10; i++)
        {
            var data = new byte[1024];
            using var entryStream = new MemoryStream(data);
            await writer.WriteAsync($"file{i}.txt", entryStream).ConfigureAwait(false);
        }
    }
}
