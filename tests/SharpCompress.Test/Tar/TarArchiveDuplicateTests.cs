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
