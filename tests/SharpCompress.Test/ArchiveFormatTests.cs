using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test;

public class ArchiveFormatTests : TestBase
{
    public static IEnumerable<object[]> SingleFileTestArchives
    {
        get
        {
            yield return ["64bitstream.zip.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.ARM.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.ARM64.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.ARMT.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.BCJ.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.BCJ2.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.BZip2.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.Copy.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.EmptyStream.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.Filters.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.IA64.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.LZMA.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.LZMA.Aes.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.LZMA2.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.LZMA2.Aes.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.PPC.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.PPMd.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.RISCV.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.SPARC.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.Tar.tar", ArchiveType.Tar, true];
            yield return ["7Zip.Tar.tar.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.ZSTD.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.delta.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.delta.distance.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.encryptedFiles.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.eos.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.nonsolid.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.solid.1block.7z", ArchiveType.SevenZip, true];
            yield return ["7Zip.solid.7z", ArchiveType.SevenZip, true];
            yield return ["Ace.encrypted.ace", ArchiveType.Ace, false];
            yield return ["Ace.method1-solid.ace", ArchiveType.Ace, false];
            yield return ["Ace.method1.ace", ArchiveType.Ace, false];
            yield return ["Ace.method2-solid.ace", ArchiveType.Ace, false];
            yield return ["Ace.method2.ace", ArchiveType.Ace, false];
            yield return ["Ace.store.ace", ArchiveType.Ace, false];
            yield return ["Ace.store.largefile.ace", ArchiveType.Ace, false];
            yield return ["Arc.crunched.arc", ArchiveType.Arc, false];
            yield return ["Arc.crunched.largefile.arc", ArchiveType.Arc, false];
            yield return ["Arc.squashed.arc", ArchiveType.Arc, false];
            yield return ["Arc.squashed.largefile.arc", ArchiveType.Arc, false];
            yield return ["Arc.squeezed.arc", ArchiveType.Arc, false];
            yield return ["Arc.squeezed.largefile.arc", ArchiveType.Arc, false];
            yield return ["Arc.uncompressed.arc", ArchiveType.Arc, false];
            yield return ["Arc.uncompressed.largefile.arc", ArchiveType.Arc, false];
            yield return ["Arj.encrypted.arj", ArchiveType.Arj, false];
            yield return ["Arj.method1.arj", ArchiveType.Arj, false];
            yield return ["Arj.method1.largefile.arj", ArchiveType.Arj, false];
            yield return ["Arj.method2.arj", ArchiveType.Arj, false];
            yield return ["Arj.method2.largefile.arj", ArchiveType.Arj, false];
            yield return ["Arj.method3.arj", ArchiveType.Arj, false];
            yield return ["Arj.method3.largefile.arj", ArchiveType.Arj, false];
            yield return ["Arj.method4.arj", ArchiveType.Arj, false];
            yield return ["Arj.method4.largefile.arj", ArchiveType.Arj, false];
            yield return ["Arj.store.arj", ArchiveType.Arj, false];
            yield return ["Arj.store.largefile.arj", ArchiveType.Arj, false];
            yield return ["Issue_685.zip", ArchiveType.Zip, true];
            yield return ["PrePostHeaders.zip", ArchiveType.Zip, true];
            yield return ["Rar.Audio_program.rar", ArchiveType.Rar, true];
            yield return ["Rar.Encrypted.rar", ArchiveType.Rar, true];
            yield return ["Rar.comment.rar", ArchiveType.Rar, true];
            yield return ["Rar.encrypted_filesAndHeader.rar", ArchiveType.Rar, true];
            yield return ["Rar.encrypted_filesOnly.rar", ArchiveType.Rar, true];
            yield return ["Rar.issue1050.rar", ArchiveType.Rar, true];
            yield return ["Rar.malformed_512byte.rar", ArchiveType.Rar, true];
            yield return ["Rar.none.rar", ArchiveType.Rar, true];
            yield return ["Rar.rar", ArchiveType.Rar, true];
            yield return ["Rar.solid.rar", ArchiveType.Rar, true];
            yield return ["Rar.test_invalid_exttime.rar", ArchiveType.Rar, true];
            yield return ["Rar15.rar", ArchiveType.Rar, true];
            yield return ["Rar2.rar", ArchiveType.Rar, true];
            yield return ["Rar4.rar", ArchiveType.Rar, true];
            yield return ["Rar5.comment.rar", ArchiveType.Rar, true];
            yield return ["Rar5.crc_blake2.rar", ArchiveType.Rar, true];
            yield return ["Rar5.encrypted_filesAndHeader.rar", ArchiveType.Rar, true];
            yield return ["Rar5.encrypted_filesOnly.rar", ArchiveType.Rar, true];
            yield return ["Rar5.none.rar", ArchiveType.Rar, true];
            yield return ["Rar5.rar", ArchiveType.Rar, true];
            yield return ["Rar5.solid.rar", ArchiveType.Rar, true];
            yield return ["Tar.ContainsRar.tar", ArchiveType.Tar, true];
            yield return ["Tar.ContainsTarGz.tar", ArchiveType.Tar, true];
            yield return ["Tar.Empty.tar", ArchiveType.Tar, true];
            yield return ["Tar.LongPathsWithLongNameExtension.tar", ArchiveType.Tar, true];
            yield return ["Tar.mod.tar", ArchiveType.Tar, true];
            yield return ["Tar.noEmptyDirs.tar", ArchiveType.Tar, true];
            yield return ["Tar.tar", ArchiveType.Tar, true];
            yield return ["TarCorrupted.tar", ArchiveType.Tar, true];
            yield return ["WinZip26.zip", ArchiveType.Zip, true];
            yield return ["WinZip26_BZip2.zipx", ArchiveType.Zip, true];
            yield return ["WinZip26_LZMA.zipx", ArchiveType.Zip, true];
            yield return ["WinZip27_XZ.zipx", ArchiveType.Zip, true];
            yield return ["WinZip27_ZSTD.zipx", ArchiveType.Zip, true];
            yield return ["Zip.644.zip", ArchiveType.Zip, true];
            yield return ["Zip.EntryComment.zip", ArchiveType.Zip, true];
            yield return ["Zip.Evil.zip", ArchiveType.Zip, true];
            yield return ["Zip.LongComment.zip", ArchiveType.Zip, true];
            yield return ["Zip.UnicodePathExtra.zip", ArchiveType.Zip, true];
            yield return ["Zip.badlocalextra.zip", ArchiveType.Zip, true];
            yield return ["Zip.bzip2.dd.zip", ArchiveType.Zip, true];
            yield return ["Zip.bzip2.noEmptyDirs.zip", ArchiveType.Zip, true];
            yield return ["Zip.bzip2.pkware.zip", ArchiveType.Zip, true];
            yield return ["Zip.bzip2.zip", ArchiveType.Zip, true];
            yield return ["Zip.deflate.WinzipAES.zip", ArchiveType.Zip, true];
            yield return ["Zip.deflate.WinzipAES2.zip", ArchiveType.Zip, true];
            yield return ["Zip.deflate.dd-.zip", ArchiveType.Zip, true];
            yield return ["Zip.deflate.dd.zip", ArchiveType.Zip, true];
            yield return ["Zip.deflate.mod.zip", ArchiveType.Zip, true];
            yield return ["Zip.deflate.mod2.zip", ArchiveType.Zip, true];
            yield return ["Zip.deflate.noEmptyDirs.zip", ArchiveType.Zip, true];
            yield return ["Zip.deflate.pkware.zip", ArchiveType.Zip, true];
            yield return ["Zip.deflate.zip", ArchiveType.Zip, true];
            yield return ["Zip.deflate64.zip", ArchiveType.Zip, true];
            yield return ["Zip.implode.zip", ArchiveType.Zip, true];
            yield return ["Zip.lzma.WinzipAES.zip", ArchiveType.Zip, true];
            yield return ["Zip.lzma.dd.zip", ArchiveType.Zip, true];
            yield return ["Zip.lzma.empty.zip", ArchiveType.Zip, true];
            yield return ["Zip.lzma.noEmptyDirs.zip", ArchiveType.Zip, true];
            yield return ["Zip.lzma.zip", ArchiveType.Zip, true];
            yield return ["Zip.none.datadescriptors.zip", ArchiveType.Zip, true];
            yield return ["Zip.none.encrypted.zip", ArchiveType.Zip, true];
            yield return ["Zip.none.issue86.zip", ArchiveType.Zip, true];
            yield return ["Zip.none.noEmptyDirs.zip", ArchiveType.Zip, true];
            yield return ["Zip.none.zip", ArchiveType.Zip, true];
            yield return ["Zip.ppmd.dd.zip", ArchiveType.Zip, true];
            yield return ["Zip.ppmd.noEmptyDirs.zip", ArchiveType.Zip, true];
            yield return ["Zip.ppmd.zip", ArchiveType.Zip, true];
            yield return ["Zip.reduce1.zip", ArchiveType.Zip, true];
            yield return ["Zip.reduce2.zip", ArchiveType.Zip, true];
            yield return ["Zip.reduce3.zip", ArchiveType.Zip, true];
            yield return ["Zip.reduce4.zip", ArchiveType.Zip, true];
            yield return ["Zip.shrink.zip", ArchiveType.Zip, true];
            yield return ["Zip.uncompressed.zip", ArchiveType.Zip, true];
            yield return ["Zip.zip64.compressedonly.zip", ArchiveType.Zip, true];
            yield return ["Zip.zip64.zip", ArchiveType.Zip, true];
            yield return ["Zip.zipx", ArchiveType.Zip, true];
            yield return ["Zip.zstd.WinzipAES.mixed.zip", ArchiveType.Zip, true];
            yield return ["large_test.txt.Z", ArchiveType.Lzw, false];
            yield return ["test_477.zip", ArchiveType.Zip, true];
            yield return ["ustar with long names.tar", ArchiveType.Tar, true];
            yield return ["very long filename.tar", ArchiveType.Tar, true];
            yield return ["zipcrypto.zip", ArchiveType.Zip, true];
            yield return ["SharpCompress.AES.zip", ArchiveType.Zip, true];
            yield return ["SharpCompress.Encrypted.zip", ArchiveType.Zip, true];
            yield return ["SharpCompress.Encrypted2.zip", ArchiveType.Zip, true];
        }
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    [InlineData("Rar.rar", ArchiveType.Rar)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip)]
    public void IsArchive_String_ReturnsExpectedType(string archiveName, ArchiveType expectedType)
    {
        var result = ArchiveFormat.IsArchive(
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

        var result = ArchiveFormat.IsArchive(stream, out var type);

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
        var result = ArchiveFormat.IsArchive(
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
        var result = await ArchiveFormat.IsArchiveAsync(
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
        var result = await ArchiveFormat.IsArchiveAsync(
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

        var result = await ArchiveFormat.IsArchiveAsync(stream);

        Assert.True(result.IsArchive);
        Assert.Equal(expectedType, result.Type);
        Assert.Equal(startPosition, stream.Position);
    }

    [Fact]
    public async ValueTask IsArchiveAsync_InvalidData_ReturnsFalseAndNullType()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        var result = await ArchiveFormat.IsArchiveAsync(stream);

        Assert.False(result.IsArchive);
        Assert.Null(result.Type);
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip, null, true)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar, CompressionType.None, true)]
    [InlineData("Rar.rar", ArchiveType.Rar, null, true)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip, null, true)]
    [InlineData("Ace.store.ace", ArchiveType.Ace, null, false)]
    [InlineData("Arc.uncompressed.arc", ArchiveType.Arc, null, false)]
    [InlineData("large_test.txt.Z", ArchiveType.Lzw, CompressionType.Lzw, false)]
    public void GetInfo_ReturnsExpectedInfo(
        string archiveName,
        ArchiveType expectedType,
        CompressionType? expectedCompressionType,
        bool expectedRandomAccess
    )
    {
        var info = ArchiveFormat.GetInfo(Path.Combine(TEST_ARCHIVES_PATH, archiveName));

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(expectedCompressionType, info.CompressionType);
        Assert.Equal(expectedRandomAccess, info.SupportsRandomAccess);
    }

