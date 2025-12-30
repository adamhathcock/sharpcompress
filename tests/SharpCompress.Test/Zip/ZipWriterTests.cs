using System.IO;
using System.Text;
using SharpCompress.Common;
using SharpCompress.Writers;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipWriterTests : WriterTests
{
    public ZipWriterTests()
        : base(ArchiveType.Zip) { }

    [Fact]
    public void Zip_BZip2_Write_EmptyFile()
    {
        // Test that writing an empty file with BZip2 compression doesn't throw DivideByZeroException
        using var memoryStream = new MemoryStream();
        var options = new WriterOptions(CompressionType.BZip2)
        {
            ArchiveEncoding = new ArchiveEncoding { Default = new UTF8Encoding(false) },
        };

        using (var writer = WriterFactory.Open(memoryStream, ArchiveType.Zip, options))
        {
            writer.Write("test-folder/zero-byte-file.txt", Stream.Null);
        }

        Assert.True(memoryStream.Length > 0);
    }

    [Fact]
    public void Zip_BZip2_Write_EmptyFolder()
    {
        // Test that writing an empty folder entry with BZip2 compression doesn't throw DivideByZeroException
        using var memoryStream = new MemoryStream();
        var options = new WriterOptions(CompressionType.BZip2)
        {
            ArchiveEncoding = new ArchiveEncoding { Default = new UTF8Encoding(false) },
        };

        using (var writer = WriterFactory.Open(memoryStream, ArchiveType.Zip, options))
        {
            writer.Write("test-empty-folder/", Stream.Null);
        }

        Assert.True(memoryStream.Length > 0);
    }

    [Fact]
    public void Zip_Deflate_Write() =>
        Write(
            CompressionType.Deflate,
            "Zip.deflate.noEmptyDirs.zip",
            "Zip.deflate.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public void Zip_BZip2_Write() =>
        Write(
            CompressionType.BZip2,
            "Zip.bzip2.noEmptyDirs.zip",
            "Zip.bzip2.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public void Zip_None_Write() =>
        Write(
            CompressionType.None,
            "Zip.none.noEmptyDirs.zip",
            "Zip.none.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public void Zip_LZMA_Write() =>
        Write(
            CompressionType.LZMA,
            "Zip.lzma.noEmptyDirs.zip",
            "Zip.lzma.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public void Zip_PPMd_Write() =>
        Write(
            CompressionType.PPMd,
            "Zip.ppmd.noEmptyDirs.zip",
            "Zip.ppmd.noEmptyDirs.zip",
            Encoding.UTF8
        );

    [Fact]
    public void Zip_Rar_Write() =>
        Assert.Throws<InvalidFormatException>(() =>
            Write(CompressionType.Rar, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip")
        );
}
