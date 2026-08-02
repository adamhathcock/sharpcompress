using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Compressors.LZMA;
using Xunit;

namespace SharpCompress.Test.Streams;

public class LzmaStreamTests
{
    [Fact]
    public void TestLzma2Decompress1Byte()
    {
        var properties = new byte[] { 0x01 };
        var compressedData = new byte[] { 0x01, 0x00, 0x00, 0x58, 0x00 };
        var lzma2Stream = new MemoryStream(compressedData);

        var decompressor = LzmaStream.Create(properties, lzma2Stream, 5, 1);
        Assert.Equal('X', decompressor.ReadByte());
    }

    private static byte[] LzmaData { get; } =
    [
        0x5D,
        0x00,
        0x20,
        0x00,
        0x00,
        0x48,
        0x01,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x80,
        0x24,
        0x18,
        0x2F,
        0xEB,
        0x20,
        0x78,
        0xBA,
        0x78,
        0x70,
        0xDC,
        0x43,
        0x2C,
        0x32,
        0xC9,
        0xC3,
        0x97,
        0x4D,
        0x10,
        0x74,
        0xE2,
        0x20,
        0xBF,
        0x5A,
        0xB4,
        0xB3,
        0xC4,
        0x31,
        0x80,
        0x26,
        0x3E,
        0x6A,
        0xEA,
        0x51,
        0xFC,
        0xE4,
        0x8D,
        0x54,
        0x96,
        0x05,
        0xCC,
        0x78,
        0x59,
        0xAC,
        0xD4,
        0x21,
        0x65,
        0x8F,
        0xA9,
        0xC8,
        0x0D,
        0x9B,
        0xE2,
        0xC2,
        0xF9,
        0x7C,
        0x3C,
        0xDD,
        0x4D,
        0x38,
        0x04,
        0x0B,
        0xF8,
        0x0B,
        0x68,
        0xA5,
        0x93,
        0x6C,
        0x64,
        0xAC,
        0xCF,
        0x71,
        0x68,
        0xE8,
        0x69,
        0x25,
        0xC6,
        0x17,
        0x28,
        0xF1,
        0x7C,
        0xF1,
        0xDC,
        0x47,
        0x51,
        0x4D,
        0x1E,
        0x0E,
        0x0B,
        0x80,
        0x37,
        0x24,
        0x58,
        0x80,
        0xF7,
        0xB4,
        0xAC,
        0x54,
        0xF1,
        0x0F,
        0x7F,
        0x0F,
        0x0F,
        0xF5,
        0x9C,
        0xDE,
        0x54,
        0x4F,
        0xA3,
        0x7B,
        0x20,
        0xC5,
        0xA8,
        0x18,
        0x3B,
        0xED,
        0xDC,
        0x04,
        0xF6,
        0xFB,
        0x86,
        0xE0,
        0xAB,
        0xB6,
        0x87,
        0x99,
        0x92,
        0x43,
        0x7B,
        0x2C,
        0xCC,
        0x31,
        0x83,
        0x90,
        0xFF,
        0xF1,
        0x76,
        0x03,
        0x90,
    ];

