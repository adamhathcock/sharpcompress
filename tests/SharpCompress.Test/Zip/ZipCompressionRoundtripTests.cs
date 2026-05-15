using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipCompressionRoundtripTests : TestBase
{
    private const int TestSize = 1024 * 100;

    private static (byte[] TestSet, MemoryStream Stream) CreateTestZip()
    {
        var testset = Enumerable
            .Range(0, TestSize)
            .Select(i => (byte)((i * 22695477) % 257))
            .ToArray();

        var stream = new MemoryStream();

        var writerOptions = new ZipWriterOptions(CompressionType.Deflate, compressionLevel: 9);

        using (var writer = WriterFactory.OpenWriter(stream, ArchiveType.Zip, writerOptions))
        {
            writer.Write("sample1", new MemoryStream(testset));
        }

        stream.Position = 0;
        return (testset, stream);
    }

    [Fact]
    public void Zip_Deflate_Roundtrip_ArchiveApi_BufferedRead_Succeeds()
    {
        var (testset, stream) = CreateTestZip();

        // Decompress and verify using Archive API with buffered read (CopyTo)
        stream.Position = 0;
        using (var archive = ZipArchive.OpenArchive(stream))
        {
            var files = archive.Entries.Where(e => !e.IsDirectory).ToList();
            Assert.Single(files);

            using (var entryStream = files[0].OpenEntryStream())
            {
                using var extracted = new MemoryStream();
                entryStream.CopyTo(extracted);
                Assert.Equal(testset, extracted.ToArray());
            }
        }
    }

    [Fact]
    public void Zip_Deflate_Roundtrip_ArchiveApi_ByteByByteRead_Succeeds()
    {
        var (testset, stream) = CreateTestZip();
        using (var archive = ZipArchive.OpenArchive(stream))
        {
            var files = archive.Entries.Where(e => !e.IsDirectory).ToList();
            Assert.Single(files);

            using (var entryStream = files[0].OpenEntryStream())
            {
                var buffer = new byte[testset.Length];
                for (var i = 0; i < buffer.Length; i++)
                {
                    var b = entryStream.ReadByte();
                    if (b == -1)
                    {
                        throw new InvalidOperationException($"Unexpected EOF at offset {i}");
                    }

                    buffer[i] = (byte)b;
                }
                Assert.Equal(testset, buffer);

                // Verify EOF
                Assert.Equal(-1, entryStream.ReadByte());
            }
        }
    }

    [Fact]
    public void Zip_Deflate_Roundtrip_ReaderApi_BufferedRead_Succeeds()
    {
        var (testset, stream) = CreateTestZip();
        using (var reader = ReaderFactory.OpenReader(stream))
        {
            // Read first entry
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("sample1", reader.Entry.Key);
            using (var entryStream = reader.OpenEntryStream())
            {
                using var extracted = new MemoryStream();
                entryStream.CopyTo(extracted);
                Assert.Equal(testset, extracted.ToArray());
            }

            // No more entries
            Assert.False(reader.MoveToNextEntry());
        }
    }

    [Fact]
    public void Zip_Deflate_Roundtrip_ReaderApi_ByteByByteRead_Succeeds()
    {
        var (testset, stream) = CreateTestZip();
        using (var reader = ReaderFactory.OpenReader(stream))
        {
            // Read first entry byte by byte
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("sample1", reader.Entry.Key);
            using (var entryStream = reader.OpenEntryStream())
            {
                var buffer = new byte[testset.Length];
                for (var i = 0; i < buffer.Length; i++)
                {
                    var b = entryStream.ReadByte();
                    if (b == -1)
                    {
                        throw new InvalidOperationException($"Unexpected EOF at offset {i}");
                    }
                    buffer[i] = (byte)b;
                }
                Assert.Equal(testset, buffer);
                Assert.Equal(-1, entryStream.ReadByte());
            }

            // No more entries
            Assert.False(reader.MoveToNextEntry());
        }
    }
}
