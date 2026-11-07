using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipArchiveDuplicateTests
{
    [Theory]
    [InlineData("dir/file.txt", "DIR/FILE.TXT")]
    [InlineData("dir/file.txt", "dir\\file.txt")]
    [InlineData("file.txt", "C:/file.txt")]
    [InlineData("file.txt", "//file.txt/")]
    public void Zip_AddEntry_RejectsDuplicateAfterFirstEntry(string first, string duplicate)
    {
        using var archive = ZipArchive.CreateArchive();
        using var stream = new MemoryStream();
        archive.AddEntry("other", stream, false);
        archive.AddEntry(first, stream, false);
        Assert.Throws<ArchiveException>(() => archive.AddEntry(duplicate, stream, false));
    }

    [Fact]
    public void Zip_PausedRebuilding_ChecksPendingAndRemovedEntries()
    {
        using var archive = ZipArchive.CreateArchive();
        using var stream = new MemoryStream();
        using (archive.PauseEntryRebuilding())
        {
            var entry = archive.AddEntry("dir/file", stream, false);
            Assert.Throws<ArchiveException>(() => archive.AddEntry("DIR\\FILE", stream, false));
            archive.RemoveEntry(entry);
            archive.AddEntry("dir/file", stream, false);
            archive.AddDirectoryEntry("folder");
            Assert.Throws<ArchiveException>(() => archive.AddDirectoryEntry("FOLDER/"));
        }
        Assert.Equal(2, archive.Entries.Count());
    }

    [Fact]
    public async Task Zip_Async_ChecksPendingEntriesAndExposesModifications()
    {
        await using var archive = await ZipArchive.CreateAsyncArchive();
        using var stream = new MemoryStream();
        await archive.AddEntryAsync("other", stream, false);
        var entry = await archive.AddEntryAsync("dir/file", stream, false);
        await Assert.ThrowsAsync<ArchiveException>(async () =>
            await archive.AddEntryAsync("DIR\\FILE", stream, false)
        );
        Assert.Equal(2, (await archive.EntriesAsync.ToListAsync()).Count);
        await archive.RemoveEntryAsync(entry);
        Assert.Single(await archive.EntriesAsync.ToListAsync());
        await archive.AddEntryAsync("dir/file", stream, false);
        using (archive.PauseEntryRebuilding())
        {
            await archive.AddDirectoryEntryAsync("folder");
            await Assert.ThrowsAsync<ArchiveException>(async () =>
                await archive.AddDirectoryEntryAsync("FOLDER/")
            );
        }
        Assert.Equal(3, (await archive.EntriesAsync.ToListAsync()).Count);
    }

    [Fact]
    public async Task Zip_ExistingEntries_AreCheckedSynchronouslyAndAsynchronously()
    {
        using var data = new MemoryStream();
        using var source = new MemoryStream();
        using (var archive = ZipArchive.CreateArchive())
        {
            archive.AddEntry("other", source, false);
            archive.AddEntry("dir/file", source, false);
            archive.SaveTo(
                data,
                new ZipWriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }
            );
        }
        data.Position = 0;
        using (
            var archive = ZipArchive.OpenArchive(data, new ReaderOptions { LeaveStreamOpen = true })
        )
        {
            Assert.Throws<ArchiveException>(() => archive.AddEntry("DIR\\FILE", source, false));
        }
        data.Position = 0;
        await using (
            var archive = await ZipArchive.OpenAsyncArchive(
                data,
                new ReaderOptions { LeaveStreamOpen = true }
            )
        )
        {
            await Assert.ThrowsAsync<ArchiveException>(async () =>
                await archive.AddEntryAsync("DIR\\FILE", source, false)
            );
            var entry = await archive.EntriesAsync.SingleAsync(x => x.Key == "dir/file");
            using (archive.PauseEntryRebuilding())
            {
                await archive.RemoveEntryAsync(entry);
                var temporary = await archive.AddEntryAsync("temporary", source, false);
                await archive.RemoveEntryAsync(temporary);
                await archive.AddEntryAsync("dir/file", source, false);
                Assert.Equal(2, (await archive.EntriesAsync.ToListAsync()).Count);
            }
            Assert.Equal(2, (await archive.EntriesAsync.ToListAsync()).Count);
            using var output = new MemoryStream();
            await archive.SaveToAsync(
                output,
                new ZipWriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }
            );
            output.Position = 0;
            using var saved = ZipArchive.OpenArchive(output);
            Assert.Equal(2, saved.Entries.Count());
            Assert.DoesNotContain(saved.Entries, x => x.Key == "temporary");
        }
    }
}
