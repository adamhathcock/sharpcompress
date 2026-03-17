using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Lzw;
using Xunit;

namespace SharpCompress.Test.Lzw;

public class LzwReaderTests : ReaderTests
{
    public LzwReaderTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public void Lzw_Reader_Generic() => Read("Tar.tar.Z", CompressionType.Lzw);

    [Fact]
    public void Lzw_Reader_Generic2()
    {
        //read only as Lzw item
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.Z"));
        using var reader = LzwReader.OpenReader(SharpCompressStream.CreateNonDisposing(stream));
        while (reader.MoveToNextEntry())
        {
            // LZW doesn't have CRC or Size in header like GZip, so we just check the entry exists
            Assert.NotNull(reader.Entry);
        }
    }

    [Fact]
    public void Lzw_Reader_Factory_Detects_Tar_Wrapper()
    {
        // Note: Testing with Tar.tar.Z because:
        // 1. LzwStream only supports decompression, not compression
        // 2. This tests the important tar wrapper detection code path in LzwFactory.TryOpenReader
        // 3. Verifies that tar.Z files correctly return TarReader with CompressionType.Lzw
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.Z"));
        using var reader = ReaderFactory.OpenReader(
            stream,
            new ReaderOptions { LeaveStreamOpen = false }
        );

        // Should detect as Tar archive with Lzw compression
        Assert.Equal(ArchiveType.Tar, reader.Type);
        Assert.True(reader.MoveToNextEntry());
        Assert.NotNull(reader.Entry);
        Assert.Equal(CompressionType.Lzw, reader.Entry.CompressionType);
    }

    [Fact]
    public void Lzw_Reader_Plain_Z_File()
    {
        // Test with a plain .Z file (not tar-wrapped) using LzwReader directly
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "large_test.txt.Z"));
        using var reader = LzwReader.OpenReader(stream);

        Assert.True(reader.MoveToNextEntry());
        var entry = reader.Entry;
        Assert.NotNull(entry);
        Assert.Equal(CompressionType.Lzw, entry.CompressionType);

        // Entry key should be "large_test.txt" (stripped .Z extension) when opened via FileStream
        Assert.Equal("large_test.txt", entry.Key);

        // Decompress and verify content
        using var entryStream = reader.OpenEntryStream();
        using var ms = new MemoryStream();
        entryStream.CopyTo(ms);
        var decompressed = System.Text.Encoding.UTF8.GetString(ms.ToArray());

        Assert.Equal(22300, ms.Length);
        Assert.Contains("This is a test file for LZW compression testing", decompressed);
    }

    [Fact]
    public void Lzw_Reader_Factory_Detects_Plain_Z_File()
    {
        // Test that ReaderFactory correctly identifies a plain .Z file (not tar-wrapped)
        using Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "large_test.txt.Z"));
        using var reader = ReaderFactory.OpenReader(stream);

        // Should detect as Lzw archive (not Tar)
        Assert.Equal(ArchiveType.Lzw, reader.Type);
        Assert.True(reader.MoveToNextEntry());
        Assert.NotNull(reader.Entry);
        Assert.Equal(CompressionType.Lzw, reader.Entry.CompressionType);

        // When opened via ReaderFactory with a non-FileStream, key defaults to "data"
        Assert.NotNull(reader.Entry.Key);
    }

    // Regression tests for malformed input crashes (fuzzer-discovered)

    [Fact]
    public void Lzw_MalformedInput_IOOB_ThrowsSharpCompressException()
    {
        // Malformed LZW stream with maxBits=8 producing codes >= table size;
        // previously caused IndexOutOfRangeException in Read().
        var data = Convert.FromHexString(
            "1f9d0836e1553ac4e1ce9ea227000000000000001070b4058faf051127c54144f8bfe54192e141bab6efe8032c41cd64004aef53da4acc8077a5b26245c47b97e6d615e29400000000000003edd1310a8030f1e2ee66ff535d800000000b00000000"
        );
        var ex = Record.Exception(() => DrainReader(data));
        Assert.IsAssignableFrom<SharpCompressException>(ex);
    }

    [Fact]
    public void Lzw_MalformedInput_DivideByZero_ThrowsSharpCompressException()
    {
        // Malformed LZW header (maxBits > MAX_BITS) that previously caused DivideByZeroException
        // on a second Read call (e.g. SkipEntry during disposal) because nBits was left at 0.
        var data = Convert.FromHexString(
            "1f9d1a362f20000000130003edd1310a8030f1605ca2b26245c47b97e6d615e29400000000130003edd1310a8030f1605c606060606060606060606060606060606060606060606060007f60606060280000"
        );
        var ex = Record.Exception(() => DrainReader(data));
        Assert.IsAssignableFrom<SharpCompressException>(ex);
    }

    private static void DrainReader(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = ReaderFactory.OpenReader(ms);
        var buf = new byte[4096];
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                using var entryStream = reader.OpenEntryStream();
                while (entryStream.Read(buf, 0, buf.Length) > 0) { }
            }
        }
    }
}