    [Theory]
    [InlineData("7Zip.LZMA2.exe", ArchiveType.SevenZip, true)]
    [InlineData("Rar.jpeg.jpg", ArchiveType.Rar, true)]
    public void GetInfo_WithReaderOptions_ReturnsExpectedInfo(
        string archiveName,
        ArchiveType expectedType,
        bool expectedRandomAccess
    )
    {
        var info = ArchiveFormat.GetInfo(
            GetTestArchivePath(archiveName),
            ReaderOptions.ForFilePath.WithLookForHeader(true)
        );

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(expectedRandomAccess, info.SupportsRandomAccess);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip, null, true)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar, CompressionType.None, true)]
    [InlineData("Rar.rar", ArchiveType.Rar, null, true)]
    [InlineData("7Zip.nonsolid.7z", ArchiveType.SevenZip, null, true)]
    [InlineData("Ace.store.ace", ArchiveType.Ace, null, false)]
    [InlineData("Arc.uncompressed.arc", ArchiveType.Arc, null, false)]
    [InlineData("large_test.txt.Z", ArchiveType.Lzw, CompressionType.Lzw, false)]
    public async ValueTask GetInfoAsync_ReturnsExpectedInfo(
        string archiveName,
        ArchiveType expectedType,
        CompressionType? expectedCompressionType,
        bool expectedRandomAccess
    )
    {
        var info = await ArchiveFormat.GetInfoAsync(Path.Combine(TEST_ARCHIVES_PATH, archiveName));

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(expectedCompressionType, info.CompressionType);
        Assert.Equal(expectedRandomAccess, info.SupportsRandomAccess);
    }

    [Theory]
    [InlineData("7Zip.LZMA2.exe", ArchiveType.SevenZip, true)]
    [InlineData("Rar.jpeg.jpg", ArchiveType.Rar, true)]
    public async ValueTask GetInfoAsync_WithReaderOptions_ReturnsExpectedInfo(
        string archiveName,
        ArchiveType expectedType,
        bool expectedRandomAccess
    )
    {
        var info = await ArchiveFormat.GetInfoAsync(
            GetTestArchivePath(archiveName),
            ReaderOptions.ForFilePath.WithLookForHeader(true)
        );

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(expectedRandomAccess, info.SupportsRandomAccess);
    }

    [Theory]
    [MemberData(nameof(SingleFileTestArchives))]
    public void GetInfo_DetectsSingleFileTestArchives(
        string archiveName,
        ArchiveType expectedType,
        bool expectedSeekable
    )
    {
        var info = ArchiveFormat.GetInfo(GetTestArchivePath(archiveName));

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(expectedSeekable, info.SupportsRandomAccess);
    }

    [Theory]
    [MemberData(nameof(SingleFileTestArchives))]
    public async ValueTask GetInfoAsync_DetectsSingleFileTestArchives(
        string archiveName,
        ArchiveType expectedType,
        bool expectedSeekable
    )
    {
        var info = await ArchiveFormat.GetInfoAsync(GetTestArchivePath(archiveName));

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(expectedSeekable, info.SupportsRandomAccess);
    }

    [Fact]
    public void GetInfo_ReturnsNull_ForNonArchive()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        var info = ArchiveFormat.GetInfo(stream);

        Assert.Null(info);
    }

    [Fact]
    public async ValueTask GetInfoAsync_ReturnsNull_ForNonArchive()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not an archive"));

        var info = await ArchiveFormat.GetInfoAsync(stream);

        Assert.Null(info);
    }

    [Theory]
    [InlineData("Tar.tar.gz")]
    [InlineData("Tar.tar.Z")]
    public void IsArchive_ReturnsTrue_ForCompressedTar(string archiveName)
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));

        var isArchive = ArchiveFormat.IsArchive(stream, out var archiveType);

        Assert.True(isArchive);
        Assert.Equal(ArchiveType.Tar, archiveType);
    }

    [Theory]
    [InlineData("Tar.tar.gz")]
    [InlineData("Tar.tar.Z")]
    public async ValueTask IsArchiveAsync_ReturnsTrue_ForCompressedTar(string archiveName)
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));

        var (isArchive, archiveType) = await ArchiveFormat.IsArchiveAsync(stream);

        Assert.True(isArchive);
        Assert.Equal(ArchiveType.Tar, archiveType);
    }

    [Theory]
    [InlineData("Tar.tar.gz", CompressionType.GZip)]
    [InlineData("Tar.tar.Z", CompressionType.Lzw)]
    public void GetInfo_ReturnsReaderOnlyTar_ForCompressedTar(
        string archiveName,
        CompressionType expectedCompressionType
    )
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));

        var info = ArchiveFormat.GetInfo(stream);

        Assert.NotNull(info);
        Assert.Equal(ArchiveType.Tar, info.Type);
        Assert.Equal(expectedCompressionType, info.CompressionType);
        Assert.False(info.SupportsRandomAccess);
    }

    [Theory]
    [InlineData("Tar.tar.gz", CompressionType.GZip)]
    [InlineData("Tar.tar.Z", CompressionType.Lzw)]
    public async ValueTask GetInfoAsync_ReturnsReaderOnlyTar_ForCompressedTar(
        string archiveName,
        CompressionType expectedCompressionType
    )
    {
        using var stream = File.OpenRead(GetTestArchivePath(archiveName));

        var info = await ArchiveFormat.GetInfoAsync(stream);

        Assert.NotNull(info);
        Assert.Equal(ArchiveType.Tar, info.Type);
        Assert.Equal(expectedCompressionType, info.CompressionType);
        Assert.False(info.SupportsRandomAccess);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    public void GetInfo_Stream_PreservesPosition(string archiveName, ArchiveType expectedType)
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 13);
        var startPosition = stream.Position;

        var info = ArchiveFormat.GetInfo(stream);

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(startPosition, stream.Position);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", ArchiveType.Zip)]
    [InlineData("Tar.noEmptyDirs.tar", ArchiveType.Tar)]
    public async ValueTask GetInfoAsync_Stream_PreservesPosition(
        string archiveName,
        ArchiveType expectedType
    )
    {
        using var stream = CreatePrefixedArchiveStream(archiveName, 13);
        var startPosition = stream.Position;

        var info = await ArchiveFormat.GetInfoAsync(stream);

        Assert.NotNull(info);
        Assert.Equal(expectedType, info.Type);
        Assert.Equal(startPosition, stream.Position);
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
