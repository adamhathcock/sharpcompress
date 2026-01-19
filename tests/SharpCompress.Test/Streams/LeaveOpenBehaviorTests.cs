using System;
using System.IO;
using System.Text;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class LeaveOpenBehaviorTests
{
    private static byte[] CreateTestData() =>
        Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog");

    [Fact]
    public void BZip2Stream_Compress_LeaveOpen_False()
    {
        using var innerStream = new TestStream(new MemoryStream());
        using (
            var bzip2 = new BZip2Stream(
                innerStream,
                CompressionMode.Compress,
                false,
                leaveOpen: false
            )
        )
        {
            bzip2.Write(CreateTestData(), 0, CreateTestData().Length);
            bzip2.Finish();
        }

        Assert.True(innerStream.IsDisposed, "Inner stream should be disposed when leaveOpen=false");
    }

    [Fact]
    public void BZip2Stream_Compress_LeaveOpen_True()
    {
        using var innerStream = new TestStream(new MemoryStream());
        byte[] compressed;
        using (
            var bzip2 = new BZip2Stream(
                innerStream,
                CompressionMode.Compress,
                false,
                leaveOpen: true
            )
        )
        {
            bzip2.Write(CreateTestData(), 0, CreateTestData().Length);
            bzip2.Finish();
        }

        Assert.False(
            innerStream.IsDisposed,
            "Inner stream should NOT be disposed when leaveOpen=true"
        );

        // Should be able to read the compressed data
        innerStream.Position = 0;
        compressed = new byte[innerStream.Length];
        innerStream.Read(compressed, 0, compressed.Length);
        Assert.True(compressed.Length > 0);
    }

    [Fact]
    public void BZip2Stream_Decompress_LeaveOpen_False()
    {
        // First compress some data
        var memStream = new MemoryStream();
        using (var bzip2 = new BZip2Stream(memStream, CompressionMode.Compress, false, true))
        {
            bzip2.Write(CreateTestData(), 0, CreateTestData().Length);
            bzip2.Finish();
        }

        memStream.Position = 0;
        using var innerStream = new TestStream(memStream);
        var decompressed = new byte[CreateTestData().Length];

        using (
            var bzip2 = new BZip2Stream(
                innerStream,
                CompressionMode.Decompress,
                false,
                leaveOpen: false
            )
        )
        {
            bzip2.Read(decompressed, 0, decompressed.Length);
        }

        Assert.True(innerStream.IsDisposed, "Inner stream should be disposed when leaveOpen=false");
        Assert.Equal(CreateTestData(), decompressed);
    }

    [Fact]
    public void BZip2Stream_Decompress_LeaveOpen_True()
    {
        // First compress some data
        var memStream = new MemoryStream();
        using (var bzip2 = new BZip2Stream(memStream, CompressionMode.Compress, false, true))
        {
            bzip2.Write(CreateTestData(), 0, CreateTestData().Length);
            bzip2.Finish();
        }

        memStream.Position = 0;
        using var innerStream = new TestStream(memStream);
        var decompressed = new byte[CreateTestData().Length];

        using (
            var bzip2 = new BZip2Stream(
                innerStream,
                CompressionMode.Decompress,
                false,
                leaveOpen: true
            )
        )
        {
            bzip2.Read(decompressed, 0, decompressed.Length);
        }

        Assert.False(
            innerStream.IsDisposed,
            "Inner stream should NOT be disposed when leaveOpen=true"
        );
        Assert.Equal(CreateTestData(), decompressed);

        // Should still be able to use the stream
        innerStream.Position = 0;
        Assert.True(innerStream.CanRead);
    }

    [Fact]
    public void LZipStream_Compress_LeaveOpen_False()
    {
        using var innerStream = new TestStream(new MemoryStream());
        using (var lzip = new LZipStream(innerStream, CompressionMode.Compress, leaveOpen: false))
        {
            lzip.Write(CreateTestData(), 0, CreateTestData().Length);
            lzip.Finish();
        }

        Assert.True(innerStream.IsDisposed, "Inner stream should be disposed when leaveOpen=false");
    }

    [Fact]
    public void LZipStream_Compress_LeaveOpen_True()
    {
        using var innerStream = new TestStream(new MemoryStream());
        byte[] compressed;
        using (var lzip = new LZipStream(innerStream, CompressionMode.Compress, leaveOpen: true))
        {
            lzip.Write(CreateTestData(), 0, CreateTestData().Length);
            lzip.Finish();
        }

        Assert.False(
            innerStream.IsDisposed,
            "Inner stream should NOT be disposed when leaveOpen=true"
        );

        // Should be able to read the compressed data
        innerStream.Position = 0;
        compressed = new byte[innerStream.Length];
        innerStream.Read(compressed, 0, compressed.Length);
        Assert.True(compressed.Length > 0);
    }

    [Fact]
    public void LZipStream_Decompress_LeaveOpen_False()
    {
        // First compress some data
        var memStream = new MemoryStream();
        using (var lzip = new LZipStream(memStream, CompressionMode.Compress, true))
        {
            lzip.Write(CreateTestData(), 0, CreateTestData().Length);
            lzip.Finish();
        }

        memStream.Position = 0;
        using var innerStream = new TestStream(memStream);
        var decompressed = new byte[CreateTestData().Length];

        using (var lzip = new LZipStream(innerStream, CompressionMode.Decompress, leaveOpen: false))
        {
            lzip.Read(decompressed, 0, decompressed.Length);
        }

        Assert.True(innerStream.IsDisposed, "Inner stream should be disposed when leaveOpen=false");
        Assert.Equal(CreateTestData(), decompressed);
    }

    [Fact]
    public void LZipStream_Decompress_LeaveOpen_True()
    {
        // First compress some data
        var memStream = new MemoryStream();
        using (var lzip = new LZipStream(memStream, CompressionMode.Compress, true))
        {
            lzip.Write(CreateTestData(), 0, CreateTestData().Length);
            lzip.Finish();
        }

        memStream.Position = 0;
        using var innerStream = new TestStream(memStream);
        var decompressed = new byte[CreateTestData().Length];

        using (var lzip = new LZipStream(innerStream, CompressionMode.Decompress, leaveOpen: true))
        {
            lzip.Read(decompressed, 0, decompressed.Length);
        }

        Assert.False(
            innerStream.IsDisposed,
            "Inner stream should NOT be disposed when leaveOpen=true"
        );
        Assert.Equal(CreateTestData(), decompressed);

        // Should still be able to use the stream
        innerStream.Position = 0;
        Assert.True(innerStream.CanRead);
    }
}
