using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test;

public class ArchiveFactoryTests : TestBase
{
    [Theory]
    [InlineData("Zip.deflate.zip", typeof(ZipFactory))]
    [InlineData("Tar.noEmptyDirs.tar", typeof(TarFactory))]
    [InlineData("Rar.rar", typeof(RarFactory))]
    [InlineData("7Zip.nonsolid.7z", typeof(SevenZipFactory))]
    public async ValueTask FindFactoryAsync_String_ReturnsExpectedFactory_Async(
        string archiveName,
        Type expectedFactoryType
    )
    {
        var factory = await ArchiveFactory.FindFactoryAsync<IArchiveFactory>(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName),
            TestContext.Current.CancellationToken
        );

        Assert.IsType(expectedFactoryType, factory);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", typeof(ZipFactory))]
    [InlineData("Tar.noEmptyDirs.tar", typeof(TarFactory))]
    [InlineData("Rar.rar", typeof(RarFactory))]
    [InlineData("7Zip.nonsolid.7z", typeof(SevenZipFactory))]
    public async ValueTask FindFactoryAsync_FileInfo_ReturnsExpectedFactory_Async(
        string archiveName,
        Type expectedFactoryType
    )
    {
        var factory = await ArchiveFactory.FindFactoryAsync<IArchiveFactory>(
            new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, archiveName)),
            TestContext.Current.CancellationToken
        );

        Assert.IsType(expectedFactoryType, factory);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", typeof(ZipFactory))]
    [InlineData("Tar.noEmptyDirs.tar", typeof(TarFactory))]
    public async ValueTask FindFactoryAsync_Stream_PreservesPosition_Async(
        string archiveName,
        Type expectedFactoryType
    )
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 7);
        var startPosition = stream.Position;

        var factory = await ArchiveFactory.FindFactoryAsync<IArchiveFactory>(
            stream,
            TestContext.Current.CancellationToken
        );

        Assert.IsType(expectedFactoryType, factory);
        Assert.Equal(startPosition, stream.Position);
    }

    [Fact]
    public void OpenArchive_StreamCollection_Throws_On_NonSeekable_Stream()
    {
        using var nonSeekable = new ForwardOnlyStream(new MemoryStream());
        using var seekable = new MemoryStream();

        Assert.Throws<ArgumentException>(() => ArchiveFactory.OpenArchive([nonSeekable, seekable]));
    }

    [Fact]
    public async ValueTask OpenAsyncArchive_StreamCollection_Throws_On_NonSeekable_Stream_Async()
    {
        using var nonSeekable = new ForwardOnlyStream(new MemoryStream());
        using var seekable = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ArchiveFactory
                .OpenAsyncArchive(
                    [nonSeekable, seekable],
                    cancellationToken: TestContext.Current.CancellationToken
                )
                .AsTask()
        );
    }

    [Fact]
    public async ValueTask FindFactoryAsync_InvalidData_ThrowsArchiveOperationException_Async()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        await Assert.ThrowsAsync<ArchiveOperationException>(async () =>
            await ArchiveFactory.FindFactoryAsync<IArchiveFactory>(
                stream,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public void OpenArchive_Stream_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);

        Assert.Throws<ArgumentException>(() => ArchiveFactory.OpenArchive(unreadable));
    }

    [Fact]
    public async ValueTask OpenAsyncArchive_Stream_Throws_On_Unreadable_Stream_Async()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ArchiveFactory
                .OpenAsyncArchive(
                    unreadable,
                    cancellationToken: TestContext.Current.CancellationToken
                )
                .AsTask()
        );
    }

    [Theory]
    [InlineData("Zip.deflate.zip")]
    [InlineData("Tar.noEmptyDirs.tar")]
    [InlineData("Rar.rar")]
    [InlineData("7Zip.nonsolid.7z")]
    public void OpenArchive_SingleVolume_VolumeFileName_MatchesPath(string archiveName)
    {
        var archivePath = GetTestArchivePath(archiveName);
        using var archive = ArchiveFactory.OpenArchive(archivePath);

        var volume = Assert.Single(archive.Volumes);
        Assert.Equal(archivePath, volume.FileName);
    }

    private MemoryStream CreatePrefixedArchiveStream(string archiveName, int prefixLength)
    {
        var archiveBytes = File.ReadAllBytes(GetTestArchivePath(archiveName));
        var buffer = new byte[prefixLength + archiveBytes.Length];

        archiveBytes.CopyTo(buffer, prefixLength);

        var stream = new MemoryStream(buffer);
        stream.Position = prefixLength;
        return stream;
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
