using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test;

public class ReaderFactoryTests : TestBase
{
    [Fact]
    public void OpenReader_Stream_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);

        Assert.Throws<ArgumentException>(() => ReaderFactory.OpenReader(unreadable));
    }

    [Fact]
    public async ValueTask OpenAsyncReader_Stream_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReaderFactory.OpenAsyncReader(unreadable).AsTask()
        );
    }

    [Fact]
    public void OpenReader_InvalidData_ThrowsInvalidFormatException()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        Assert.Throws<InvalidFormatException>(() => ReaderFactory.OpenReader(stream));
    }

    [Fact]
    public async ValueTask OpenAsyncReader_InvalidData_ThrowsInvalidFormatException()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        await Assert.ThrowsAsync<InvalidFormatException>(() =>
            ReaderFactory.OpenAsyncReader(stream).AsTask()
        );
    }

    [Theory]
    [InlineData("Tar.tar.gz")]
    [InlineData("Tar.tar.Z")]
    public void OpenReader_DetectsCompressedTar(string archiveName)
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));
        using var reader = ReaderFactory.OpenReader(stream);

        Assert.True(reader.MoveToNextEntry());
        Assert.Equal(ArchiveType.Tar, reader.Type);
    }

    [Theory]
    [InlineData("Tar.tar.gz")]
    [InlineData("Tar.tar.Z")]
    public async ValueTask OpenAsyncReader_DetectsCompressedTar(string archiveName)
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));
        await using var reader = await ReaderFactory.OpenAsyncReader(stream);

        Assert.True(await reader.MoveToNextEntryAsync());
        Assert.Equal(ArchiveType.Tar, reader.Type);
    }

    [Fact]
    public void RarReader_StreamCollection_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);
        using var readable = new MemoryStream();

        Assert.Throws<ArgumentException>(() =>
            RarReader.OpenReader([unreadable, readable]).MoveToNextEntry()
        );
    }

    private static string GetTestArchivePath(string archiveName)
    {
        var archivesPath = Path.Combine(TEST_ARCHIVES_PATH, archiveName);
        if (File.Exists(archivesPath))
        {
            return archivesPath;
        }

        return Path.GetFullPath(Path.Combine(TEST_ARCHIVES_PATH, "..", archiveName));
    }
}
