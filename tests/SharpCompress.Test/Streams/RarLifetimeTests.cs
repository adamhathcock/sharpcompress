using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Compressors.Rar;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using Xunit;

namespace SharpCompress.Test.Streams;

public class RarLifetimeTests : TestBase
{
    [Theory]
    [InlineData("Rar.rar", true)]
    [InlineData("Rar.rar", false)]
    [InlineData("Rar5.rar", true)]
    [InlineData("Rar5.rar", false)]
    public async Task Reader_Async_Dispose_Releases_Unpacker_Window(string filename, bool leaveOpen)
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        await using var reader = await ReaderFactory.OpenAsyncReader(
            input,
            new ReaderOptions { LeaveStreamOpen = leaveOpen },
            CancellationToken.None
        );
        while (await reader.MoveToNextEntryAsync(CancellationToken.None))
        {
            if (!reader.Entry.IsDirectory)
            {
                await reader.WriteEntryToAsync(Stream.Null, CancellationToken.None);
            }
        }

        var property = typeof(RarReader).GetProperty(
            filename == "Rar.rar" ? "UnpackV1" : "UnpackV2017",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        var unpack = ((Lazy<IRarUnpack>)property.GetValue(reader)!).Value;
        var window = GetWindow(unpack, filename);
        Assert.NotNull(window.GetValue(unpack));

        await reader.DisposeAsync();
        await reader.DisposeAsync();
        ((IDisposable)reader).Dispose();

        Assert.Null(window.GetValue(unpack));
        Assert.Equal(leaveOpen, input.CanRead);
    }

    [Theory]
    [InlineData("Rar.rar", true)]
    [InlineData("Rar.rar", false)]
    [InlineData("Rar5.rar", true)]
    [InlineData("Rar5.rar", false)]
    public async Task Archive_Async_Dispose_Releases_Unpacker_Window(
        string filename,
        bool leaveOpen
    )
    {
        using var input = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        await using var archive = await RarArchive.OpenAsyncArchive(
            input,
            new ReaderOptions { LeaveStreamOpen = leaveOpen },
            CancellationToken.None
        );
        await foreach (var entry in archive.EntriesAsync.Where(entry => !entry.IsDirectory))
        {
            var stream = await entry.OpenEntryStreamAsync(CancellationToken.None);
            await using var scope = stream.DisposeAsyncScope().ConfigureAwait(false);
            await stream.CopyToAsync(Stream.Null, Constants.BufferSize, CancellationToken.None);
        }

        var rarArchive = (RarArchive)archive;
        var unpack = (filename == "Rar.rar" ? rarArchive.UnpackV1 : rarArchive.UnpackV2017).Value;
        var window = GetWindow(unpack, filename);
        Assert.NotNull(window.GetValue(unpack));

        await archive.DisposeAsync();
        await archive.DisposeAsync();
        rarArchive.Dispose();

        Assert.Null(window.GetValue(unpack));
        Assert.Equal(leaveOpen, input.CanRead);
    }

    private static FieldInfo GetWindow(IRarUnpack unpack, string filename) =>
        unpack
            .GetType()
            .GetField(
                filename == "Rar.rar" ? "window" : "Window",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;
}
