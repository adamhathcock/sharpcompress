using System;
using System.Buffers.Binary;
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

        using (var writer = WriterFactory.OpenWriter(memoryStream, ArchiveType.Zip, options))
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

        using (var writer = WriterFactory.OpenWriter(memoryStream, ArchiveType.Zip, options))
        {
            writer.Write("test-empty-folder/", Stream.Null);
        }

        Assert.True(memoryStream.Length > 0);
    }

    [Fact]
    public void Zip_Write_Utf8_Name_Sets_Efs_Flag()
    {
        // Encoding.Default is a UTF-8 encoding without a BOM on .NET Core
        using var memoryStream = new MemoryStream();
        var options = new WriterOptions(CompressionType.Deflate)
        {
            ArchiveEncoding = new ArchiveEncoding { Default = new UTF8Encoding(false) },
        };

        using (var writer = WriterFactory.OpenWriter(memoryStream, ArchiveType.Zip, options))
        {
            writer.Write("d\u00e9j\u00e0-vu.txt", new MemoryStream(new byte[] { 1, 2, 3 }));
        }

        var zip = memoryStream.ToArray();
        var central = BinaryPrimitives.ReadInt32LittleEndian(zip.AsSpan(zip.Length - 6));

        // Bit 11 of the general purpose flags marks the name as UTF-8 for other readers
        Assert.Equal(0x800, BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(6)) & 0x800);
        Assert.Equal(
            0x800,
            BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(central + 8)) & 0x800
        );
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
