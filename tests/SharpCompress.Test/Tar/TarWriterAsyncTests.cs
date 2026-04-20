using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarWriterAsyncTests : WriterTests
{
    static TarWriterAsyncTests()
    {
#if !NETFRAMEWORK
        //fix issue where these tests could not be ran in isolation
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#endif
    }

    public TarWriterAsyncTests()
        : base(ArchiveType.Tar) => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async ValueTask Tar_Writer_Async() =>
        await WriteAsync(
            CompressionType.None,
            "Tar.noEmptyDirs.tar",
            "Tar.noEmptyDirs.tar",
            Encoding.GetEncoding(866)
        );

    [Fact]
    public async ValueTask Tar_BZip2_Writer_Async() =>
        await WriteAsync(
            CompressionType.BZip2,
            "Tar.noEmptyDirs.tar.bz2",
            "Tar.noEmptyDirs.tar.bz2",
            Encoding.GetEncoding(866)
        );

    [Fact]
    public async ValueTask Tar_LZip_Writer_Async() =>
        await WriteAsync(
            CompressionType.LZip,
            "Tar.noEmptyDirs.tar.lz",
            "Tar.noEmptyDirs.tar.lz",
            Encoding.GetEncoding(866)
        );

    [Fact]
    public async ValueTask Tar_Rar_Write_Async() =>
        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await WriteAsync(
                CompressionType.Rar,
                "Zip.ppmd.noEmptyDirs.zip",
                "Zip.ppmd.noEmptyDirs.zip"
            )
        );

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async ValueTask Tar_Finalize_Archive_Async(bool finalizeArchive)
    {
        using var stream = new MemoryStream();
        using Stream content = File.OpenRead(Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg"));
        using (
            var writer = new TarWriter(
                new AsyncOnlyStream(stream),
                new TarWriterOptions(CompressionType.None, finalizeArchive)
            )
        )
        {
            await writer.WriteAsync("doesn't matter", content, null);
        }

        var paddedContentWithHeader = (content.Length / 512 * 512) + 512 + 512;
        var expectedStreamLength = finalizeArchive
            ? paddedContentWithHeader + (512 * 2)
            : paddedContentWithHeader;
        Assert.Equal(expectedStreamLength, stream.Length);
    }

    [Fact]
    public async ValueTask Tar_Ustar_HeaderFormat_WritesShortPath_Async()
    {
        using var stream = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true, TarHeaderWriteFormat.USTAR);
        using (var writer = new TarWriter(new AsyncOnlyStream(stream), options))
        using (var content = new MemoryStream(Encoding.UTF8.GetBytes("hello")))
        {
            await writer.WriteAsync("dir/file.txt", content, null);
        }

        stream.Position = 0;
        using var archive = TarArchive.OpenArchive(stream);
        Assert.Single(archive.Entries);
        Assert.Equal("dir/file.txt", archive.Entries.Single().Key);
    }

    [Fact]
    public async ValueTask Tar_Ustar_HeaderFormat_ThrowsForLongPath_Async()
    {
        var longName = new string('a', 160) + ".txt";

        using var stream = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true, TarHeaderWriteFormat.USTAR);
        using var writer = new TarWriter(new AsyncOnlyStream(stream), options);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await writer.WriteAsync(longName, content, null)
        );
    }

    [Fact]
    public async ValueTask Tar_GnuLongLink_HeaderFormat_WritesLongPath_Async()
    {
        var longName = new string('a', 160) + ".txt";

        using var stream = new MemoryStream();
        var options = new TarWriterOptions(
            CompressionType.None,
            true,
            TarHeaderWriteFormat.GNU_TAR_LONG_LINK
        );

        using (var writer = new TarWriter(new AsyncOnlyStream(stream), options))
        using (var content = new MemoryStream(Encoding.UTF8.GetBytes("hello")))
        {
            await writer.WriteAsync(longName, content, null);
        }

        stream.Position = 0;
        using var archive = TarArchive.OpenArchive(stream);
        Assert.Single(archive.Entries);
        Assert.Equal(longName, archive.Entries.Single().Key);
    }

    [Fact]
    public async ValueTask Tar_Ustar_HeaderFormat_ThrowsForLongDirectory_Async()
    {
        var longDirectory = new string('a', 170);

        using var stream = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true, TarHeaderWriteFormat.USTAR);
        using var writer = new TarWriter(new AsyncOnlyStream(stream), options);

        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await writer.WriteDirectoryAsync(longDirectory, null)
        );
    }
}
