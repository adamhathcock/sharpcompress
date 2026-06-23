using System;
using System.IO;
using System.Text;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.BZip2;

public class BZip2StreamTests
{
    [Fact]
    public void BZip2Stream_Throws_On_Corrupt_Checksum()
    {
        var compressed = Compress("BZip2 checksum validation test data.");
        compressed[^5] ^= 1;

        using var stream = BZip2Stream.Create(
            new MemoryStream(compressed),
            SharpCompress.Compressors.CompressionMode.Decompress,
            false
        );
        using var output = new MemoryStream();

        Assert.Throws<ArchiveOperationException>(() => stream.CopyTo(output));
    }

    // A stream that ends exactly where a block header is expected (no stream footer) is the shape a caller
    // sees when decoding a truncated stream or a sub-range of blocks extracted for random access. "BZh9" is
    // a valid header with no blocks and no footer, so the very first block-header read hits end-of-input.
    [Fact]
    public void BZip2Stream_TolerateTruncatedStream_DecodesFooterlessStreamAsEmpty()
    {
        var headerOnly = Encoding.ASCII.GetBytes("BZh9");

        Assert.Throws<ArchiveOperationException>(() =>
            Decompress(headerOnly, tolerateTruncatedStream: false)
        );

        Assert.Empty(Decompress(headerOnly, tolerateTruncatedStream: true));
    }

    // A real block followed by end-of-input at the next block boundary: decode a complete stream, then
    // append another header that stops before its first block. With tolerance the first stream's data comes
    // back and the truncated continuation ends cleanly; without it, the end-of-input throws.
    [Fact]
    public void BZip2Stream_TolerateTruncatedStream_DecodesStreamTruncatedAtBlockBoundary()
    {
        const string text = "Some data that bzip2 will put into a single block.";
        var truncated = Concat(Compress(text), Encoding.ASCII.GetBytes("BZh9"));

        Assert.Throws<ArchiveOperationException>(() =>
            Decompress(truncated, tolerateTruncatedStream: false, decompressConcatenated: true)
        );

        var result = Decompress(
            truncated,
            tolerateTruncatedStream: true,
            decompressConcatenated: true
        );
        Assert.Equal(text, Encoding.ASCII.GetString(result));
    }

    // A partial decode's running combined CRC won't match the whole-stream value in the footer, so the
    // flag skips that whole-stream check (per-block CRCs are still enforced). Corrupting the stored
    // combined CRC is fatal by default but tolerated with the flag.
    [Fact]
    public void BZip2Stream_TolerateTruncatedStream_SkipsWholeStreamCrc()
    {
        const string text = "BZip2 combined-CRC validation test data.";
        var compressed = Compress(text);
        compressed[^5] ^= 1;

        Assert.Throws<ArchiveOperationException>(() =>
            Decompress(compressed, tolerateTruncatedStream: false)
        );

        var result = Decompress(compressed, tolerateTruncatedStream: true);
        Assert.Equal(text, Encoding.ASCII.GetString(result));
    }

    // The flag must not change decoding of a normal, well-formed stream.
    [Fact]
    public void BZip2Stream_TolerateTruncatedStream_StillDecodesCompleteStream()
    {
        const string text =
            "Round trip with tolerateTruncatedStream set on a complete, valid stream.";

        var result = Decompress(Compress(text), tolerateTruncatedStream: true);

        Assert.Equal(text, Encoding.ASCII.GetString(result));
    }

    [Fact]
    public void BZip2Stream_Create_WithOptions_UsesDecompressionOptions()
    {
        var headerOnly = Encoding.ASCII.GetBytes("BZh9");

        using var stream = BZip2Stream.Create(
            new MemoryStream(headerOnly),
            CompressionMode.Decompress,
            new BZip2StreamOptions { TolerateTruncatedStream = true }
        );
        using var output = new MemoryStream();

        stream.CopyTo(output);

        Assert.Empty(output.ToArray());
    }

    [Fact]
    public void BZip2Stream_Create_WithOptions_LeavesStreamOpen()
    {
        using var innerStream = new TestStream(new MemoryStream());
        using (
            var stream = BZip2Stream.Create(
                innerStream,
                CompressionMode.Compress,
                new BZip2StreamOptions { LeaveStreamOpen = true }
            )
        )
        {
            var bytes = Encoding.ASCII.GetBytes("leave open");
            stream.Write(bytes, 0, bytes.Length);
        }

        Assert.False(innerStream.IsDisposed);
    }

    [Fact]
    public void BZip2Stream_Create_WithOptions_UsesCompressionBlockSize()
    {
        using var memoryStream = new MemoryStream();
        using (
            var stream = BZip2Stream.Create(
                memoryStream,
                CompressionMode.Compress,
                new BZip2StreamOptions { LeaveStreamOpen = true, BlockSize100k = 1 }
            )
        )
        {
            var bytes = Encoding.ASCII.GetBytes("block size");
            stream.Write(bytes, 0, bytes.Length);
        }

        var compressed = memoryStream.ToArray();
        Assert.Equal((byte)'B', compressed[0]);
        Assert.Equal((byte)'Z', compressed[1]);
        Assert.Equal((byte)'h', compressed[2]);
        Assert.Equal((byte)'1', compressed[3]);
    }

    private static byte[] Decompress(
        byte[] compressed,
        bool tolerateTruncatedStream,
        bool decompressConcatenated = false
    )
    {
        using var stream = BZip2Stream.Create(
            new MemoryStream(compressed),
            CompressionMode.Decompress,
            decompressConcatenated,
            leaveOpen: false,
            tolerateTruncatedStream: tolerateTruncatedStream
        );
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var result = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, result, 0, a.Length);
        Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
        return result;
    }

    private static byte[] Compress(string value)
    {
        using var memoryStream = new MemoryStream();
        using (
            var bzip2Stream = BZip2Stream.Create(
                memoryStream,
                SharpCompress.Compressors.CompressionMode.Compress,
                false,
                leaveOpen: true
            )
        )
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            bzip2Stream.Write(bytes, 0, bytes.Length);
        }

        return memoryStream.ToArray();
    }
}
