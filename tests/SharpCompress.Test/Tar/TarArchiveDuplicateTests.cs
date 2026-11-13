using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarArchiveDuplicateTests
{
    [Fact]
    public void Tar_AddEntry_RejectsDuplicateAfterFirstEntry()
    {
        using var archive = TarArchive.CreateArchive();
        using var stream = new MemoryStream();
        archive.AddEntry("other", stream, false);
        archive.AddEntry("dir/file", stream, false);
        Assert.Throws<ArchiveException>(() => archive.AddEntry("DIR\\FILE", stream, false));
        var entry = archive.Entries.Single(x => x.Key == "dir/file");
        archive.RemoveEntry(entry);
        archive.AddEntry("dir/file", stream, false);
    }

    [Theory]
    [InlineData("file.txt", "C:/file.txt", false)]
    [InlineData("file.txt", "//file.txt/", false)]
    [InlineData("dir/file.txt", "DIR\\FILE.TXT", false)]
    [InlineData("file.txt", "C:/file.txt", true)]
    [InlineData("file.txt", "//file.txt/", true)]
    [InlineData("dir/file.txt", "DIR\\FILE.TXT", true)]
    public async Task Tar_AddEntry_Rejects_Writer_Normalized_Duplicates(
        string first,
        string duplicate,
        bool useAsync
    )
    {
        using var stream = new MemoryStream();
        if (useAsync)
        {
            await using var archive = await TarArchive.CreateAsyncArchive();
            await archive.AddEntryAsync(first, stream, false);
            await Assert.ThrowsAsync<ArchiveException>(async () =>
                await archive.AddEntryAsync(duplicate, stream, false)
            );
        }
        else
        {
            using var archive = TarArchive.CreateArchive();
            archive.AddEntry(first, stream, false);
            Assert.Throws<ArchiveException>(() => archive.AddEntry(duplicate, stream, false));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tar_AddDirectory_Rejects_Trailing_Slash_Duplicates(bool useAsync)
    {
        if (useAsync)
        {
            await using var archive = await TarArchive.CreateAsyncArchive();
            await archive.AddDirectoryEntryAsync("folder");
            await Assert.ThrowsAsync<ArchiveException>(async () =>
                await archive.AddDirectoryEntryAsync("folder/")
            );
        }
        else
        {
            using var archive = TarArchive.CreateArchive();
            archive.AddDirectoryEntry("folder");
            Assert.Throws<ArchiveException>(() => archive.AddDirectoryEntry("folder/"));
        }
    }

    [Fact]
    public async Task Tar_Async_ChecksPendingEntriesWhileRebuildingIsPaused()
    {
        await using var archive = await TarArchive.CreateAsyncArchive();
        using var stream = new MemoryStream();
        using (archive.PauseEntryRebuilding())
        {
            await archive.AddEntryAsync("other", stream, false);
            var entry = await archive.AddEntryAsync("dir/file", stream, false);
            await Assert.ThrowsAsync<ArchiveException>(async () =>
                await archive.AddEntryAsync("DIR\\FILE", stream, false)
            );
            await archive.RemoveEntryAsync(entry);
            await archive.AddEntryAsync("dir/file", stream, false);
        }
        Assert.Equal(2, (await archive.EntriesAsync.ToListAsync()).Count);
    }
}
