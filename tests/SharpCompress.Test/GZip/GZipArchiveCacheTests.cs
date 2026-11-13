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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Completeness_Uses_The_Current_Entry_Cache(bool useAsync, bool partial)
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        using var archive = GZipArchive.OpenArchive(input, ReaderOptions.ForExternalStream);
        var asyncArchive = (IAsyncArchive)archive;
        if (partial)
        {
            if (useAsync)
            {
                await asyncArchive.EntriesAsync.FirstAsync();
            }
            else
            {
                _ = archive.Entries.First();
            }
        }
        Assert.True(useAsync ? await asyncArchive.IsCompleteAsync() : archive.IsComplete);
        var entry = Assert.Single(archive.Entries);
        Assert.Same(entry, Assert.Single(await asyncArchive.EntriesAsync.ToListAsync()));
        Assert.True(archive.IsComplete);
        Assert.True(await asyncArchive.IsCompleteAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completeness_Does_Not_Reparse_Already_Cached_Entries(bool useAsync)
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        using var archive = GZipArchive.OpenArchive(input, ReaderOptions.ForExternalStream);
        var asyncArchive = (IAsyncArchive)archive;
        if (useAsync)
        {
            Assert.Single(await asyncArchive.EntriesAsync.ToListAsync());
        }
        else
        {
            Assert.Single(archive.Entries);
        }
        // Completeness needs only cached metadata and must not move the source.
        input.Position = input.Length;
        Assert.True(useAsync ? await asyncArchive.IsCompleteAsync() : archive.IsComplete);
        Assert.Equal(input.Length, input.Position);
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

    [Fact]
    public async Task Resuming_And_Checking_Completeness_Do_Not_Use_Synchronous_Io()
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        using var asyncOnly = new AsyncOnlyStream(input, disposeStream: false);
        await using (
            var archive = await GZipArchive.OpenAsyncArchive(
                asyncOnly,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            )
        )
        {
            var entry = await archive.EntriesAsync.FirstAsync();
            Assert.True(await archive.IsCompleteAsync());
            Assert.Same(entry, Assert.Single(await archive.EntriesAsync.ToListAsync()));
            using var payload = await entry.OpenEntryStreamAsync(CancellationToken.None);
            await payload.CopyToAsync(Stream.Null, 81920, CancellationToken.None);
        }
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task Cancelling_An_Enumeration_Does_Not_Poison_The_Shared_Cache()
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        await using var archive = await GZipArchive.OpenAsyncArchive(
            input,
            ReaderOptions.ForExternalStream,
            CancellationToken.None
        );
        using var cancellation = new CancellationTokenSource();
        await using (var entries = archive.EntriesAsync.GetAsyncEnumerator(cancellation.Token))
        {
            Assert.True(await entries.MoveNextAsync());
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<System.OperationCanceledException>(async () =>
                await entries.MoveNextAsync()
            );
        }
        Assert.Single(await archive.EntriesAsync.ToListAsync());
    }
}
