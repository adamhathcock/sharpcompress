using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipArchiveCacheTests : TestBase
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task Partial_Enumeration_Can_Be_Resumed_Through_Either_Api(
        bool asyncFirst,
        bool asyncNext,
        bool prefixed
    )
    {
        using var input = new MemoryStream();
        var start = prefixed ? 17 : 0;
        input.Write(new byte[start], 0, start);
        using (var fixture = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz")))
        {
            fixture.CopyTo(input);
        }
        input.Position = start;
        using var archive = GZipArchive.OpenArchive(input, ReaderOptions.ForExternalStream);
        var asyncArchive = (IAsyncArchive)archive;
        var first = asyncFirst
            ? await asyncArchive.EntriesAsync.FirstAsync()
            : archive.Entries.First();
        var entries = asyncNext
            ? await asyncArchive.EntriesAsync.ToListAsync()
            : archive.Entries.ToList();
        Assert.Same(first, Assert.Single(entries));
        Assert.Same(first, Assert.Single(archive.Entries));
        Assert.Same(first, Assert.Single(await asyncArchive.EntriesAsync.ToListAsync()));
        using var payload = first.OpenEntryStream();
        Assert.True(payload.ReadByte() >= 0);
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Extraction_Does_Not_Invalidate_A_Partially_Consumed_Loader(bool asyncFirst)
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        using var archive = GZipArchive.OpenArchive(input, ReaderOptions.ForExternalStream);
        var asyncArchive = (IAsyncArchive)archive;
        var first = asyncFirst
            ? await asyncArchive.EntriesAsync.FirstAsync()
            : archive.Entries.First();
        if (asyncFirst)
        {
            using var payload = await first.OpenEntryStreamAsync(CancellationToken.None);
            await payload.CopyToAsync(Stream.Null, 81920, CancellationToken.None);
        }
        else
        {
            using var payload = first.OpenEntryStream();
            payload.CopyTo(Stream.Null);
        }
        var entries = asyncFirst
            ? archive.Entries.ToList()
            : await asyncArchive.EntriesAsync.ToListAsync();
        Assert.Same(first, Assert.Single(entries));
    }
}