    /// <summary>
    /// The decoded data for <see cref="LzmaData"/>.
    /// </summary>
    private static byte[] LzmaResultData { get; } =
    [
        0x01,
        0x00,
        0xFD,
        0x01,
        0x00,
        0x00,
        0x00,
        0x00,
        0xFA,
        0x61,
        0x18,
        0x5F,
        0x02,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x02,
        0x00,
        0x00,
        0x00,
        0x03,
        0x00,
        0x00,
        0x00,
        0x01,
        0x00,
        0x00,
        0x00,
        0x02,
        0x00,
        0xB4,
        0x01,
        0x00,
        0x00,
        0x00,
        0x00,
        0x3D,
        0x61,
        0xE5,
        0x5E,
        0x03,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x12,
        0x00,
        0x00,
        0x00,
        0x02,
        0x00,
        0xB4,
        0x01,
        0x00,
        0x00,
        0x00,
        0x00,
        0xE2,
        0x61,
        0x18,
        0x5F,
        0x04,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x12,
        0x00,
        0x00,
        0x00,
        0x29,
        0x00,
        0x00,
        0x00,
        0x01,
        0x00,
        0xFD,
        0x01,
        0x00,
        0x00,
        0x00,
        0x00,
        0x14,
        0x62,
        0x18,
        0x5F,
        0x01,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x03,
        0x00,
        0x00,
        0x00,
        0x40,
        0x00,
        0x00,
        0x00,
        0x09,
        0x00,
        0x00,
        0x00,
        0x02,
        0x00,
        0xB4,
        0x01,
        0x00,
        0x00,
        0x00,
        0x00,
        0x7F,
        0x61,
        0xE5,
        0x5E,
        0x05,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x3B,
        0x00,
        0x00,
        0x00,
        0xCB,
        0x15,
        0x00,
        0x00,
        0x02,
        0x00,
        0xB4,
        0x01,
        0x00,
        0x00,
        0x00,
        0x00,
        0x7F,
        0x61,
        0xE5,
        0x5E,
        0x06,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x3B,
        0x00,
        0x00,
        0x00,
        0xCB,
        0x15,
        0x00,
        0x00,
        0x02,
        0x00,
        0xB4,
        0x01,
        0x00,
        0x00,
        0x00,
        0x00,
        0x3D,
        0x61,
        0xE5,
        0x5E,
        0x07,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x12,
        0x00,
        0x00,
        0x00,
        0x02,
        0x00,
        0xB4,
        0x01,
        0x00,
        0x00,
        0x00,
        0x00,
        0xFC,
        0x96,
        0x40,
        0x5C,
        0x08,
        0x00,
        0x00,
        0x00,
        0x60,
        0x00,
        0x00,
        0x00,
        0xFF,
        0xFF,
        0xFF,
        0xFF,
        0x00,
        0x00,
        0x00,
        0x00,
        0xF8,
        0x83,
        0x12,
        0x00,
        0xD4,
        0x99,
        0x00,
        0x00,
        0x43,
        0x95,
        0x00,
        0x00,
        0xEB,
        0x7A,
        0x00,
        0x00,
        0x40,
        0x6F,
        0x00,
        0x00,
        0xD2,
        0x6F,
        0x00,
        0x00,
        0x67,
        0x74,
        0x00,
        0x00,
        0x02,
        0x69,
        0x00,
        0x00,
        0x76,
        0x79,
        0x00,
        0x00,
        0x98,
        0x66,
        0x00,
        0x00,
        0x23,
        0x25,
        0x00,
        0x00,
        0x01,
        0x00,
        0xFD,
        0x01,
        0x00,
        0x00,
        0x00,
        0x00,
        0x3B,
        0x2F,
        0xC0,
        0x5F,
        0x09,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0x03,
        0x00,
        0x00,
        0x00,
        0x69,
        0x00,
        0x3D,
        0x00,
        0x0A,
        0x00,
        0x00,
        0x00,
    ];

    [Fact]
    public void TestLzmaBuffer()
    {
        var input = new MemoryStream(LzmaData);
        using var output = new MemoryStream();
        var properties = new byte[5];
        input.Read(properties, 0, 5);

        var fileLengthBytes = new byte[8];
        input.Read(fileLengthBytes, 0, 8);
        var fileLength = BitConverter.ToInt64(fileLengthBytes, 0);

        var coder = new Decoder();
        coder.SetDecoderProperties(properties);
        coder.Code(input, output, input.Length, fileLength, null);

        Assert.Equal(output.ToArray(), LzmaResultData);
    }

    [Fact]
    public void TestLzmaStreamEncodingWritesData()
    {
        using var inputStream = new MemoryStream(LzmaResultData);
        using MemoryStream outputStream = new();
        using var lzmaStream = LzmaStream.Create(
            LzmaEncoderProperties.Default,
            false,
            outputStream
        );
        inputStream.CopyTo(lzmaStream);
        lzmaStream.Close();
        Assert.NotEqual(0, outputStream.Length);
    }

