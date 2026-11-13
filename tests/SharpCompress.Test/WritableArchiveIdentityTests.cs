using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test;

public class WritableArchiveIdentityTests
{
    [Theory]
    [InlineData(ArchiveType.Zip, false, false)]
    [InlineData(ArchiveType.Zip, false, true)]
    [InlineData(ArchiveType.Zip, true, false)]
    [InlineData(ArchiveType.Zip, true, true)]
    [InlineData(ArchiveType.Tar, false, false)]
    [InlineData(ArchiveType.Tar, false, true)]
    [InlineData(ArchiveType.Tar, true, false)]
    [InlineData(ArchiveType.Tar, true, true)]
    public async Task Removal_Survives_Switching_Apis_And_Rebuilding(
        ArchiveType type,
        bool asyncFirst,
        bool paused
    )
    {
        using var input = CreateArchive(type, "remove", "keep");
        using var archive = ArchiveFactory.OpenArchive(input, ReaderOptions.ForExternalStream);
        var writable = (IWritableArchive)archive;
        var asyncWritable = (IWritableAsyncArchive)archive;
        // Leave the first loader partially consumed before switching APIs.
        var entry = asyncFirst
            ? await asyncWritable.EntriesAsync.FirstAsync(CancellationToken.None)
            : archive.Entries.First();
        var otherEntry = asyncFirst
            ? archive.Entries.First()
            : await asyncWritable.EntriesAsync.FirstAsync(CancellationToken.None);
        Assert.Same(entry, otherEntry);

        using var source = new MemoryStream(new byte[] { 42 });
        using (paused ? writable.PauseEntryRebuilding() : null)
        {
            if (asyncFirst)
            {
                writable.RemoveEntry(entry);
                writable.AddEntry("new", source, false);
            }
            else
            {
                await asyncWritable.RemoveEntryAsync(entry);
                await asyncWritable.AddEntryAsync(
                    "new",
                    source,
                    false,
                    cancellationToken: CancellationToken.None
                );
            }
        }
        var syncEntries = archive.Entries.ToList();
        var asyncEntries = await asyncWritable.EntriesAsync.ToListAsync(CancellationToken.None);
        Assert.Equal(new[] { "keep", "new" }, syncEntries.Select(e => e.Key));
        Assert.Equal(syncEntries, asyncEntries);

        foreach (var useAsync in new[] { false, true })
        {
            using var output = new MemoryStream();
            await Save(archive, output, useAsync);
            output.Position = 0;
            using var saved = ArchiveFactory.OpenArchive(output, ReaderOptions.ForExternalStream);
            Assert.Equal(new[] { "keep", "new" }, saved.Entries.Select(e => e.Key));
        }
        Assert.True(input.CanRead);
        Assert.True(source.CanRead);
    }

    [Theory]
    [InlineData(ArchiveType.Zip, false)]
    [InlineData(ArchiveType.Zip, true)]
    [InlineData(ArchiveType.Tar, false)]
    [InlineData(ArchiveType.Tar, true)]
    public async Task Shared_Identity_Does_Not_Remove_Other_Entries_With_The_Same_Name(
        ArchiveType type,
        bool asyncFirst
    )
    {
        using var input = CreateArchive(type, "same", "same");
        using var archive = ArchiveFactory.OpenArchive(input, ReaderOptions.ForExternalStream);
        var asyncArchive = (IWritableAsyncArchive)archive;
        if (asyncFirst)
        {
            var entry = await asyncArchive.EntriesAsync.FirstAsync(CancellationToken.None);
            ((IWritableArchive)archive).RemoveEntry(entry);
        }
        else
        {
            await asyncArchive.RemoveEntryAsync(archive.Entries.First());
        }
        var remaining = Assert.Single(archive.Entries);
        Assert.Same(
            remaining,
            Assert.Single(await asyncArchive.EntriesAsync.ToListAsync(CancellationToken.None))
        );
        using var payload = remaining.OpenEntryStream();
        Assert.Equal(2, payload.ReadByte());
    }

    [Theory]
    [InlineData(ArchiveType.Zip)]
    [InlineData(ArchiveType.Tar)]
    public async Task Partially_Consumed_Loaders_Resume_From_The_Shared_Cache(ArchiveType type)
    {
        using var input = CreateArchive(type, "first", "second", "third");
        using var archive = ArchiveFactory.OpenArchive(input, ReaderOptions.ForExternalStream);
        using var syncEntries = archive.Entries.GetEnumerator();
        await using var asyncEntries = ((IAsyncArchive)archive).EntriesAsync.GetAsyncEnumerator(
            CancellationToken.None
        );
        Assert.True(syncEntries.MoveNext());
        Assert.True(await asyncEntries.MoveNextAsync());
        Assert.Same(syncEntries.Current, asyncEntries.Current);
        Assert.True(await asyncEntries.MoveNextAsync());
        var second = asyncEntries.Current;
        Assert.True(syncEntries.MoveNext());
        Assert.Same(second, syncEntries.Current);
        Assert.True(syncEntries.MoveNext());
        Assert.True(await asyncEntries.MoveNextAsync());
        Assert.Same(syncEntries.Current, asyncEntries.Current);
        Assert.False(syncEntries.MoveNext());
        Assert.False(await asyncEntries.MoveNextAsync());
    }

    private static MemoryStream CreateArchive(ArchiveType type, params string[] names)
    {
        var input = new MemoryStream();
        using (
            var writer = WriterFactory.OpenWriter(
                input,
                type,
                new WriterOptions(CompressionType.None) { LeaveStreamOpen = true }
            )
        )
        {
            for (var i = 0; i < names.Length; i++)
            {
                using var source = new MemoryStream(new[] { (byte)(i + 1) });
                writer.Write(names[i], source, DateTime.UtcNow);
            }
        }
        input.Position = 0;
        return input;
    }

    private static async Task Save(IArchive archive, Stream output, bool useAsync)
    {
        if (archive is ZipArchive zip)
        {
            var options = new ZipWriterOptions(CompressionType.None) { LeaveStreamOpen = true };
            if (useAsync)
            {
                await zip.SaveToAsync(output, options, CancellationToken.None);
            }
            else
            {
                zip.SaveTo(output, options);
            }
        }
        else
        {
            var tar = (TarArchive)archive;
            var options = new TarWriterOptions(CompressionType.None) { LeaveStreamOpen = true };
            if (useAsync)
            {
                await tar.SaveToAsync(output, options, CancellationToken.None);
            }
            else
            {
                tar.SaveTo(output, options);
            }
        }
    }
}
