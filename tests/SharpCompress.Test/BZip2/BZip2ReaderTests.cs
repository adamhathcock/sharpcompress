using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using Xunit;

namespace SharpCompress.Test.BZip2;

public class BZip2ReaderTests : ReaderTests
{
    [Fact]
    public void BZip2_Reader_Factory()
    {
        Stream stream = new MemoryStream(
            new byte[] { 0x42, 0x5a, 0x68, 0x34, 0x31, 0x41, 0x59, 0x26, 0x53, 0x59, 0x35 }
        );
        Assert.Throws<ArchiveOperationException>(() => ReaderFactory.OpenReader(stream));
    }

    // Regression tests for malformed input crashes (fuzzer-discovered)

    [Fact]
    public void BZip2_NullRef_MalformedHeader_ThrowsSharpCompressException()
    {
        // Input "BZhW..." - invalid block-size byte causes Initialize() to return false;
        // previously this led to NullReferenceException in BsR because bsStream was null.
        var data = Convert.FromHexString(
            "425a6857575757575768575757575757fff2fff27c007159425a6857ff0f21007159c1e2d5e2"
        );
        // The stream is not a valid supported archive, so ReaderFactory throws.
        var ex = Record.Exception(() =>
        {
            using var ms = new MemoryStream(data);
            using var reader = ReaderFactory.OpenReader(ms);
        });
        Assert.IsAssignableFrom<SharpCompressException>(ex);
    }

    [Fact]
    public void BZip2_IOOB_MalformedHuffman_ThrowsSharpCompressException()
    {
        // Malformed BZip2 data with invalid Huffman code lengths; previously caused
        // IndexOutOfRangeException in GetAndMoveToFrontDecode.
        var data = Convert.FromHexString(
            "425a6839314159265359c1c080e2000001410000100244a000305a6839314159265359c1c080e2000001410000100244a00030cd00c3cd00c34629971772c080e2"
        );
        var ex = Record.Exception(() =>
        {
            using var ms = new MemoryStream(data);
            using var reader = ReaderFactory.OpenReader(ms);
        });
        Assert.IsAssignableFrom<SharpCompressException>(ex);
    }
}