    [Fact]
    public void TestLzmaEncodingAccuracy()
    {
        var input = new MemoryStream(LzmaResultData);
        var compressed = new MemoryStream();
        var lzmaEncodingStream = LzmaStream.Create(
            LzmaEncoderProperties.Default,
            false,
            compressed
        );
        input.CopyTo(lzmaEncodingStream);
        lzmaEncodingStream.Close();
        compressed.Position = 0;

        var output = new MemoryStream();
        DecompressLzmaStream(
            lzmaEncodingStream.Properties,
            compressed,
            compressed.Length,
            output,
            LzmaResultData.LongLength
        );

        Assert.Equal(output.ToArray(), LzmaResultData);
    }

    private static void DecompressLzmaStream(
        byte[] properties,
        Stream compressedStream,
        long compressedSize,
        Stream decompressedStream,
        long decompressedSize
    )
    {
        var lzmaStream = LzmaStream.Create(
            properties,
            compressedStream,
            compressedSize,
            -1,
            null,
            false
        );

        var buffer = new byte[1024];
        long totalRead = 0;
        while (totalRead < decompressedSize)
        {
            var toRead = (int)Math.Min(buffer.Length, decompressedSize - totalRead);
            var read = lzmaStream.Read(buffer, 0, toRead);
            if (read > 0)
            {
                decompressedStream.Write(buffer, 0, read);
                totalRead += read;
            }
            else
            {
                break;
            }
        }
    }

    // Tests Lzma2ParallelDecoder directly using hand-built LZMA2 streams made only of
    // "uncompressed" chunks (control bytes 0x01/0x02). Those chunks carry their payload verbatim
    // (no real LZMA compression involved), which makes it possible to build fully valid, precisely
    // controlled multi-block LZMA2 streams -- including ones that exercise the small-block merge
    // threshold and cross-segment block cuts -- without needing a real (and large) 7z test archive.
#if !LEGACY_DOTNET
    // Props byte encoding a ~2MB dictionary (see LzmaStream's dict-size formula), comfortably
    // larger than any single chunk used by these tests.
    private static readonly byte[] Lzma2Props = { 18 };
#endif

    private static byte[] UncompressedChunk(bool dictReset, byte[] payload)
    {
        Assert.InRange(payload.Length, 1, 0x10000);
        var sizeMinusOne = payload.Length - 1;
        var chunk = new byte[3 + payload.Length];
        chunk[0] = dictReset ? (byte)0x01 : (byte)0x02;
        chunk[1] = (byte)(sizeMinusOne >> 8);
        chunk[2] = (byte)(sizeMinusOne & 0xFF);
        Buffer.BlockCopy(payload, 0, chunk, 3, payload.Length);
        return chunk;
    }

    private static byte[] RepeatingPayload(int size, byte seed)
    {
        var payload = new byte[size];
        for (var i = 0; i < size; i++)
        {
            payload[i] = (byte)(seed + i);
        }
        return payload;
    }

    [Fact]
    public void Lzma2ParallelDecoder_TryScanBlocks_MergesSmallSegmentsAndCutsOnceThresholdReached()
    {
        // Seg1+Seg2 are both tiny -> merge. Seg3 is >= MinBlockSize on its own, so it merges into
        // the same block (the cut only happens once the *already accumulated* size reaches the
        // threshold), pushing that block's total past MinBlockSize. Seg4 then starts a new block,
        // and Seg5 (tiny) merges into it. The trailing block is flushed as-is even though small.
        var seg1 = UncompressedChunk(dictReset: true, RepeatingPayload(100, 1));
        var seg2 = UncompressedChunk(dictReset: true, RepeatingPayload(100, 2));
        var seg3 = UncompressedChunk(dictReset: true, RepeatingPayload(20_000, 3));
        var seg4 = UncompressedChunk(dictReset: true, RepeatingPayload(50, 4));
        var seg5 = UncompressedChunk(dictReset: true, RepeatingPayload(50, 5));

        using var ms = new MemoryStream();
        ms.Write(seg1, 0, seg1.Length);
        ms.Write(seg2, 0, seg2.Length);
        ms.Write(seg3, 0, seg3.Length);
        ms.Write(seg4, 0, seg4.Length);
        ms.Write(seg5, 0, seg5.Length);
        ms.WriteByte(0); // end marker

        var packSize = ms.Length;
        var unpackSize = 100 + 100 + 20_000 + 50 + 50;

        var blocks = Lzma2ParallelDecoder.TryScanBlocks(ms, 0, packSize, unpackSize);

        Assert.NotNull(blocks);
        Assert.Equal(2, blocks!.Count);

        Assert.Equal(0, blocks[0].UnpackOffset);
        Assert.Equal(100 + 100 + 20_000, blocks[0].UnpackLen);

        Assert.Equal(100 + 100 + 20_000, blocks[1].UnpackOffset);
        Assert.Equal(50 + 50, blocks[1].UnpackLen);
    }

