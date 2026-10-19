using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Compressors.Filters;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Streams;

public class SevenZipLifetimeTests : TestBase
{
    [Theory]
    [InlineData("7Zip.LZMA.7z", false, false)]
    [InlineData("7Zip.LZMA.7z", false, true)]
    [InlineData("7Zip.LZMA.7z", true, false)]
    [InlineData("7Zip.LZMA.7z", true, true)]
    [InlineData("7Zip.LZMA2.7z", false, false)]
    [InlineData("7Zip.LZMA2.7z", false, true)]
    [InlineData("7Zip.LZMA2.7z", true, false)]
    [InlineData("7Zip.LZMA2.7z", true, true)]
    [InlineData("7Zip.solid.7z", false, false)]
    [InlineData("7Zip.solid.7z", false, true)]
    [InlineData("7Zip.solid.7z", true, false)]
    [InlineData("7Zip.solid.7z", true, true)]
    public async Task Reader_Dispose_Releases_Decoder_And_Preserves_Archive(
        string filename,
        bool asyncDispose,
        bool readAll
    )
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        await using var archive = await SevenZipArchive.OpenAsyncArchive(
            input,
            new ReaderOptions { LeaveStreamOpen = false },
            CancellationToken.None
        );
        await using var reader = await archive.ExtractAllEntriesAsync();
        var sevenZipReader = Assert.IsType<SevenZipArchive.SevenZipReader>(reader);
        sevenZipReader.DiagnosticsEnabled = true;
        while (await reader.MoveToNextEntryAsync(CancellationToken.None))
        {
            if (!reader.Entry.IsDirectory)
            {
#if LEGACY_DOTNET
                using var stream = await reader.OpenEntryStreamAsync(CancellationToken.None);
#else
                await using var stream = await reader.OpenEntryStreamAsync(CancellationToken.None);
#endif
                await stream.CopyToAsync(Stream.Null, Constants.BufferSize, CancellationToken.None);
                if (!readAll)
                {
                    break;
                }
            }
        }

        var folderStream = sevenZipReader.DiagnosticsCurrentFolderStream;
        Assert.NotNull(folderStream);
        while (folderStream is Filter)
        {
            folderStream = (Stream)GetField(folderStream, "_baseStream")!;
        }
        var decoder = Assert.IsType<LzmaStream>(folderStream);
        var window = GetField(decoder, "_outWindow")!;
        Assert.NotNull(GetField(window, "_buffer"));
        try
        {
            if (asyncDispose)
            {
                await reader.DisposeAsync();
                await reader.DisposeAsync();
            }
            else
            {
                sevenZipReader.Dispose();
                sevenZipReader.Dispose();
            }

            Assert.Null(sevenZipReader.DiagnosticsCurrentFolderStream);
            Assert.Null(GetField(window, "_buffer"));
            Assert.True(input.CanRead);

            // Disposing a borrowed reader must not prevent another extraction.
            var entry = await archive.EntriesAsync.Where(entry => !entry.IsDirectory).FirstAsync();
#if LEGACY_DOTNET
            using var entryStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#else
            await using var entryStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#endif
            await entryStream.CopyToAsync(
                Stream.Null,
                Constants.BufferSize,
                CancellationToken.None
            );
        }
        finally
        {
            await archive.DisposeAsync();
        }
        Assert.False(input.CanRead);
    }

    [Theory]
    [InlineData("7Zip.LZMA.7z", false, false)]
    [InlineData("7Zip.LZMA.7z", true, true)]
    [InlineData("7Zip.LZMA2.7z", false, true)]
    [InlineData("7Zip.LZMA2.7z", true, false)]
    [InlineData("7Zip.solid.7z", true, true)]
    public async Task Archive_Async_Dispose_Releases_Cached_Decoder(
        string filename,
        bool leaveOpen,
        bool readAll
    )
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        await using var archive = await SevenZipArchive.OpenAsyncArchive(
            input,
            new ReaderOptions { LeaveStreamOpen = leaveOpen },
            CancellationToken.None
        );
        var entry = await archive.EntriesAsync.Where(e => !e.IsDirectory).FirstAsync();
        var entryStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
        await using (entryStream.DisposeAsyncScope())
        {
            if (readAll)
            {
                await entryStream.CopyToAsync(
                    Stream.Null,
                    Constants.BufferSize,
                    CancellationToken.None
                );
            }
            else
            {
                Assert.True(
                    await entryStream.ReadAsync(new byte[1], 0, 1, CancellationToken.None) > 0
                );
            }
        }
        var database = GetField(archive, "_database")!;
        var folderStream = (Stream)GetField(database, "_cachedFolderStream")!;
        while (folderStream is Filter)
        {
            folderStream = (Stream)GetField(folderStream, "_baseStream")!;
        }
        var decoder = Assert.IsType<LzmaStream>(folderStream);
        var window = GetField(decoder, "_outWindow")!;
        Assert.NotNull(GetField(window, "_buffer"));

        await archive.DisposeAsync();
        await archive.DisposeAsync();
        Assert.Null(GetField(database, "_cachedFolderStream"));
        Assert.Null(GetField(window, "_buffer"));
        Assert.Equal(leaveOpen, input.CanRead);
    }

    private static object? GetField(object instance, string name) =>
        instance
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance);
}
