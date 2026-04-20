using System.IO;
using System.Linq;
using System.Text;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarWriterTests : WriterTests
{
    static TarWriterTests()
    {
#if !NETFRAMEWORK
        //fix issue where these tests could not be ran in isolation
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#endif
    }

    public TarWriterTests()
        : base(ArchiveType.Tar) => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public void Tar_Writer() =>
        Write(
            CompressionType.None,
            "Tar.noEmptyDirs.tar",
            "Tar.noEmptyDirs.tar",
            Encoding.GetEncoding(866)
        );

    [Fact]
    public void Tar_BZip2_Writer() =>
        Write(
            CompressionType.BZip2,
            "Tar.noEmptyDirs.tar.bz2",
            "Tar.noEmptyDirs.tar.bz2",
            Encoding.GetEncoding(866)
        );

    [Fact]
    public void Tar_LZip_Writer() =>
        Write(
            CompressionType.LZip,
            "Tar.noEmptyDirs.tar.lz",
            "Tar.noEmptyDirs.tar.lz",
            Encoding.GetEncoding(866)
        );

    [Fact]
    public void Tar_Rar_Write() =>
        Assert.Throws<InvalidFormatException>(() =>
            Write(CompressionType.Rar, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip")
        );

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tar_Finalize_Archive(bool finalizeArchive)
    {
        using var stream = new MemoryStream();
        using Stream content = File.OpenRead(Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg"));
        using (
            var writer = new TarWriter(
                stream,
                new TarWriterOptions(CompressionType.None, finalizeArchive)
            )
        )
        {
            writer.Write("doesn't matter", content, null);
        }

        var paddedContentWithHeader = (content.Length / 512 * 512) + 512 + 512;
        var expectedStreamLength = finalizeArchive
            ? paddedContentWithHeader + (512 * 2)
            : paddedContentWithHeader;
        Assert.Equal(expectedStreamLength, stream.Length);
    }

    [Fact]
    public void Tar_Ustar_HeaderFormat_WritesShortPath()
    {
        using var stream = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true, TarHeaderWriteFormat.USTAR);
        using (var writer = new TarWriter(stream, options))
        using (var content = new MemoryStream(Encoding.UTF8.GetBytes("hello")))
        {
            writer.Write("dir/file.txt", content, null);
        }

        stream.Position = 0;
        using var archive = TarArchive.OpenArchive(stream);
        Assert.Single(archive.Entries);
        Assert.Equal("dir/file.txt", archive.Entries.Single().Key);
    }

    [Fact]
    public void Tar_Ustar_HeaderFormat_ThrowsForLongPath()
    {
        var longName = new string('a', 160) + ".txt";

        using var stream = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true, TarHeaderWriteFormat.USTAR);
        using var writer = new TarWriter(stream, options);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        Assert.Throws<InvalidFormatException>(() => writer.Write(longName, content, null));
    }

    [Fact]
    public void Tar_GnuLongLink_HeaderFormat_WritesLongPath()
    {
        var longName = new string('a', 160) + ".txt";

        using var stream = new MemoryStream();
        var options = new TarWriterOptions(
            CompressionType.None,
            true,
            TarHeaderWriteFormat.GNU_TAR_LONG_LINK
        );

        using (var writer = new TarWriter(stream, options))
        using (var content = new MemoryStream(Encoding.UTF8.GetBytes("hello")))
        {
            writer.Write(longName, content, null);
        }

        stream.Position = 0;
        using var archive = TarArchive.OpenArchive(stream);
        Assert.Single(archive.Entries);
        Assert.Equal(longName, archive.Entries.Single().Key);
    }
}
