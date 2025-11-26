using System;
using System.IO;
using System.Linq;
using System.Text;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Common.Zip.SOZip;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class SOZipTests : TestBase
{
    [Fact]
    public void SOZipIndex_RoundTrip()
    {
        // Create an index
        var offsets = new ulong[] { 0, 1024, 2048, 3072 };
        var originalIndex = new SOZipIndex(
            chunkSize: 32768,
            uncompressedSize: 100000,
            compressedSize: 50000,
            compressedOffsets: offsets
        );

        // Serialize to bytes
        var bytes = originalIndex.ToByteArray();

        // Deserialize back
        var parsedIndex = SOZipIndex.Read(bytes);

        // Verify all fields
        Assert.Equal(SOZipIndex.SOZIP_VERSION, parsedIndex.Version);
        Assert.Equal(32768u, parsedIndex.ChunkSize);
        Assert.Equal(100000ul, parsedIndex.UncompressedSize);
        Assert.Equal(50000ul, parsedIndex.CompressedSize);
        Assert.Equal(4u, parsedIndex.OffsetCount);
        Assert.Equal(offsets, parsedIndex.CompressedOffsets);
    }

    [Fact]
    public void SOZipIndex_Read_InvalidMagic_ThrowsException()
    {
        var invalidData = new byte[] { 0x00, 0x00, 0x00, 0x00 };

        var exception = Assert.Throws<InvalidDataException>(() => SOZipIndex.Read(invalidData));

        Assert.Contains("magic number mismatch", exception.Message);
    }

    [Fact]
    public void SOZipIndex_GetChunkIndex()
    {
        var offsets = new ulong[] { 0, 1000, 2000, 3000, 4000 };
        var index = new SOZipIndex(
            chunkSize: 32768,
            uncompressedSize: 163840, // 5 * 32768
            compressedSize: 5000,
            compressedOffsets: offsets
        );

        Assert.Equal(0, index.GetChunkIndex(0));
        Assert.Equal(0, index.GetChunkIndex(32767));
        Assert.Equal(1, index.GetChunkIndex(32768));
        Assert.Equal(2, index.GetChunkIndex(65536));
        Assert.Equal(4, index.GetChunkIndex(163839));
    }

    [Fact]
    public void SOZipIndex_GetCompressedOffset()
    {
        var offsets = new ulong[] { 0, 1000, 2000, 3000, 4000 };
        var index = new SOZipIndex(
            chunkSize: 32768,
            uncompressedSize: 163840,
            compressedSize: 5000,
            compressedOffsets: offsets
        );

        Assert.Equal(0ul, index.GetCompressedOffset(0));
        Assert.Equal(1000ul, index.GetCompressedOffset(1));
        Assert.Equal(2000ul, index.GetCompressedOffset(2));
        Assert.Equal(3000ul, index.GetCompressedOffset(3));
        Assert.Equal(4000ul, index.GetCompressedOffset(4));
    }

    [Fact]
    public void SOZipIndex_GetUncompressedOffset()
    {
        var offsets = new ulong[] { 0, 1000, 2000, 3000, 4000 };
        var index = new SOZipIndex(
            chunkSize: 32768,
            uncompressedSize: 163840,
            compressedSize: 5000,
            compressedOffsets: offsets
        );

        Assert.Equal(0ul, index.GetUncompressedOffset(0));
        Assert.Equal(32768ul, index.GetUncompressedOffset(1));
        Assert.Equal(65536ul, index.GetUncompressedOffset(2));
        Assert.Equal(98304ul, index.GetUncompressedOffset(3));
        Assert.Equal(131072ul, index.GetUncompressedOffset(4));
    }

    [Fact]
    public void SOZipIndex_GetIndexFileName()
    {
        Assert.Equal(".file.txt.sozip.idx", SOZipIndex.GetIndexFileName("file.txt"));
        Assert.Equal("dir/.file.txt.sozip.idx", SOZipIndex.GetIndexFileName("dir/file.txt"));
        Assert.Equal("a/b/.file.txt.sozip.idx", SOZipIndex.GetIndexFileName("a/b/file.txt"));
    }

    [Fact]
    public void SOZipIndex_IsIndexFile()
    {
        Assert.True(SOZipIndex.IsIndexFile(".file.txt.sozip.idx"));
        Assert.True(SOZipIndex.IsIndexFile("dir/.file.txt.sozip.idx"));
        Assert.True(SOZipIndex.IsIndexFile(".test.sozip.idx"));

        Assert.False(SOZipIndex.IsIndexFile("file.txt"));
        Assert.False(SOZipIndex.IsIndexFile("file.sozip.idx")); // Missing leading dot
        Assert.False(SOZipIndex.IsIndexFile(".file.txt")); // Missing .sozip.idx
        Assert.False(SOZipIndex.IsIndexFile(""));
        Assert.False(SOZipIndex.IsIndexFile(null!));
    }

    [Fact]
    public void SOZipIndex_GetMainFileName()
    {
        Assert.Equal("file.txt", SOZipIndex.GetMainFileName(".file.txt.sozip.idx"));
        Assert.Equal("dir/file.txt", SOZipIndex.GetMainFileName("dir/.file.txt.sozip.idx"));
        Assert.Equal("test", SOZipIndex.GetMainFileName(".test.sozip.idx"));

        Assert.Null(SOZipIndex.GetMainFileName("file.txt"));
        Assert.Null(SOZipIndex.GetMainFileName(""));
    }

    [Fact]
    public void ZipEntry_IsSozipIndexFile_Detection()
    {
        // Create a zip with a file that has a SOZip index file name pattern
        using var memoryStream = new MemoryStream();

        using (
            var writer = WriterFactory.Open(
                memoryStream,
                ArchiveType.Zip,
                new ZipWriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }
            )
        )
        {
            // Write a regular file
            writer.Write("test.txt", new MemoryStream(Encoding.UTF8.GetBytes("Hello World")));

            // Write a file with SOZip index name pattern
            var indexData = new SOZipIndex(
                chunkSize: 32768,
                uncompressedSize: 100,
                compressedSize: 50,
                compressedOffsets: new ulong[] { 0 }
            );
            writer.Write(".test.txt.sozip.idx", new MemoryStream(indexData.ToByteArray()));
        }

        memoryStream.Position = 0;

        using var archive = ZipArchive.Open(memoryStream);
        var entries = archive.Entries.ToList();

        Assert.Equal(2, entries.Count);

        var regularEntry = entries.First(e => e.Key == "test.txt");
        Assert.False(regularEntry.IsSozipIndexFile);
        Assert.False(regularEntry.IsSozip); // No SOZip extra field

        var indexEntry = entries.First(e => e.Key == ".test.txt.sozip.idx");
        Assert.True(indexEntry.IsSozipIndexFile);
    }

    [Fact]
    public void ZipWriterOptions_SOZipDefaults()
    {
        var options = new ZipWriterOptions(CompressionType.Deflate);

        Assert.False(options.EnableSOZip);
        Assert.Equal((int)SOZipIndex.DEFAULT_CHUNK_SIZE, options.SOZipChunkSize);
        Assert.Equal(1048576L, options.SOZipMinFileSize); // 1MB
    }

    [Fact]
    public void ZipWriterEntryOptions_SOZipDefaults()
    {
        var options = new ZipWriterEntryOptions();

        Assert.Null(options.EnableSOZip);
    }
}
