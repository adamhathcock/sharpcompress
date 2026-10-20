using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using SharpCompress.Archives.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Performance.Benchmarks;

[MemoryDiagnoser]
public class RarEncryptedBenchmarks : ArchiveBenchmarkBase
{
    private byte[] archiveBytes = null!;
    private RarHeaderFactory factory = null!;

    [Params("Rar5.encrypted_filesAndHeader.rar", "Rar5.encrypted_filesOnly.rar", "Rar5.rar")]
    public string ArchiveName { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        archiveBytes = File.ReadAllBytes(GetArchivePath(ArchiveName));
        factory = CreateFactory();
        // Separate warm-cache parsing from expensive first-use key derivation.
        HeadersWarm();
    }

    private static RarHeaderFactory CreateFactory() =>
        new(StreamingMode.Seekable, ReaderOptions.ForExternalStream.WithPassword("test"));

    [Benchmark]
    public int HeadersWarm() => ReadHeaders(factory);

    [Benchmark]
    public int HeadersCold() => ReadHeaders(CreateFactory());

    private int ReadHeaders(RarHeaderFactory headerFactory)
    {
        using var stream = new MemoryStream(archiveBytes);
        var count = 0;
        foreach (var header in headerFactory.ReadHeaders(stream))
        {
            count++;
        }
        return count;
    }

    [Benchmark]
    public async Task<int> HeadersWarmAsync()
    {
        using var stream = new MemoryStream(archiveBytes);
        var count = 0;
        await foreach (var header in factory.ReadHeadersAsync(stream))
        {
            count++;
        }
        return count;
    }

    [Benchmark]
    public void Extract()
    {
        using var stream = new MemoryStream(archiveBytes);
        using var archive = RarArchive.OpenArchive(
            stream,
            ReaderOptions.ForExternalStream.WithPassword("test")
        );
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                using var entryStream = entry.OpenEntryStream();
                entryStream.CopyTo(Stream.Null);
            }
        }
    }

    [Benchmark]
    public async Task ExtractAsync()
    {
        using var stream = new MemoryStream(archiveBytes);
        await using var archive = await RarArchive
            .OpenAsyncArchive(
                stream,
                ReaderOptions.ForExternalStream.WithPassword("test"),
                CancellationToken.None
            )
            .ConfigureAwait(false);
        await foreach (var entry in archive.EntriesAsync)
        {
            if (!entry.IsDirectory)
            {
                await using var entryStream = await entry
                    .OpenEntryStreamAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                await entryStream
                    .CopyToAsync(Stream.Null, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
    }
}
