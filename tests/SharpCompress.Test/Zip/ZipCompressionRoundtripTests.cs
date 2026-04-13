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
    private const int TESTSIZE = 1024 * 1024;

    [Fact]
    public void Zip_Deflate_Roundtrip_ArchiveApi_BufferedRead_Succeeds()
    {
        // Create test data with specific patterns
        var testset1 = Enumerable
            .Range(0, TESTSIZE)
            .Select(i => (byte)((i * 22695477) % 257))
            .ToArray();
        var testset2 = Enumerable
            .Range(0, TESTSIZE)
            .Select(i => (byte)((i * 48271) % 257))
            .ToArray();

        using var stream = new MemoryStream();

        // Compress test streams
        var writerOptions = new ZipWriterOptions(CompressionType.Deflate, compressionLevel: 9);

        using (var writer = WriterFactory.OpenWriter(stream, ArchiveType.Zip, writerOptions))
        {
            writer.Write("sample1", new MemoryStream(testset1));
            writer.Write("sample2", new MemoryStream(testset2));
        }

        // Decompress and verify using Archive API with buffered read (CopyTo)
        stream.Position = 0;
        using (var archive = ZipArchive.OpenArchive(stream))
        {
            var files = archive.Entries.Where(e => !e.IsDirectory).OrderBy(e => e.Key).ToList();
            Assert.Equal(2, files.Count);

            // Read using CopyTo pattern (buffered - should succeed)
            using (var entryStream = files[1].OpenEntryStream())
            {
                using var extracted = new MemoryStream();
                entryStream.CopyTo(extracted);
                Assert.Equal(testset2, extracted.ToArray());
            }

            using (var entryStream = files[0].OpenEntryStream())
            {
                using var extracted = new MemoryStream();
                entryStream.CopyTo(extracted);
                Assert.Equal(testset1, extracted.ToArray());
            }
        }
    }

    [Fact]
    public void Zip_Deflate_Roundtrip_ArchiveApi_ByteByByteRead_Succeeds()
    {
        // Create test data with specific patterns
        var testset1 = Enumerable
            .Range(0, TESTSIZE)
            .Select(i => (byte)((i * 22695477) % 257))
            .ToArray();
        var testset2 = Enumerable
            .Range(0, TESTSIZE)
            .Select(i => (byte)((i * 48271) % 257))
            .ToArray();

        using var stream = new MemoryStream();

        // Compress test streams
        var writerOptions = new ZipWriterOptions(CompressionType.Deflate, compressionLevel: 9);

        using (var writer = WriterFactory.OpenWriter(stream, ArchiveType.Zip, writerOptions))
        {
            writer.Write("sample1", new MemoryStream(testset1));
            writer.Write("sample2", new MemoryStream(testset2));
        }

        // Decompress and verify using byte-by-byte read
        stream.Position = 0;
        using (var archive = ZipArchive.OpenArchive(stream))
        {
            var files = archive.Entries.Where(e => !e.IsDirectory).OrderBy(e => e.Key).ToList();
            Assert.Equal(2, files.Count);

            // Read second file byte by byte
            using (var entryStream = files[1].OpenEntryStream())
            {
                var buffer = new byte[testset2.Length];
                for (var i = 0; i < buffer.Length; i++)
                {
                    var b = entryStream.ReadByte();
                    if (b == -1)
                    {
                        throw new InvalidOperationException($"Unexpected EOF at offset {i}");
                    }

                    buffer[i] = (byte)b;
                }
                Assert.Equal(testset2, buffer);

                // Verify EOF
                Assert.Equal(-1, entryStream.ReadByte());
            }

            // Read first file byte by byte using All pattern
            using (var entryStream = files[0].OpenEntryStream())
            {
                var match =
                    testset1.All(b => b == entryStream.ReadByte()) && entryStream.ReadByte() == -1;
                Assert.True(
                    match,
                    "Decompressed file sample1 contents do not match the source file."
                );
            }
        }
    }

    [Fact]
    public void Zip_Deflate_Roundtrip_ReaderApi_BufferedRead_Succeeds()
    {
        // Create test data with specific patterns
        var testset1 = Enumerable
            .Range(0, TESTSIZE)
            .Select(i => (byte)((i * 22695477) % 257))
            .ToArray();
        var testset2 = Enumerable
            .Range(0, TESTSIZE)
            .Select(i => (byte)((i * 48271) % 257))
            .ToArray();

        using var stream = new MemoryStream();

        // Compress test streams
        var writerOptions = new ZipWriterOptions(CompressionType.Deflate, compressionLevel: 9);

        using (var writer = WriterFactory.OpenWriter(stream, ArchiveType.Zip, writerOptions))
        {
            writer.Write("sample1", new MemoryStream(testset1));
            writer.Write("sample2", new MemoryStream(testset2));
        }

        // Decompress and verify using Reader API with buffered read (CopyTo)
        stream.Position = 0;
        using (var reader = ReaderFactory.OpenReader(stream))
        {
            // Read first entry
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("sample1", reader.Entry.Key);
            using (var entryStream = reader.OpenEntryStream())
            {
                using var extracted = new MemoryStream();
                entryStream.CopyTo(extracted);
                Assert.Equal(testset1, extracted.ToArray());
            }

            // Read second entry
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("sample2", reader.Entry.Key);
            using (var entryStream = reader.OpenEntryStream())
            {
                using var extracted = new MemoryStream();
                entryStream.CopyTo(extracted);
                Assert.Equal(testset2, extracted.ToArray());
            }

            // No more entries
            Assert.False(reader.MoveToNextEntry());
        }
    }

    [Fact]
    public void Zip_Deflate_Roundtrip_ReaderApi_ByteByByteRead_Succeeds()
    {
        // Create test data with specific patterns
        var testset1 = Enumerable
            .Range(0, TESTSIZE)
            .Select(i => (byte)((i * 22695477) % 257))
            .ToArray();
        var testset2 = Enumerable
            .Range(0, TESTSIZE)
            .Select(i => (byte)((i * 48271) % 257))
            .ToArray();

        using var stream = new MemoryStream();

        // Compress test streams
        var writerOptions = new ZipWriterOptions(CompressionType.Deflate, compressionLevel: 9);

        using (var writer = WriterFactory.OpenWriter(stream, ArchiveType.Zip, writerOptions))
        {
            writer.Write("sample1", new MemoryStream(testset1));
            writer.Write("sample2", new MemoryStream(testset2));
        }

        // Decompress and verify using Reader API with byte-by-byte read
        stream.Position = 0;
        using (var reader = ReaderFactory.OpenReader(stream))
        {
            // Read first entry byte by byte
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("sample1", reader.Entry.Key);
            using (var entryStream = reader.OpenEntryStream())
            {
                var buffer = new byte[testset1.Length];
                for (var i = 0; i < buffer.Length; i++)
                {
                    var b = entryStream.ReadByte();
                    if (b == -1)
                    {
                        throw new InvalidOperationException($"Unexpected EOF at offset {i}");
                    }
                    buffer[i] = (byte)b;
                }
                Assert.Equal(testset1, buffer);
                Assert.Equal(-1, entryStream.ReadByte());
            }

            // Read second entry byte by byte
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal("sample2", reader.Entry.Key);
            using (var entryStream = reader.OpenEntryStream())
            {
                var buffer = new byte[testset2.Length];
                for (var i = 0; i < buffer.Length; i++)
                {
                    var b = entryStream.ReadByte();
                    if (b == -1)
                    {
                        throw new InvalidOperationException($"Unexpected EOF at offset {i}");
                    }
                    buffer[i] = (byte)b;
                }
                Assert.Equal(testset2, buffer);
                Assert.Equal(-1, entryStream.ReadByte());
            }

            // No more entries
            Assert.False(reader.MoveToNextEntry());
        }
    }
}
