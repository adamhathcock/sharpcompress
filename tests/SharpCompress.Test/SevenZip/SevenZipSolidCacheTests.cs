using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.SevenZip;

/// <summary>
/// A solid archive is decoded by a single shared, forward-only stream. These tests cover the
/// bookkeeping of that shared stream: entries that are not read completely must not make the
/// following entries start at the wrong offset.
/// </summary>
public class SevenZipSolidCacheTests : TestBase
{
    private const string SolidArchive = "7Zip.solid.7z";
    private const string SingleBlockSolidArchive = "7Zip.solid.1block.7z";

    private static string PathOf(string archiveName) =>
        Path.Combine(TEST_ARCHIVES_PATH, archiveName);

    private static List<IArchiveEntry> FileEntries(IArchive archive) =>
        archive.Entries.Where(e => !e.IsDirectory).ToList();

    private static async Task<List<IArchiveEntry>> FileEntriesAsync(IAsyncArchive archive) =>
        (
            await archive
                .EntriesAsync.Where(e => !e.IsDirectory)
                .ToListAsync()
                .ConfigureAwait(false)
        ).ToList();

    /// <summary>
    /// Reads a single entry from its own archive instance so that the expected content never
    /// depends on the shared folder stream.
    /// </summary>
    private static byte[] ExpectedContent(string archiveName, int entryIndex)
    {
        using var archive = SevenZipArchive.OpenArchive(PathOf(archiveName));
        var entry = FileEntries(archive)[entryIndex];
        using var stream = entry.OpenEntryStream();
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return content.ToArray();
    }

    private static async Task<byte[]> ExpectedContentAsync(string archiveName, int entryIndex)
    {
        await using var archive = await SevenZipArchive
            .OpenAsyncArchive(PathOf(archiveName))
            .ConfigureAwait(false);
        var entry = (await FileEntriesAsync(archive).ConfigureAwait(false))[entryIndex];
        var stream = await entry.OpenEntryStreamAsync(CancellationToken.None).ConfigureAwait(false);
        await using var scope = stream.DisposeAsyncScope().ConfigureAwait(false);
        using var content = new MemoryStream();
        await stream
            .CopyToAsync(content, Constants.BufferSize, CancellationToken.None)
            .ConfigureAwait(false);
        return content.ToArray();
    }

    private static byte[] ReadAll(IArchiveEntry entry)
    {
        using var stream = entry.OpenEntryStream();
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return content.ToArray();
    }

    private static async Task<byte[]> ReadAllAsync(IArchiveEntry entry)
    {
        var stream = await entry.OpenEntryStreamAsync(CancellationToken.None).ConfigureAwait(false);
        await using var scope = stream.DisposeAsyncScope().ConfigureAwait(false);
        using var content = new MemoryStream();
        await stream
            .CopyToAsync(content, Constants.BufferSize, CancellationToken.None)
            .ConfigureAwait(false);
        return content.ToArray();
    }

    private static void ReadPartial(IArchiveEntry entry, int byteCount)
    {
        using var stream = entry.OpenEntryStream();
        var buffer = new byte[byteCount];
        Assert.Equal(byteCount, stream.Read(buffer, 0, buffer.Length));
    }

    private static async Task ReadPartialAsync(IArchiveEntry entry, int byteCount)
    {
        var stream = await entry.OpenEntryStreamAsync(CancellationToken.None).ConfigureAwait(false);
        await using var scope = stream.DisposeAsyncScope().ConfigureAwait(false);
        var buffer = new byte[byteCount];
        Assert.Equal(
            byteCount,
            await stream
                .ReadAsync(buffer, 0, buffer.Length, CancellationToken.None)
                .ConfigureAwait(false)
        );
    }

