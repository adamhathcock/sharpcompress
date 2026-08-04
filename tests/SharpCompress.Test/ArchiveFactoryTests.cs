using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;
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

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    [InlineData("Rar.rar", ArchiveType.Rar)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip)]
    public void IsArchive_String_ReturnsExpectedType(string archiveName, ArchiveType expectedType)
    {
        var result = ArchiveFactory.IsArchive(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName),
            out var type
        );

        Assert.True(result);
        Assert.Equal(expectedType, type);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    public void IsArchive_Stream_PreservesPosition(string archiveName, ArchiveType expectedType)
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 11);
        var startPosition = stream.Position;

        var result = ArchiveFactory.IsArchive(stream, out var type);

        Assert.True(result);
        Assert.Equal(expectedType, type);
        Assert.Equal(startPosition, stream.Position);
    }

    [Theory]
    [InlineData("7Zip.LZMA2.exe", ArchiveType.SevenZip)]
    [InlineData("Rar.jpeg.jpg", ArchiveType.Rar)]
    public void IsArchive_WithReaderOptions_ReturnsExpectedType(
        string archiveName,
        ArchiveType expectedType
    )
    {
        var result = ArchiveFactory.IsArchive(
            GetTestArchivePath(archiveName),
            ReaderOptions.ForFilePath.WithLookForHeader(true),
            out var type
        );

        Assert.True(result);
        Assert.Equal(expectedType, type);
    }

    [Theory]
    [InlineData("7Zip.LZMA2.exe", ArchiveType.SevenZip)]
    [InlineData("Rar.jpeg.jpg", ArchiveType.Rar)]
    public async ValueTask IsArchiveAsync_WithReaderOptions_ReturnsExpectedType(
        string archiveName,
        ArchiveType expectedType
    )
    {
        var result = await ArchiveFactory.IsArchiveAsync(
            GetTestArchivePath(archiveName),
            ReaderOptions.ForFilePath.WithLookForHeader(true)
        );

        Assert.True(result.IsArchive);
        Assert.Equal(expectedType, result.Type);
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
    public void DetectArchive_ReturnsExpectedInfo(
        string archiveName,
        ArchiveType expectedType,
        bool expectedRandomAccess
    )
    {
        var info = ArchiveFactory.DetectArchive(Path.Combine(TEST_ARCHIVES_PATH, archiveName));

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.ContainerType);
        Assert.Equal(expectedRandomAccess, info.SupportedApis.HasFlag(ArchiveAccessMode.Archive));
    }

    [Theory]
    [InlineData("7Zip.LZMA2.exe", ArchiveType.SevenZip, true)]
    [InlineData("Rar.jpeg.jpg", ArchiveType.Rar, true)]
    public void DetectArchive_WithReaderOptions_ReturnsExpectedInfo(
        string archiveName,
        ArchiveType expectedType,
        bool expectedRandomAccess
    )
    {
        var info = ArchiveFactory.DetectArchive(
            GetTestArchivePath(archiveName),
            ReaderOptions.ForFilePath.WithLookForHeader(true)
        );

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.ContainerType);
        Assert.Equal(expectedRandomAccess, info.SupportedApis.HasFlag(ArchiveAccessMode.Archive));
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip, true)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar, true)]
    [InlineData("Rar.rar", ArchiveType.Rar, true)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip, true)]
    [InlineData("Ace.store.ace", ArchiveType.Ace, false)]
    [InlineData("Arc.uncompressed.arc", ArchiveType.Arc, false)]
    public async ValueTask DetectArchiveAsync_ReturnsExpectedInfo(
        string archiveName,
        ArchiveType expectedType,
        bool expectedRandomAccess
    )
    {
        var info = await ArchiveFactory.DetectArchiveAsync(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName)
        );

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.ContainerType);
        Assert.Equal(expectedRandomAccess, info.SupportedApis.HasFlag(ArchiveAccessMode.Archive));
    }

    [Theory]
    [InlineData("7Zip.LZMA2.exe", ArchiveType.SevenZip, true)]
    [InlineData("Rar.jpeg.jpg", ArchiveType.Rar, true)]
    public async ValueTask DetectArchiveAsync_WithReaderOptions_ReturnsExpectedInfo(
        string archiveName,
        ArchiveType expectedType,
        bool expectedRandomAccess
    )
    {
        var info = await ArchiveFactory.DetectArchiveAsync(
            GetTestArchivePath(archiveName),
            ReaderOptions.ForFilePath.WithLookForHeader(true)
        );

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.ContainerType);
        Assert.Equal(expectedRandomAccess, info.SupportedApis.HasFlag(ArchiveAccessMode.Archive));
    }

    [Theory]
    [InlineData("64bitstream.zip.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.ARM.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.ARM64.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.ARMT.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.BCJ.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.BCJ2.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.BZip2.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.Copy.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.EmptyStream.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.Filters.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.IA64.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.LZMA.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.LZMA.Aes.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.LZMA2.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.LZMA2.Aes.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.PPC.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.PPMd.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.RISCV.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.SPARC.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.Tar.tar", ArchiveType.Tar, true)]
    [InlineData("7Zip.Tar.tar.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.ZSTD.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.delta.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.delta.distance.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.encryptedFiles.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.eos.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.solid.1block.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.solid.7z", ArchiveType.SevenZip, true)]
    [InlineData("Ace.encrypted.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.method1-solid.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.method1.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.method2-solid.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.method2.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.store.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.store.largefile.ace", ArchiveType.Ace, false)]
    [InlineData("Arc.crunched.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.crunched.largefile.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.squashed.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.squashed.largefile.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.squeezed.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.squeezed.largefile.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.uncompressed.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.uncompressed.largefile.arc", ArchiveType.Arc, false)]
    [InlineData("Arj.encrypted.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method1.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method1.largefile.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method2.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method2.largefile.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method3.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method3.largefile.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method4.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method4.largefile.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.store.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.store.largefile.arj", ArchiveType.Arj, false)]
    [InlineData("Issue_685.zip", ArchiveType.Zip, true)]
    [InlineData("PrePostHeaders.zip", ArchiveType.Zip, true)]
    [InlineData("Rar.Audio_program.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.Encrypted.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.comment.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.encrypted_filesAndHeader.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.encrypted_filesOnly.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.issue1050.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.malformed_512byte.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.none.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.solid.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.test_invalid_exttime.rar", ArchiveType.Rar, true)]
    [InlineData("Rar15.rar", ArchiveType.Rar, true)]
    [InlineData("Rar2.rar", ArchiveType.Rar, true)]
    [InlineData("Rar4.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.comment.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.crc_blake2.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.encrypted_filesAndHeader.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.encrypted_filesOnly.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.none.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.solid.rar", ArchiveType.Rar, true)]
    [InlineData("Tar.ContainsRar.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.ContainsTarGz.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.Empty.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.LongPathsWithLongNameExtension.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.mod.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.tar", ArchiveType.Tar, true)]
    [InlineData("TarCorrupted.tar", ArchiveType.Tar, true)]
    [InlineData("WinZip26.zip", ArchiveType.Zip, true)]
    [InlineData("WinZip26_BZip2.zipx", ArchiveType.Zip, true)]
    [InlineData("WinZip26_LZMA.zipx", ArchiveType.Zip, true)]
    [InlineData("WinZip27_XZ.zipx", ArchiveType.Zip, true)]
    [InlineData("WinZip27_ZSTD.zipx", ArchiveType.Zip, true)]
    [InlineData("Zip.644.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.EntryComment.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.Evil.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.LongComment.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.UnicodePathExtra.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.badlocalextra.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.bzip2.dd.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.bzip2.noEmptyDirs.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.bzip2.pkware.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.bzip2.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.WinzipAES.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.WinzipAES2.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.dd-.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.dd.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.mod.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.mod2.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.noEmptyDirs.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.pkware.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate64.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.implode.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.lzma.WinzipAES.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.lzma.dd.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.lzma.empty.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.lzma.noEmptyDirs.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.lzma.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.none.datadescriptors.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.none.encrypted.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.none.issue86.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.none.noEmptyDirs.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.none.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.ppmd.dd.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.ppmd.noEmptyDirs.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.ppmd.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.reduce1.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.reduce2.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.reduce3.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.reduce4.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.shrink.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.uncompressed.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.zip64.compressedonly.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.zip64.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.zipx", ArchiveType.Zip, true)]
    [InlineData("Zip.zstd.WinzipAES.mixed.zip", ArchiveType.Zip, true)]
    [InlineData("large_test.txt.Z", ArchiveType.Lzw, false)]
    [InlineData("test_477.zip", ArchiveType.Zip, true)]
    [InlineData("ustar with long names.tar", ArchiveType.Tar, true)]
    [InlineData("very long filename.tar", ArchiveType.Tar, true)]
    [InlineData("zipcrypto.zip", ArchiveType.Zip, true)]
    [InlineData("SharpCompress.AES.zip", ArchiveType.Zip, true)]
    [InlineData("SharpCompress.Encrypted.zip", ArchiveType.Zip, true)]
    [InlineData("SharpCompress.Encrypted2.zip", ArchiveType.Zip, true)]
    public void DetectArchive_DetectsSingleFileTestArchives(
        string archiveName,
        ArchiveType expectedType,
        bool expectedSeekable
    )
    {
        var info = ArchiveFactory.DetectArchive(GetTestArchivePath(archiveName));

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.ContainerType);
        Assert.Equal(expectedSeekable, info.SupportedApis.HasFlag(ArchiveAccessMode.Archive));
    }

    [Theory]
    [InlineData("64bitstream.zip.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.ARM.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.ARM64.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.ARMT.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.BCJ.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.BCJ2.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.BZip2.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.Copy.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.EmptyStream.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.Filters.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.IA64.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.LZMA.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.LZMA.Aes.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.LZMA2.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.LZMA2.Aes.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.PPC.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.PPMd.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.RISCV.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.SPARC.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.Tar.tar", ArchiveType.Tar, true)]
    [InlineData("7Zip.Tar.tar.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.ZSTD.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.delta.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.delta.distance.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.encryptedFiles.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.eos.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.solid.1block.7z", ArchiveType.SevenZip, true)]
    [InlineData("7Zip.solid.7z", ArchiveType.SevenZip, true)]
    [InlineData("Ace.encrypted.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.method1-solid.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.method1.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.method2-solid.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.method2.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.store.ace", ArchiveType.Ace, false)]
    [InlineData("Ace.store.largefile.ace", ArchiveType.Ace, false)]
    [InlineData("Arc.crunched.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.crunched.largefile.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.squashed.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.squashed.largefile.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.squeezed.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.squeezed.largefile.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.uncompressed.arc", ArchiveType.Arc, false)]
    [InlineData("Arc.uncompressed.largefile.arc", ArchiveType.Arc, false)]
    [InlineData("Arj.encrypted.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method1.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method1.largefile.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method2.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method2.largefile.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method3.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method3.largefile.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method4.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.method4.largefile.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.store.arj", ArchiveType.Arj, false)]
    [InlineData("Arj.store.largefile.arj", ArchiveType.Arj, false)]
    [InlineData("Issue_685.zip", ArchiveType.Zip, true)]
    [InlineData("PrePostHeaders.zip", ArchiveType.Zip, true)]
    [InlineData("Rar.Audio_program.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.Encrypted.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.comment.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.encrypted_filesAndHeader.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.encrypted_filesOnly.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.issue1050.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.malformed_512byte.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.none.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.solid.rar", ArchiveType.Rar, true)]
    [InlineData("Rar.test_invalid_exttime.rar", ArchiveType.Rar, true)]
    [InlineData("Rar15.rar", ArchiveType.Rar, true)]
    [InlineData("Rar2.rar", ArchiveType.Rar, true)]
    [InlineData("Rar4.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.comment.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.crc_blake2.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.encrypted_filesAndHeader.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.encrypted_filesOnly.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.none.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.rar", ArchiveType.Rar, true)]
    [InlineData("Rar5.solid.rar", ArchiveType.Rar, true)]
    [InlineData("Tar.ContainsRar.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.ContainsTarGz.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.Empty.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.LongPathsWithLongNameExtension.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.mod.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar, true)]
    [InlineData("Tar.tar", ArchiveType.Tar, true)]
    [InlineData("TarCorrupted.tar", ArchiveType.Tar, true)]
    [InlineData("WinZip26.zip", ArchiveType.Zip, true)]
    [InlineData("WinZip26_BZip2.zipx", ArchiveType.Zip, true)]
    [InlineData("WinZip26_LZMA.zipx", ArchiveType.Zip, true)]
    [InlineData("WinZip27_XZ.zipx", ArchiveType.Zip, true)]
    [InlineData("WinZip27_ZSTD.zipx", ArchiveType.Zip, true)]
    [InlineData("Zip.644.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.EntryComment.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.Evil.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.LongComment.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.UnicodePathExtra.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.badlocalextra.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.bzip2.dd.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.bzip2.noEmptyDirs.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.bzip2.pkware.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.bzip2.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.WinzipAES.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.WinzipAES2.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.dd-.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.dd.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.mod.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.mod2.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.noEmptyDirs.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.pkware.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.deflate64.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.implode.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.lzma.WinzipAES.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.lzma.dd.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.lzma.empty.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.lzma.noEmptyDirs.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.lzma.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.none.datadescriptors.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.none.encrypted.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.none.issue86.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.none.noEmptyDirs.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.none.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.ppmd.dd.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.ppmd.noEmptyDirs.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.ppmd.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.reduce1.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.reduce2.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.reduce3.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.reduce4.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.shrink.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.uncompressed.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.zip64.compressedonly.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.zip64.zip", ArchiveType.Zip, true)]
    [InlineData("Zip.zipx", ArchiveType.Zip, true)]
    [InlineData("Zip.zstd.WinzipAES.mixed.zip", ArchiveType.Zip, true)]
    [InlineData("large_test.txt.Z", ArchiveType.Lzw, false)]
    [InlineData("test_477.zip", ArchiveType.Zip, true)]
    [InlineData("ustar with long names.tar", ArchiveType.Tar, true)]
    [InlineData("very long filename.tar", ArchiveType.Tar, true)]
    [InlineData("zipcrypto.zip", ArchiveType.Zip, true)]
    [InlineData("SharpCompress.AES.zip", ArchiveType.Zip, true)]
    [InlineData("SharpCompress.Encrypted.zip", ArchiveType.Zip, true)]
    [InlineData("SharpCompress.Encrypted2.zip", ArchiveType.Zip, true)]
    public async ValueTask DetectArchiveAsync_DetectsSingleFileTestArchives(
        string archiveName,
        ArchiveType expectedType,
        bool expectedSeekable
    )
    {
        var info = await ArchiveFactory.DetectArchiveAsync(GetTestArchivePath(archiveName));

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.ContainerType);
        Assert.Equal(expectedSeekable, info.SupportedApis.HasFlag(ArchiveAccessMode.Archive));
    }

    [Fact]
    public void DetectArchive_ReturnsNull_ForNonArchive()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        var info = ArchiveFactory.DetectArchive(stream);

        Assert.Null(info);
    }

    [Fact]
    public async ValueTask DetectArchiveAsync_ReturnsNull_ForNonArchive()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        var info = await ArchiveFactory.DetectArchiveAsync(stream);

        Assert.Null(info);
    }

    [Theory]
    [InlineData("Tar.tar.gz")]
    [InlineData("Tar.tar.bz2")]
    [InlineData("Tar.tar.lz")]
    [InlineData("Tar.tar.xz")]
    [InlineData("Tar.tar.zst")]
    [InlineData("Tar.tar.Z")]
    public void IsArchive_ReturnsFalse_ForCompressedTar(string archiveName)
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));

        var isArchive = ArchiveFactory.IsArchive(stream, out var archiveType);

        Assert.False(isArchive);
        Assert.Null(archiveType);
    }

    [Theory]
    [InlineData("Tar.tar.gz")]
    [InlineData("Tar.tar.bz2")]
    [InlineData("Tar.tar.lz")]
    [InlineData("Tar.tar.xz")]
    [InlineData("Tar.tar.zst")]
    [InlineData("Tar.tar.Z")]
    public async ValueTask IsArchiveAsync_ReturnsFalse_ForCompressedTar(string archiveName)
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));

        var (isArchive, archiveType) = await ArchiveFactory.IsArchiveAsync(stream);

        Assert.False(isArchive);
        Assert.Null(archiveType);
    }

    [Theory]
    [InlineData("Tar.tar.gz")]
    [InlineData("Tar.tar.bz2")]
    [InlineData("Tar.tar.lz")]
    [InlineData("Tar.tar.xz")]
    [InlineData("Tar.tar.zst")]
    [InlineData("Tar.tar.Z")]
    public void DetectArchive_RecognizesCompressedTar(string archiveName)
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));

        var info = ArchiveFactory.DetectArchive(stream);

        Assert.NotNull(info);
        Assert.Equal(ArchiveType.Tar, info.ContainerType);
        Assert.NotNull(info.OuterCompressionType);
        Assert.Equal(ArchiveAccessMode.Reader, info.SupportedApis);
    }

    [Theory]
    [InlineData("Tar.tar.gz")]
    [InlineData("Tar.tar.bz2")]
    [InlineData("Tar.tar.lz")]
    [InlineData("Tar.tar.xz")]
    [InlineData("Tar.tar.zst")]
    [InlineData("Tar.tar.Z")]
    public async ValueTask DetectArchiveAsync_RecognizesCompressedTar(string archiveName)
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));

        var info = await ArchiveFactory.DetectArchiveAsync(stream);

        Assert.NotNull(info);
        Assert.Equal(ArchiveType.Tar, info.ContainerType);
        Assert.NotNull(info.OuterCompressionType);
        Assert.Equal(ArchiveAccessMode.Reader, info.SupportedApis);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    public void DetectArchive_Stream_PreservesPosition(string archiveName, ArchiveType expectedType)
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 13);
        var startPosition = stream.Position;

        var info = ArchiveFactory.DetectArchive(stream);

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.ContainerType);
        Assert.Equal(startPosition, stream.Position);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    public async ValueTask DetectArchiveAsync_Stream_PreservesPosition(
        string archiveName,
        ArchiveType expectedType
    )
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 13);
        var startPosition = stream.Position;

        var info = await ArchiveFactory.DetectArchiveAsync(stream);

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.ContainerType);
        Assert.Equal(startPosition, stream.Position);
    }

    [Theory]
    [InlineData("Zip.none.datadescriptors.zip", 2)]
    [InlineData("Zip.deflate.zip", 0)]
    public void InspectArchive_ZipDataDescriptorEntryCount(
        string archiveName,
        long expectedDataDescriptorEntryCount
    )
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));

        var info = ArchiveFactory.InspectArchive(stream);

        Assert.NotNull(info);
        Assert.Equal(ArchiveType.Zip, info.Detection.ContainerType);
        Assert.NotNull(info.Zip);
        Assert.Equal(expectedDataDescriptorEntryCount, info.Zip.DataDescriptorEntryCount);
        Assert.Equal(expectedDataDescriptorEntryCount, info.EntriesWithUnknownSizeCount);
    }

    [Theory]
    [InlineData("7Zip.solid.7z", true, 1)]
    [InlineData("7Zip.nonsolid.7z", false, 0)]
    [InlineData("Rar.rar", false, 0)]
    [InlineData("Rar.solid.rar", true, 1)]
    public void InspectArchive_SolidStreamCount(
        string archiveName,
        bool expectedSolid,
        long expectedSolidStreamCount
    )
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));

        var info = ArchiveFactory.InspectArchive(stream);

        Assert.NotNull(info);
        Assert.Equal(expectedSolid, info.IsSolid);
        Assert.Equal(expectedSolidStreamCount, info.SolidStreamCount);
    }

    [Theory]
    [InlineData("Zip.none.datadescriptors.zip", ArchiveType.Zip)]
    [InlineData("7Zip.solid.7z", ArchiveType.SevenZip)]
    public async ValueTask InspectArchiveAsync_ReturnsMetadata(
        string archiveName,
        ArchiveType expectedType
    )
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));

        var info = await ArchiveFactory.InspectArchiveAsync(stream);

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Detection.ContainerType);
        Assert.Equal(ArchiveInformationStatus.Complete, info.Status);
        Assert.NotNull(info.EntryCount);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    [InlineData("Rar.rar", ArchiveType.Rar)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip)]
    public void InspectArchive_Stream_PreservesPosition(
        string archiveName,
        ArchiveType expectedType
    )
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 13);
        var startPosition = stream.Position;

        var info = ArchiveFactory.InspectArchive(stream);

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Detection.ContainerType);
        Assert.Equal(startPosition, stream.Position);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    [InlineData("Rar.rar", ArchiveType.Rar)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip)]
    public async ValueTask InspectArchiveAsync_Stream_PreservesPosition(
        string archiveName,
        ArchiveType expectedType
    )
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 13);
        var startPosition = stream.Position;

        var info = await ArchiveFactory.InspectArchiveAsync(stream);

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Detection.ContainerType);
        Assert.Equal(startPosition, stream.Position);
    }

    [Fact]
    public void InspectArchive_CompressedTar_ReportsContainerAndWrapper()
    {
        var info = ArchiveFactory.InspectArchive(GetTestArchivePath("Tar.tar.xz"));

        Assert.NotNull(info);
        Assert.Equal(ArchiveType.Tar, info.Detection.ContainerType);
        Assert.Equal(CompressionType.Xz, info.Detection.OuterCompressionType);
        Assert.Equal(ArchiveInformationStatus.Partial, info.Status);
        Assert.Null(info.CompressedPayloadSize);
        Assert.NotNull(info.EntryCount);
    }

    [Theory]
    [InlineData("Ace.method1-solid.ace", true)]
    [InlineData("Ace.method1.ace", false)]
    public void InspectArchive_Ace_ReportsSolidMetadata(string archiveName, bool expectedSolid)
    {
        var info = ArchiveFactory.InspectArchive(GetTestArchivePath(archiveName));

        Assert.NotNull(info);
        Assert.Equal(expectedSolid, info.IsSolid);
        Assert.Equal(expectedSolid ? 1 : 0, info.SolidStreamCount);
    }

    [Fact]
    public void InspectArchive_Arc_ReportsUnavailableUncompressedSizes()
    {
        var info = ArchiveFactory.InspectArchive(GetTestArchivePath("Arc.uncompressed.arc"));

        Assert.NotNull(info);
        Assert.Equal(ArchiveInformationStatus.Partial, info.Status);
        Assert.True(info.Limitations.HasFlag(ArchiveInformationLimitations.UnavailableMetadata));
        Assert.Null(info.UncompressedPayloadSize);
        Assert.True(info.EntriesWithUnknownSizeCount > 0);
    }

    [Theory]
    [InlineData("Arj.store.arj", ArchiveType.Arj, ArchiveInformationStatus.Complete)]
    [InlineData("large_test.txt.Z", ArchiveType.Lzw, ArchiveInformationStatus.Partial)]
    public void InspectArchive_ReaderOnlyFormats_ReturnsMetadata(
        string archiveName,
        ArchiveType expectedType,
        ArchiveInformationStatus expectedStatus
    )
    {
        var info = ArchiveFactory.InspectArchive(GetTestArchivePath(archiveName));

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Detection.ContainerType);
        Assert.Equal(expectedStatus, info.Status);
        Assert.NotNull(info.EntryCount);
    }

    [Fact]
    public void InspectArchive_EncryptedHeaders_ReturnsPartialInformation()
    {
        var info = ArchiveFactory.InspectArchive(
            GetTestArchivePath("Rar.encrypted_filesAndHeader.rar")
        );

        Assert.NotNull(info);
        Assert.Equal(ArchiveInformationStatus.Partial, info.Status);
        Assert.True(info.Limitations.HasFlag(ArchiveInformationLimitations.EncryptedHeaders));
        Assert.Equal(ArchiveEncryptionScope.Headers, info.Encryption);
        Assert.True(info.IsEncrypted);
    }

    [Fact]
    public void InspectArchive_EncryptedHeadersWithPassword_ReportsBothEncryptionScopes()
    {
        var info = ArchiveFactory.InspectArchive(
            GetTestArchivePath("Rar.encrypted_filesAndHeader.rar"),
            ReaderOptions.ForFilePath.WithPassword("test")
        );

        Assert.NotNull(info);
        Assert.Equal(ArchiveInformationStatus.Complete, info.Status);
        Assert.Equal(
            ArchiveEncryptionScope.Headers | ArchiveEncryptionScope.EntryData,
            info.Encryption
        );
        Assert.True(info.IsEncrypted);
    }

    [Fact]
    public void InspectArchive_IncompleteRarStream_ReturnsPartialInformation()
    {
        using var stream = File.OpenRead(GetTestArchivePath("Rar.multi.solid.part01.rar"));

        var info = ArchiveFactory.InspectArchive(stream);

        Assert.NotNull(info);
        Assert.Equal(ArchiveInformationStatus.Partial, info.Status);
        Assert.True(info.Limitations.HasFlag(ArchiveInformationLimitations.MissingVolumes));
        Assert.Null(info.CompressedPayloadSize);
        Assert.Null(info.UncompressedPayloadSize);
    }

    [Fact]
    public async ValueTask InspectArchiveAsync_CompressedTar_ReturnsMetadata()
    {
        var info = await ArchiveFactory.InspectArchiveAsync(GetTestArchivePath("Tar.tar.xz"));

        Assert.NotNull(info);
        Assert.Equal(ArchiveType.Tar, info.Detection.ContainerType);
        Assert.Equal(CompressionType.Xz, info.Detection.OuterCompressionType);
        Assert.NotNull(info.EntryCount);
    }

    [Fact]
    public void InspectArchive_MultiVolumeRar_ReturnsVolumeMetadata()
    {
        FileInfo[] parts =
        [
            new(GetTestArchivePath("Rar.multi.solid.part01.rar")),
            new(GetTestArchivePath("Rar.multi.solid.part02.rar")),
            new(GetTestArchivePath("Rar.multi.solid.part03.rar")),
            new(GetTestArchivePath("Rar.multi.solid.part04.rar")),
            new(GetTestArchivePath("Rar.multi.solid.part05.rar")),
            new(GetTestArchivePath("Rar.multi.solid.part06.rar")),
        ];

        var info = ArchiveFactory.InspectArchive(parts);

        Assert.NotNull(info);
        Assert.Equal(ArchiveType.Rar, info.Detection.ContainerType);
        Assert.Equal(parts.Length, info.PhysicalPartCount);
        Assert.True(info.IsMultiVolume);
    }

    [Fact]
    public void InspectArchive_MultiVolumeRarPath_DiscoversAllParts()
    {
        var info = ArchiveFactory.InspectArchive(GetTestArchivePath("Rar.multi.solid.part01.rar"));

        Assert.NotNull(info);
        Assert.Equal(6, info.PhysicalPartCount);
        Assert.True(info.IsMultiVolume);
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
