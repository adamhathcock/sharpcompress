using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

    [Benchmark(Description = "Rar: Extract all entries (Archive API, Async)")]
    public async Task RarExtractArchiveApiAsync()
    {
        using var stream = new MemoryStream(_rarBytes);
        await using var archive = await RarArchive.OpenAsyncArchive(stream).ConfigureAwait(false);
        await foreach (var entry in archive.EntriesAsync.Where(e => !e.IsDirectory))
        {
            await using var entryStream = await entry.OpenEntryStreamAsync().ConfigureAwait(false);
            await entryStream.CopyToAsync(Stream.Null).ConfigureAwait(false);
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

    [Benchmark(Description = "Rar: Extract all entries (Reader API, Async)")]
    public async Task RarExtractReaderApiAsync()
    {
        using var stream = new MemoryStream(_rarBytes);
        await using var reader = await ReaderFactory.OpenAsyncReader(stream).ConfigureAwait(false);
        while (await reader.MoveToNextEntryAsync().ConfigureAwait(false))
        {
            if (!reader.Entry.IsDirectory)
            {
                await reader.WriteEntryToAsync(Stream.Null).ConfigureAwait(false);
            }
        }
    }
}