    private static object? GetField(object instance, string name) =>
        instance
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance);

    private static object? CachedFolderStream(object archive) =>
        GetField(GetField(archive, "_database")!, "_cachedFolderStream");

    [Theory]
    [InlineData(SolidArchive)]
    [InlineData(SingleBlockSolidArchive)]
    public void PartialRead_ThenNextEntry_ReturnsEntryContent(string archiveName)
    {
        var expected = ExpectedContent(archiveName, 1);

        using var archive = SevenZipArchive.OpenArchive(PathOf(archiveName));
        var entries = FileEntries(archive);
        Assert.True(entries.Count >= 2);

        ReadPartial(entries[0], 16);

        Assert.Equal(expected, ReadAll(entries[1]));
    }

    [Theory]
    [InlineData(SolidArchive)]
    [InlineData(SingleBlockSolidArchive)]
    public void ZeroRead_ThenNextEntry_ReturnsEntryContent(string archiveName)
    {
        var expected = ExpectedContent(archiveName, 1);

        using var archive = SevenZipArchive.OpenArchive(PathOf(archiveName));
        var entries = FileEntries(archive);
        Assert.True(entries.Count >= 2);

        using (entries[0].OpenEntryStream()) { }

        Assert.Equal(expected, ReadAll(entries[1]));
    }

    [Theory]
    [InlineData(SolidArchive)]
    [InlineData(SingleBlockSolidArchive)]
    public void PartialRead_ThenEarlierEntry_ReturnsEntryContent(string archiveName)
    {
        var expected = ExpectedContent(archiveName, 0);

        using var archive = SevenZipArchive.OpenArchive(PathOf(archiveName));
        var entries = FileEntries(archive);
        Assert.True(entries.Count >= 2);

        ReadPartial(entries[1], 16);

        Assert.Equal(expected, ReadAll(entries[0]));
    }

    [Fact]
    public void PartialRead_ThenLaterEntry_ReturnsEntryContent()
    {
        var expected = ExpectedContent(SolidArchive, 2);

        using var archive = SevenZipArchive.OpenArchive(PathOf(SolidArchive));
        var entries = FileEntries(archive);
        Assert.True(entries.Count >= 3);

        ReadPartial(entries[0], 16);

        Assert.Equal(expected, ReadAll(entries[2]));
    }

    [Theory]
    [InlineData(SolidArchive)]
    [InlineData(SingleBlockSolidArchive)]
    public async Task PartialRead_ThenNextEntryAsync_ReturnsEntryContent(string archiveName)
    {
        var expected = await ExpectedContentAsync(archiveName, 1);

        await using var archive = await SevenZipArchive
            .OpenAsyncArchive(PathOf(archiveName))
            .ConfigureAwait(false);
        var entries = await FileEntriesAsync(archive).ConfigureAwait(false);
        Assert.True(entries.Count >= 2);

        await ReadPartialAsync(entries[0], 16).ConfigureAwait(false);

        Assert.Equal(expected, await ReadAllAsync(entries[1]).ConfigureAwait(false));
    }

    [Theory]
    [InlineData(SolidArchive)]
    [InlineData(SingleBlockSolidArchive)]
    public async Task ZeroRead_ThenNextEntryAsync_ReturnsEntryContent(string archiveName)
    {
        var expected = await ExpectedContentAsync(archiveName, 1);

        await using var archive = await SevenZipArchive
            .OpenAsyncArchive(PathOf(archiveName))
            .ConfigureAwait(false);
        var entries = await FileEntriesAsync(archive).ConfigureAwait(false);
        Assert.True(entries.Count >= 2);

        var first = await entries[0]
            .OpenEntryStreamAsync(CancellationToken.None)
            .ConfigureAwait(false);
        await using (first.DisposeAsyncScope()) { }

        Assert.Equal(expected, await ReadAllAsync(entries[1]).ConfigureAwait(false));
    }

    [Theory]
    [InlineData(SolidArchive)]
    [InlineData(SingleBlockSolidArchive)]
    public async Task PartialRead_ThenEarlierEntryAsync_ReturnsEntryContent(string archiveName)
    {
        var expected = await ExpectedContentAsync(archiveName, 0);

        await using var archive = await SevenZipArchive
            .OpenAsyncArchive(PathOf(archiveName))
            .ConfigureAwait(false);
        var entries = await FileEntriesAsync(archive).ConfigureAwait(false);
        Assert.True(entries.Count >= 2);

        await ReadPartialAsync(entries[1], 16).ConfigureAwait(false);

        Assert.Equal(expected, await ReadAllAsync(entries[0]).ConfigureAwait(false));
    }

    [Fact]
    public void AlternatingSyncAndAsyncEntryReads_ReturnEntryContent()
    {
        var expected = FileEntries(SevenZipArchive.OpenArchive(PathOf(SolidArchive)))
            .Select((_, index) => ExpectedContent(SolidArchive, index))
            .ToList();

        using var archive = SevenZipArchive.OpenArchive(PathOf(SolidArchive));
        var entries = FileEntries(archive);
        Assert.True(entries.Count >= 3);

        ReadPartial(entries[0], 16);

        var second = entries[1].OpenEntryStream();
        using (second)
        {
            using var content = new MemoryStream();
            second.CopyTo(content);
            Assert.Equal(expected[1], content.ToArray());
        }

        Assert.Equal(expected[2], ReadAll(entries[2]));
    }

    [Fact]
    public async Task AlternatingSyncAndAsyncEntryReads_ReturnEntryContentAsync()
    {
        var expected = new List<byte[]>();
        for (var index = 0; index < 3; index++)
        {
            expected.Add(await ExpectedContentAsync(SolidArchive, index).ConfigureAwait(false));
        }

        await using var archive = await SevenZipArchive
            .OpenAsyncArchive(PathOf(SolidArchive))
            .ConfigureAwait(false);
        var entries = await FileEntriesAsync(archive).ConfigureAwait(false);
        Assert.True(entries.Count >= 3);

        await ReadPartialAsync(entries[0], 16).ConfigureAwait(false);

        var second = entries[1].OpenEntryStream();
        using (second)
        {
            using var content = new MemoryStream();
            await second
                .CopyToAsync(content, Constants.BufferSize, CancellationToken.None)
                .ConfigureAwait(false);
            Assert.Equal(expected[1], content.ToArray());
        }

        Assert.Equal(expected[2], await ReadAllAsync(entries[2]).ConfigureAwait(false));
    }

    [Fact]
    public void FullyReadEntries_ReuseTheCachedFolderStream()
    {
        using var archive = SevenZipArchive.OpenArchive(PathOf(SolidArchive));
        var entries = FileEntries(archive);
        Assert.True(entries.Count >= 2);

        ReadAll(entries[0]);
        var decoder = CachedFolderStream(archive);
        Assert.NotNull(decoder);

        ReadAll(entries[1]);

        // The decoder of a solid folder is shared by all of its entries and must not be recreated
        // for every entry, otherwise extracting N entries decodes O(N^2) bytes.
        Assert.Same(decoder, CachedFolderStream(archive));
    }

    [Fact]
    public void PartialRead_ThenNextEntry_ReusesTheCachedFolderStream()
    {
        using var archive = SevenZipArchive.OpenArchive(PathOf(SolidArchive));
        var entries = FileEntries(archive);
        Assert.True(entries.Count >= 2);

        ReadPartial(entries[0], 16);
        var decoder = CachedFolderStream(archive);
        Assert.NotNull(decoder);

        ReadAll(entries[1]);

        // The bytes that were not read are skipped forward, the folder is not decoded again.
        Assert.Same(decoder, CachedFolderStream(archive));
    }

    [Fact]
    public void Dispose_ReleasesTheCachedFolderStream_AndKeepsTheSourceStreamUsable()
    {
        using var source = File.OpenRead(PathOf(SolidArchive));
        var archive = SevenZipArchive.OpenArchive(source, ReaderOptions.ForExternalStream);
        var entries = FileEntries(archive);

        ReadPartial(entries[0], 16);
        var decoder = CachedFolderStream(archive);
        Assert.NotNull(decoder);

        archive.Dispose();

        Assert.Null(CachedFolderStream(archive));

        // The source stream is borrowed from the caller and must stay usable.
        Assert.True(source.CanRead);
        source.Position = 0;
        Assert.True(source.ReadByte() >= 0);
    }
}
