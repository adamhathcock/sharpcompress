using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        using var reader = archive.ExtractAllEntries();
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            using var entryStream = reader.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }
    }

    [Benchmark(Description = "7Zip LZMA: Extract all entries (Async)")]
    public async Task SevenZipLzmaExtractAsync()
    {
        using var stream = new MemoryStream(_lzmaBytes);
        await using var archive = await SevenZipArchive
            .OpenAsyncArchive(stream)
            .ConfigureAwait(false);
        await using var reader = await archive.ExtractAllEntriesAsync().ConfigureAwait(false);
        while (await reader.MoveToNextEntryAsync().ConfigureAwait(false))
        {
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            await using var entryStream = await reader.OpenEntryStreamAsync().ConfigureAwait(false);
            await entryStream.CopyToAsync(Stream.Null).ConfigureAwait(false);
        }
    }

    [Benchmark(Description = "7Zip LZMA2: Extract all entries")]
    public void SevenZipLzma2Extract()
    {
        using var stream = new MemoryStream(_lzma2Bytes);
        using var archive = SevenZipArchive.OpenArchive(stream);
        using var reader = archive.ExtractAllEntries();
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            using var entryStream = reader.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }
    }

    [Benchmark(Description = "7Zip LZMA2: Extract all entries (Async)")]
    public async Task SevenZipLzma2ExtractAsync()
    {
        using var stream = new MemoryStream(_lzma2Bytes);
        await using var archive = await SevenZipArchive
            .OpenAsyncArchive(stream)
            .ConfigureAwait(false);
        await using var reader = await archive.ExtractAllEntriesAsync().ConfigureAwait(false);
        while (await reader.MoveToNextEntryAsync().ConfigureAwait(false))
        {
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            await using var entryStream = await reader.OpenEntryStreamAsync().ConfigureAwait(false);
            await entryStream.CopyToAsync(Stream.Null).ConfigureAwait(false);
        }
    }

    [Benchmark(Description = "7Zip LZMA2 Reader: Extract all entries")]
    public void SevenZipLzma2Extract_Reader()
    {
        using var stream = new MemoryStream(_lzma2Bytes);
        using var archive = SevenZipArchive.OpenArchive(stream);
        using var reader = archive.ExtractAllEntries();
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            using var entryStream = reader.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }
    }

    [Benchmark(Description = "7Zip LZMA2 Reader: Extract all entries (Async)")]
    public async Task SevenZipLzma2ExtractAsync_Reader()
    {
        using var stream = new MemoryStream(_lzma2Bytes);
        await using var archive = await SevenZipArchive
            .OpenAsyncArchive(stream)
            .ConfigureAwait(false);
        await using var reader = await archive.ExtractAllEntriesAsync();
        while (await reader.MoveToNextEntryAsync().ConfigureAwait(false))
        {
            await using var entryStream = await reader.OpenEntryStreamAsync().ConfigureAwait(false);
            await entryStream.CopyToAsync(Stream.Null).ConfigureAwait(false);
        }
    }
}
