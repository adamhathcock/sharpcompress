using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
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

    [Benchmark(Description = "Tar: Extract all entries (Archive API, Async)")]
    public async Task TarExtractArchiveApiAsync()
    {
        using var stream = new MemoryStream(_tarBytes);
        await using var archive = await TarArchive.OpenAsyncArchive(stream).ConfigureAwait(false);
        await foreach (var entry in archive.EntriesAsync.Where(e => !e.IsDirectory))
        {
            await using var entryStream = await entry.OpenEntryStreamAsync().ConfigureAwait(false);
            await entryStream.CopyToAsync(Stream.Null).ConfigureAwait(false);
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

    [Benchmark(Description = "Tar: Extract all entries (Reader API, Async)")]
    public async Task TarExtractReaderApiAsync()
    {
        using var stream = new MemoryStream(_tarBytes);
        await using var reader = await ReaderFactory.OpenAsyncReader(stream).ConfigureAwait(false);
        while (await reader.MoveToNextEntryAsync().ConfigureAwait(false))
        {
            if (!reader.Entry.IsDirectory)
            {
                await reader.WriteEntryToAsync(Stream.Null).ConfigureAwait(false);
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

    [Benchmark(Description = "Tar.GZip: Extract all entries (Async)")]
    public async Task TarGzipExtractAsync()
    {
        using var stream = new MemoryStream(_tarGzBytes);
        await using var archive = await TarArchive.OpenAsyncArchive(stream).ConfigureAwait(false);
        await foreach (var entry in archive.EntriesAsync.Where(e => !e.IsDirectory))
        {
            await using var entryStream = await entry.OpenEntryStreamAsync().ConfigureAwait(false);
            await entryStream.CopyToAsync(Stream.Null).ConfigureAwait(false);
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

    [Benchmark(Description = "Tar: Create archive with small files (Async)")]
    public async Task TarCreateSmallFilesAsync()
    {
        using var outputStream = new MemoryStream();
        await using var writer = await WriterFactory.OpenAsyncWriter(
            outputStream,
            ArchiveType.Tar,
            new WriterOptions(CompressionType.None) { LeaveStreamOpen = true }
        );

        for (int i = 0; i < 10; i++)
        {
            var data = new byte[1024];
            using var entryStream = new MemoryStream(data);
            await writer.WriteAsync($"file{i}.txt", entryStream).ConfigureAwait(false);
        }
    }
}
