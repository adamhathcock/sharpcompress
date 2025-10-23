using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
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
    public Task Tar_Writer() =>
        WriteAsync(
            CompressionType.None,
            "Tar.noEmptyDirs.tar",
            "Tar.noEmptyDirs.tar",
            Encoding.GetEncoding(866)
        );

    [Fact]
    public Task Tar_BZip2_Writer() =>
        WriteAsync(
            CompressionType.BZip2,
            "Tar.noEmptyDirs.tar.bz2",
            "Tar.noEmptyDirs.tar.bz2",
            Encoding.GetEncoding(866)
        );

    [Fact]
    public Task Tar_LZip_Writer() =>
        WriteAsync(
            CompressionType.LZip,
            "Tar.noEmptyDirs.tar.lz",
            "Tar.noEmptyDirs.tar.lz",
            Encoding.GetEncoding(866)
        );

    [Fact]
    public Task Tar_Rar_Write() =>
        Assert.ThrowsAsync<InvalidFormatException>(async () =>
                                                  await WriteAsync(CompressionType.Rar, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip")
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
}
