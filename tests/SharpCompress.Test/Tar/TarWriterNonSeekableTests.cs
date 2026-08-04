using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test.Tar;

/// <summary>
/// Regression tests for writing tar archives to non-seekable (forward-only) output streams.
/// Tar is a forward-only format with no header back-patching, so streaming to a
/// <see cref="ForwardOnlyStream"/> must produce a valid, readable archive. The sources are
/// seekable so the writer can derive each entry's size up front (see TarWriter.cs).
/// </summary>
public class TarWriterNonSeekableTests
{
    private static readonly DateTime FixedModificationTime = new(2024, 5, 15, 10, 30, 0);

    private static (string Name, byte[] Content)[] CreateStreamingTestEntries() =>
        [
            (
                "first.txt",
                Encoding.UTF8.GetBytes(
                    string.Concat(Enumerable.Repeat("Hello streaming tar! ", 500))
                )
            ),
            (
                "nested/second.txt",
                Encoding.UTF8.GetBytes(
                    string.Concat(Enumerable.Repeat("Another entry with different content. ", 300))
                )
            ),
        ];

    private static async Task<byte[]> WriteArchiveToNonSeekableAsync(
        (string Name, byte[] Content)[] entries
    )
    {
        using var ms = new MemoryStream();
        await using (
            var writer = new TarWriter(
                new ForwardOnlyStream(ms),
                new TarWriterOptions(CompressionType.None, true)
            )
        )
        {
            foreach (var (name, content) in entries)
            {
                using var source = new MemoryStream(content);
                await writer.WriteAsync(name, source, FixedModificationTime);
            }
        }
        return ms.ToArray();
    }

    private static byte[] WriteArchiveToNonSeekableSync((string Name, byte[] Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (
            var writer = new TarWriter(
                new ForwardOnlyStream(ms),
                new TarWriterOptions(CompressionType.None, true)
            )
        )
        {
            foreach (var (name, content) in entries)
            {
                using var source = new MemoryStream(content);
                writer.Write(name, source, FixedModificationTime);
            }
        }
        return ms.ToArray();
    }

    private static async Task AssertRoundTripsAsync(
        byte[] tar,
        (string Name, byte[] Content)[] entries
    )
    {
        using var archive = TarArchive.OpenArchive(new MemoryStream(tar));
        var fileEntries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        Assert.Equal(entries.Length, fileEntries.Count);
        foreach (var (name, content) in entries)
        {
            var entry = fileEntries.Single(e => e.Key == name);
            using var extracted = new MemoryStream();
            var entryStream = await entry.OpenEntryStreamAsync();
            await using (entryStream.DisposeAsyncScope())
            {
                await entryStream.CopyToAsync(extracted);
            }
            Assert.Equal(content, extracted.ToArray());
        }
    }

    [Fact]
    public async Task Tar_Async_NonSeekable_RoundTrips()
    {
        var entries = CreateStreamingTestEntries();
        var tar = await WriteArchiveToNonSeekableAsync(entries);
        await AssertRoundTripsAsync(tar, entries);
    }

    [Fact]
    public async Task Tar_Sync_NonSeekable_RoundTrips()
    {
        var entries = CreateStreamingTestEntries();
        var tar = WriteArchiveToNonSeekableSync(entries);
        await AssertRoundTripsAsync(tar, entries);
    }

    [Fact]
    public async Task Tar_Async_NonSeekable_Matches_Sync_Output()
    {
        var entries = CreateStreamingTestEntries();
        var asyncTar = await WriteArchiveToNonSeekableAsync(entries);
        var syncTar = WriteArchiveToNonSeekableSync(entries);

        Assert.Equal(syncTar, asyncTar);
    }
}