    [Fact]
    public void Lzma2ParallelDecoder_TryScanBlocks_SingleSmallSegment_ReturnsOneBlock()
    {
        var seg = UncompressedChunk(dictReset: true, RepeatingPayload(64, 7));
        using var ms = new MemoryStream();
        ms.Write(seg, 0, seg.Length);
        ms.WriteByte(0);

        var blocks = Lzma2ParallelDecoder.TryScanBlocks(ms, 0, ms.Length, 64);

        Assert.NotNull(blocks);
        Assert.Single(blocks!);
        Assert.Equal(0, blocks![0].UnpackOffset);
        Assert.Equal(64, blocks[0].UnpackLen);
    }

    [Fact]
    public void Lzma2ParallelDecoder_TryScanBlocks_NonRestartChunksNeverCutABlock()
    {
        // A dict-reset chunk followed by several *non*-restart continuation chunks must all stay
        // in the same block, regardless of accumulated size, since none of them are independently
        // decodable restart points.
        var seg1 = UncompressedChunk(dictReset: true, RepeatingPayload(20_000, 1));
        var seg2 = UncompressedChunk(dictReset: false, RepeatingPayload(20_000, 2));
        var seg3 = UncompressedChunk(dictReset: false, RepeatingPayload(20_000, 3));

        using var ms = new MemoryStream();
        ms.Write(seg1, 0, seg1.Length);
        ms.Write(seg2, 0, seg2.Length);
        ms.Write(seg3, 0, seg3.Length);
        ms.WriteByte(0);

        var blocks = Lzma2ParallelDecoder.TryScanBlocks(ms, 0, ms.Length, 60_000);

        Assert.NotNull(blocks);
        Assert.Single(blocks!);
        Assert.Equal(60_000, blocks![0].UnpackLen);
    }

    [Fact]
    public void Lzma2ParallelDecoder_TryScanBlocks_MismatchedUnpackSize_ReturnsNull()
    {
        var seg = UncompressedChunk(dictReset: true, RepeatingPayload(64, 7));
        using var ms = new MemoryStream();
        ms.Write(seg, 0, seg.Length);
        ms.WriteByte(0);

        var blocks = Lzma2ParallelDecoder.TryScanBlocks(
            ms,
            0,
            ms.Length,
            65 /* wrong */
        );

        Assert.Null(blocks);
    }

    [Fact]
    public void Lzma2ParallelDecoder_TryScanBlocks_InvalidControlByte_ReturnsNull()
    {
        using var ms = new MemoryStream(new byte[] { 0x03, 0x00, 0x00, 0x00, 0x00 });

        var blocks = Lzma2ParallelDecoder.TryScanBlocks(ms, 0, ms.Length, 1);

        Assert.Null(blocks);
    }

    [Fact]
    public void Lzma2ParallelDecoder_TryScanBlocks_TruncatedStream_ReturnsNull()
    {
        // Claims a 100-byte chunk but only provides a couple of bytes of payload.
        using var ms = new MemoryStream(new byte[] { 0x01, 0x00, 0x63, 0xAA, 0xBB });

        var blocks = Lzma2ParallelDecoder.TryScanBlocks(ms, 0, ms.Length, 100);

        Assert.Null(blocks);
    }

    [Fact]
    public void Lzma2ParallelDecoder_TryScanBlocks_EmptyStream_ReturnsEmptyList()
    {
        using var ms = new MemoryStream(new byte[] { 0 }); // just the end marker

        var blocks = Lzma2ParallelDecoder.TryScanBlocks(ms, 0, ms.Length, 0);

        Assert.NotNull(blocks);
        Assert.Empty(blocks!);
    }

