using System;
using System.IO;
using AwesomeAssertions;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test;

/// <summary>
/// Tests that malformed compressed input is handled gracefully, throwing library exceptions
/// rather than unhandled IndexOutOfRangeException, DivideByZeroException, or NullReferenceException.
/// </summary>
public class MalformedInputTests
{
    private static void VerifyMalformedInputThrowsLibraryException(string hex)
    {
        var data = Convert.FromHexString(hex);
        using var ms = new MemoryStream(data);
        var buf = new byte[4096];

        Action act = () =>
        {
            using var reader = ReaderFactory.OpenReader(ms);
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    using var entryStream = reader.OpenEntryStream();
                    while (entryStream.Read(buf, 0, buf.Length) > 0) { }
                }
            }
        };

        act.Should()
            .Throw<Exception>()
            .And.Should()
            .BeAssignableTo<SharpCompressException>(
                "malformed input should throw a library exception, not a raw system exception"
            );
    }

    [Fact]
    public void LzwStream_DivideByZero_ThrowsLibraryException()
    {
        // LZW stream with invalid header that would cause DivideByZero on subsequent reads
        VerifyMalformedInputThrowsLibraryException(
            "1f9d1a362f20000000130003edd1310a8030f1605ca2b26245c47b97e6d615e29400000000130003edd1310a8030f1605c606060606060606060606060606060606060606060606060007f60606060280000"
        );
    }

    [Fact]
    public void LzwStream_IndexOutOfRange_ThrowsLibraryException()
    {
        // LZW stream with maxBits < INIT_BITS causing table size mismatch
        VerifyMalformedInputThrowsLibraryException(
            "1f9d0836e1553ac4e1ce9ea227000000000000001070b4058faf051127c54144f8bfe54192e141bab6efe8032c41cd64004aef53da4acc8077a5b26245c47b97e6d615e29400000000000003edd1310a8030f1e2ee66ff535d800000000b00000000"
        );
    }

    [Fact]
    public void BZip2_NullRef_InBsR_ThrowsLibraryException()
    {
        // BZip2 stream with invalid block size causing null bsStream access
        VerifyMalformedInputThrowsLibraryException(
            "425a6857575757575768575757575757fff2fff27c007159425a6857ff0f21007159c1e2d5e2"
        );
    }

    [Fact]
    public void BZip2_IndexOutOfRange_InGetAndMoveToFrontDecode_ThrowsLibraryException()
    {
        // BZip2 with malformed Huffman tables causing code-too-long or bad perm index
        VerifyMalformedInputThrowsLibraryException(
            "425a6839314159265359c1c080e2000001410000100244a000305a6839314159265359c1c080e2000001410000100244a00030cd00c3cd00c34629971772c080e2"
        );
    }

    [Fact]
    public void SqueezeStream_IndexOutOfRange_ThrowsLibraryException()
    {
        // Squeezed ARC stream with malformed Huffman tree node indices
        VerifyMalformedInputThrowsLibraryException(
            "1a041a425a081a0000090000606839425a081730765cbb311042265300040000090000606839425a081730765cbb31104226530053"
        );
    }

    [Fact]
    public void ArcLzwStream_IndexOutOfRange_ThrowsLibraryException()
    {
        // ARC LZW stream with empty or malformed compressed data
        VerifyMalformedInputThrowsLibraryException(
            "1a081a1931081a00000000f9ffffff00000000ddff000000000000000000000000000012006068394200000080c431b37fff531042d9ff"
        );
    }

    [Fact]
    public void ExplodeStream_IndexOutOfRange_ThrowsLibraryException()
    {
        // ZIP entry using Implode/Explode with invalid Huffman tables
        VerifyMalformedInputThrowsLibraryException(
            "504b03040a000000060000ff676767676767676767676767676700000000683a36060000676767676767676767676700000000000000000000000000000000000000000000000000000000630000000000800000000000002e7478745554090003a8c8b6696045ac6975780b000104e803000004e803000068656c6c6f0a504b01021e030a0000000000147f6f5c20303a3639314159265359c1c080e2000001410000100244a00030cd00c346299717786975870b000104e8030000780b000104e803000004e8030000504b050600000000010000e74f004040490000000064"
        );
    }

    [Fact]
    public void Deflate64_IndexOutOfRange_ThrowsLibraryException()
    {
        // ZIP entry using Deflate64 with invalid Huffman data
        VerifyMalformedInputThrowsLibraryException(
            "504b03040a00009709001c0068656c6c6f2e807874555409000000000000147f6f5c20303a36060000ff0600000009425a6839314159265359595959595959a481000000000000000000007478925554050001c601003dffff000000000000001e000000001e00000000000000000000e1490000000000"
        );
    }

    [Fact]
    public void PPMd_NullRef_ThrowsLibraryException()
    {
        // ZIP entry using PPMd with malformed properties triggering uninitialized model access
        VerifyMalformedInputThrowsLibraryException(
            "504b03040000007462001c905c206600fa80ffffffffff1f8b0a00000000000003edd1310a80cf0c00090010000b000000e000000000030000002e000000686515e294362f763ac439d493d62a3671081e05c14114b4058faf051127c54144f8bfe541ace141bab6ef643c2ce2000001410000100244a00040cd41bdc76c4aef3977a5b25645c47b97e6d615e294362f763ac439d493d62a367108f1e2ee66ff535efa7f3015e2943601003ac439d493d62a3671081e05c14114b4058faf3a0003edd1310a80cf8597e6d60500140409"
        );
    }

    [Fact]
    public void LZMA_NullRef_ThrowsLibraryException()
    {
        // ZIP entry using LZMA with invalid dictionary size (0) causing null window buffer access
        VerifyMalformedInputThrowsLibraryException(
            "504b03040a0200000e001c0068646c6c6f2e7478745554ac507578000000000000000000000000000000000000000000e80300000000000068030a0000000000147f040020303a360600002e7478745554090003a8c8b6696045ac69f5780b0006ff1d000908180000e8030000000000a4810000109a9a9a8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b8b9a0000000000000000000000e80300000000000068030a0000009a9a9a504b03440a6fcb486c6c6f2e74ffff"
        );
    }
}
