using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Arc;

public class ArcReaderTests : ReaderTests
{
    public ArcReaderTests()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
    }

    [Fact]
    public void Arc_Uncompressed_Read() => Read("Arc.uncompressed.arc", CompressionType.None);

    [Fact]
    public void Arc_Squeezed_Read() => Read("Arc.squeezed.arc");

    [Fact]
    public void Arc_Crunched_Read() => Read("Arc.crunched.arc");

    [Theory]
    [InlineData("Arc.crunched.largefile.arc", CompressionType.Crunched)]
    public void Arc_LargeFile_ShouldThrow(string fileName, CompressionType compressionType)
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            ReadForBufferBoundaryCheck(fileName, compressionType)
        );
    }

    [Theory]
    [InlineData("Arc.uncompressed.largefile.arc", CompressionType.None)]
    [InlineData("Arc.squeezed.largefile.arc", CompressionType.Squeezed)]
    public void Arc_LargeFileTest_Read(string fileName, CompressionType compressionType)
    {
        ReadForBufferBoundaryCheck(fileName, compressionType);
    }

    // Regression tests for malformed input crashes (fuzzer-discovered)

    [Fact]
    public void Arc_Squeezed_MalformedInput_ThrowsSharpCompressException()
    {
        // Malformed SqueezeStream input: Huffman tree node index out of range
        var data = Convert.FromHexString(
            "1a041a425a081a0000090000606839425a081730765cbb311042265300040000090000606839425a081730765cbb31104226530053"
        );
        var ex = Record.Exception(() => DrainReader(data));
        Assert.IsAssignableFrom<SharpCompressException>(ex);
    }

    [Fact]
    public void Arc_Crunched_MalformedInput_ThrowsSharpCompressException()
    {
        // Malformed ArcLzwStream input: compressed data too short (CompressedSize=0)
        var data = Convert.FromHexString(
            "1a081a1931081a00000000f9ffffff00000000ddff000000000000000000000000000012006068394200000080c431b37fff531042d9ff"
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