    [Fact]
    public void Lzma2ParallelDecoder_TryScanBlocks_RealCompressedChunksWithNewProps_ParsesAndDecodesCorrectly()
    {
        // Real LZMA2 compressed chunks (as produced by the actual encoder used by SevenZipWriter)
        // always set the new-props flag (control 0xE0-0xFF), which carries an extra properties
        // byte in the chunk header, in addition to the 4 size-field bytes. This regression test
        // guards that exact byte -- the hand-built "uncompressed chunk" tests above (control
        // 0x01/0x02) never exercise this header shape, which is exactly how this bug slipped
        // through: a real multi-GB archive was needed to surface it, header-only synthetic tests
        // using only uncompressed chunks were not enough.
        var payload = new byte[5_000_000];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i % 7); // highly repetitive -> compresses well, forces real chunks
        }

        byte[] lzma2Props;
        using var packMs = new MemoryStream();
        using (
            var encoder = new Lzma2EncoderStream(packMs, dictionarySize: 1 << 20, numFastBytes: 32)
        )
        {
            encoder.Write(payload, 0, payload.Length);
            lzma2Props = encoder.Properties;
        }
        var packBytes = packMs.ToArray();

        var blocks = Lzma2ParallelDecoder.TryScanBlocks(
            packMs,
            0,
            packBytes.Length,
            payload.Length
        );

        Assert.NotNull(blocks);
        Assert.True(blocks!.Count >= 1);
        Assert.Equal(payload.Length, blocks.Sum(b => b.UnpackLen));

        // Decode sequentially (production LzmaStream decode) to confirm the scanner's reported
        // pack/unpack accounting is actually consistent with what a real decode produces.
        using var decodeMs = new MemoryStream(packBytes);
        using var lzma = LzmaStream.Create(
            lzma2Props,
            decodeMs,
            -1,
            payload.Length,
            leaveOpen: true
        );
        var decoded = new byte[payload.Length];
        var totalRead = 0;
        while (totalRead < decoded.Length)
        {
            var read = lzma.Read(decoded, totalRead, decoded.Length - totalRead);
            Assert.True(read > 0);
            totalRead += read;
        }

        Assert.Equal(payload, decoded);
    }

#if !LEGACY_DOTNET
    [Fact]
    public void Lzma2ParallelDecoder_DecodeBlocksParallel_ProducesByteIdenticalOutputAcrossMultipleBlocks()
    {
        // Enough independent, large-enough segments to guarantee more than one merged block.
        var payloads = new List<byte[]>();
        for (var i = 0; i < 6; i++)
        {
            payloads.Add(RepeatingPayload(20_000 + i, (byte)(i * 17)));
        }

        using var packMs = new MemoryStream();
        foreach (var payload in payloads)
        {
            var chunk = UncompressedChunk(dictReset: true, payload);
            packMs.Write(chunk, 0, chunk.Length);
        }
        packMs.WriteByte(0);
        var packBytes = packMs.ToArray();

        var expected = new byte[payloads.Count == 0 ? 0 : payloads.Sum(p => p.Length)];
        var offset = 0;
        foreach (var payload in payloads)
        {
            Buffer.BlockCopy(payload, 0, expected, offset, payload.Length);
            offset += payload.Length;
        }

        var blocks = Lzma2ParallelDecoder.TryScanBlocks(
            packMs,
            0,
            packBytes.Length,
            expected.Length
        );
        Assert.NotNull(blocks);
        Assert.True(blocks!.Count > 1, "Test setup should produce more than one block.");

        var inputPath = Path.GetTempFileName();
        var outputPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(inputPath, packBytes);

            using (
                var inputFile = new FileStream(
                    inputPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                )
            )
            using (
                var outputFile = new FileStream(
                    outputPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None
                )
            )
            {
                outputFile.SetLength(expected.Length);
                Lzma2ParallelDecoder.DecodeBlocksParallel(
                    inputFile.SafeFileHandle,
                    Lzma2Props,
                    0,
                    blocks,
                    outputFile.SafeFileHandle,
                    Environment.ProcessorCount
                );
            }

            var actual = File.ReadAllBytes(outputPath);
            Assert.Equal(expected, actual);
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputPath);
        }
    }
#endif
}
