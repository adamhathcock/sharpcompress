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
    public async ValueTask FindFactoryAsync_String_ReturnsExpectedFactory(
        string archiveName,
        System.Type expectedFactoryType
    )
    {
        var factory = await ArchiveFactory.FindFactoryAsync<IArchiveFactory>(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName)
        );

        Assert.IsType(expectedFactoryType, factory);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", typeof(ZipFactory))]
    [InlineData("Tar.noEmptyDirs.tar", typeof(TarFactory))]
    [InlineData("Rar.rar", typeof(RarFactory))]
    [InlineData("7Zip.nonsolid.7z", typeof(SevenZipFactory))]
    public async ValueTask FindFactoryAsync_FileInfo_ReturnsExpectedFactory(
        string archiveName,
        System.Type expectedFactoryType
    )
    {
        var factory = await ArchiveFactory.FindFactoryAsync<IArchiveFactory>(
            new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, archiveName))
        );

        Assert.IsType(expectedFactoryType, factory);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", typeof(ZipFactory))]
    [InlineData("Tar.noEmptyDirs.tar", typeof(TarFactory))]
    public async ValueTask FindFactoryAsync_Stream_PreservesPosition(
        string archiveName,
        System.Type expectedFactoryType
    )
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 7);
        var startPosition = stream.Position;

        var factory = await ArchiveFactory.FindFactoryAsync<IArchiveFactory>(stream);

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
    public async ValueTask OpenAsyncArchive_StreamCollection_Throws_On_NonSeekable_Stream()
    {
        using var nonSeekable = new ForwardOnlyStream(new MemoryStream());
        using var seekable = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ArchiveFactory.OpenAsyncArchive([nonSeekable, seekable]).AsTask()
        );
    }

    [Fact]
    public async ValueTask FindFactoryAsync_InvalidData_ThrowsArchiveOperationException()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        await Assert.ThrowsAsync<ArchiveOperationException>(async () =>
            await ArchiveFactory.FindFactoryAsync<IArchiveFactory>(stream)
        );
    }

    [Fact]
    public void OpenArchive_Stream_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);

        Assert.Throws<ArgumentException>(() => ArchiveFactory.OpenArchive(unreadable));
    }

    [Fact]
    public async ValueTask OpenAsyncArchive_Stream_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ArchiveFactory.OpenAsyncArchive(unreadable).AsTask()
        );
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    [InlineData("Rar.rar", ArchiveType.Rar)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip)]
    public async ValueTask IsArchiveAsync_String_ReturnsExpectedType(
        string archiveName,
        ArchiveType expectedType
    )
    {
        var result = await ArchiveFactory.IsArchiveAsync(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName)
        );

        Assert.True(result.IsArchive);
        Assert.Equal(expectedType, result.Type);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    public async ValueTask IsArchiveAsync_Stream_PreservesPosition(
        string archiveName,
        ArchiveType expectedType
    )
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 11);
        var startPosition = stream.Position;

        var result = await ArchiveFactory.IsArchiveAsync(stream);

        Assert.True(result.IsArchive);
        Assert.Equal(expectedType, result.Type);
        Assert.Equal(startPosition, stream.Position);
    }

    [Fact]
    public async ValueTask IsArchiveAsync_InvalidData_ReturnsFalseAndNullType()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        var result = await ArchiveFactory.IsArchiveAsync(stream);

        Assert.False(result.IsArchive);
        Assert.Null(result.Type);
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip, true)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar, true)]
    [InlineData("Rar.rar", ArchiveType.Rar, true)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip, true)]
    [InlineData("Ace.store.ace", ArchiveType.Ace, false)]
    [InlineData("Arc.uncompressed.arc", ArchiveType.Arc, false)]
    public void GetArchiveInformation_ReturnsExpectedInfo(
        string archiveName,
        ArchiveType expectedType,
        bool expectedRandomAccess
    )
    {
        var info = ArchiveFactory.GetArchiveInformation(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName)
        );

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(expectedRandomAccess, info.SupportsRandomAccess);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip, true)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar, true)]
    [InlineData("Rar.rar", ArchiveType.Rar, true)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip, true)]
    [InlineData("Ace.store.ace", ArchiveType.Ace, false)]
    [InlineData("Arc.uncompressed.arc", ArchiveType.Arc, false)]
    public async ValueTask GetArchiveInformationAsync_ReturnsExpectedInfo(
        string archiveName,
        ArchiveType expectedType,
        bool expectedRandomAccess
    )
    {
        var info = await ArchiveFactory.GetArchiveInformationAsync(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName)
        );

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(expectedRandomAccess, info.SupportsRandomAccess);
    }

    [Fact]
    public void GetArchiveInformation_ReturnsNull_ForNonArchive()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        var info = ArchiveFactory.GetArchiveInformation(stream);

        Assert.Null(info);
    }

    [Fact]
    public async ValueTask GetArchiveInformationAsync_ReturnsNull_ForNonArchive()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        var info = await ArchiveFactory.GetArchiveInformationAsync(stream);

        Assert.Null(info);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    public void GetArchiveInformation_Stream_PreservesPosition(
        string archiveName,
        ArchiveType expectedType
    )
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 13);
        var startPosition = stream.Position;

        var info = ArchiveFactory.GetArchiveInformation(stream);

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(startPosition, stream.Position);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    public async ValueTask GetArchiveInformationAsync_Stream_PreservesPosition(
        string archiveName,
        ArchiveType expectedType
    )
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 13);
        var startPosition = stream.Position;

        var info = await ArchiveFactory.GetArchiveInformationAsync(stream);

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(startPosition, stream.Position);
    }

    private MemoryStream CreatePrefixedArchiveStream(string archiveName, int prefixLength)
    {
        var archiveBytes = File.ReadAllBytes(Path.Combine(TEST_ARCHIVES_PATH, archiveName));
        var buffer = new byte[prefixLength + archiveBytes.Length];

        archiveBytes.CopyTo(buffer, prefixLength);

        var stream = new MemoryStream(buffer);
        stream.Position = prefixLength;
        return stream;
    }
}
