using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipWriterAsyncTests : WriterTests
{
    public ZipWriterAsyncTests()
        : base(ArchiveType.Zip) { }

    [Fact]
    public async Task Zip_Deflate_Write_Async() =>
        await WriteAsync(
            CompressionType.Deflate,
            "Zip.deflate.noEmptyDirs.zip",
            "Zip.deflate.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public async Task Zip_BZip2_Write_Async() =>
        await WriteAsync(
            CompressionType.BZip2,
            "Zip.bzip2.noEmptyDirs.zip",
            "Zip.bzip2.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public async Task Zip_None_Write_Async() =>
        await WriteAsync(
            CompressionType.None,
            "Zip.none.noEmptyDirs.zip",
            "Zip.none.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public async Task Zip_LZMA_Write_Async() =>
        await WriteAsync(
            CompressionType.LZMA,
            "Zip.lzma.noEmptyDirs.zip",
            "Zip.lzma.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public async Task Zip_PPMd_Write_Async() =>
        await WriteAsync(
            CompressionType.PPMd,
            "Zip.ppmd.noEmptyDirs.zip",
            "Zip.ppmd.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public async Task Zip_Rar_Write_Async() =>
        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await WriteAsync(
                CompressionType.Rar,
                "Zip.ppmd.noEmptyDirs.zip",
                "Zip.ppmd.noEmptyDirs.zip"
            )
        );
}
