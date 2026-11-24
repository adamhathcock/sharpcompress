using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Arj;
using Xunit;
using Xunit.Sdk;

namespace SharpCompress.Test.Arj;

public class ArjReaderTests : ReaderTests
{
    public ArjReaderTests()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
    }

    [Fact]
    public void Arj_Uncompressed_Read() => Read("Arj.store.arj", CompressionType.None);

    [Fact]
    public void Arj_Method1_Read() => Read("Arj.method1.arj");

    [Fact]
    public void Arj_Method2_Read() => Read("Arj.method2.arj");

    [Fact]
    public void Arj_Method3_Read() => Read("Arj.method3.arj");

    [Fact]
    public void Arj_Method4_Read() => Read("Arj.method4.arj");

    [Fact]
    public void Arj_Encrypted_Read()
    {
        var exception = Assert.Throws<CryptographicException>(() => Read("Arj.encrypted.arj"));
    }

    [Fact]
    public void Arj_Multi_Reader()
    {
        var exception = Assert.Throws<MultiVolumeExtractionException>(() =>
            DoMultiReader(
                [
                    "Arj.store.split.arj",
                    "Arj.store.split.a01",
                    "Arj.store.split.a02",
                    "Arj.store.split.a03",
                    "Arj.store.split.a04",
                    "Arj.store.split.a05",
                ],
                streams => ArjReader.OpenReader(streams)
            )
        );
    }

    [Theory]
    [InlineData("Arj.store.largefile.arj", CompressionType.None)]
    [InlineData("Arj.method1.largefile.arj", CompressionType.ArjLZ77)]
    [InlineData("Arj.method2.largefile.arj", CompressionType.ArjLZ77)]
    [InlineData("Arj.method3.largefile.arj", CompressionType.ArjLZ77)]
    [InlineData("Arj.method4.largefile.arj", CompressionType.ArjLZ77)]
    public void Arj_LargeFileTest_Read(string fileName, CompressionType compressionType)
    {
        ReadForBufferBoundaryCheck(fileName, compressionType);
    }

    [Theory]
    [InlineData("Arj.method1.largefile.arj", 1)]
    [InlineData("Arj.method1.largefile.arj", 7)]
    [InlineData("Arj.method1.largefile.arj", 4093)]
    [InlineData("Arj.method2.largefile.arj", 13)]
    [InlineData("Arj.method3.largefile.arj", 13)]
    public void Arj_LargeFile_ReadInSmallChunks(string fileName, int chunkSize)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, fileName));
        using var reader = ReaderFactory.OpenReader(stream, ReaderOptions.ForExternalStream);
        Assert.True(reader.MoveToNextEntry());

        using var entryStream = reader.OpenEntryStream();
        using var output = new MemoryStream();

        // Read into the middle of a larger buffer to verify offset and count are honoured.
        var buffer = new byte[chunkSize + 10];
        int read;
        while ((read = entryStream.Read(buffer, 5, chunkSize)) > 0)
        {
            output.Write(buffer, 5, read);
        }

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(MISC_TEST_FILES_PATH, "alice29.txt")),
            output.ToArray()
        );
    }

    [Theory]
    [InlineData("Arj.method1.arj")]
    [InlineData("Arj.method2.arj")]
    [InlineData("Arj.method3.arj")]
    [InlineData("Arj.method4.arj")]
    public void Arj_CompressedEntryOver2GB_Throws(string fileName)
    {
        using var stream = new MemoryStream(
            ArjTestArchives.WithFirstEntryOriginalSize(
                Path.Combine(TEST_ARCHIVES_PATH, fileName),
                0xC0000000
            )
        );
        using var reader = ReaderFactory.OpenReader(stream, ReaderOptions.ForExternalStream);
        Assert.True(reader.MoveToNextEntry());

        Assert.Equal(0xC0000000L, reader.Entry.Size);
        Assert.Throws<NotSupportedException>(() => reader.OpenEntryStream());
    }

    [Fact]
    public void Arj_StoredEntryOver2GB_ReportsUnsignedSize()
    {
        using var stream = new MemoryStream(
            ArjTestArchives.WithFirstEntryOriginalSize(
                Path.Combine(TEST_ARCHIVES_PATH, "Arj.store.arj"),
                0xC0000000
            )
        );
        using var reader = ReaderFactory.OpenReader(stream, ReaderOptions.ForExternalStream);
        Assert.True(reader.MoveToNextEntry());

        Assert.Equal(0xC0000000L, reader.Entry.Size);
        using var entryStream = reader.OpenEntryStream();
    }
}
