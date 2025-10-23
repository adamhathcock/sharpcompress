using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipWriterTests : WriterTests
{
    public ZipWriterTests()
        : base(ArchiveType.Zip) { }

    [Fact]
    public Task Zip_Deflate_Write() =>
        WriteAsync(
            CompressionType.Deflate,
            "Zip.deflate.noEmptyDirs.zip",
            "Zip.deflate.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public Task Zip_BZip2_Write() =>
        WriteAsync(
            CompressionType.BZip2,
            "Zip.bzip2.noEmptyDirs.zip",
            "Zip.bzip2.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public Task Zip_None_Write() =>
        WriteAsync(
            CompressionType.None,
            "Zip.none.noEmptyDirs.zip",
            "Zip.none.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public Task Zip_LZMA_Write() =>
        WriteAsync(
            CompressionType.LZMA,
            "Zip.lzma.noEmptyDirs.zip",
            "Zip.lzma.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public Task Zip_PPMd_Write() =>
        WriteAsync(
            CompressionType.PPMd,
            "Zip.ppmd.noEmptyDirs.zip",
            "Zip.ppmd.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public Task Zip_Rar_Write() =>
        Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await WriteAsync(
                CompressionType.Rar,
                "Zip.ppmd.noEmptyDirs.zip",
                "Zip.ppmd.noEmptyDirs.zip"
            )
        );
}
