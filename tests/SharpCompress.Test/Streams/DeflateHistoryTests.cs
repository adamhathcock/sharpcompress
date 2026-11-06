using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using Xunit;

namespace SharpCompress.Test.Streams;

public class DeflateHistoryTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Deflate_Rejects_Distance_Beyond_Output_History(
        bool useAsync,
        bool useFastPath,
        bool emptyHistory
    )
    {
        // Fixed Huffman blocks: either a match before any literal, or 'A' then length 3/distance 4.
        byte[] compressed = emptyHistory ? [0x03, 0x02, 0x00] : [0x73, 0x04, 0x62, 0x27, 0x00];
        using var input = new MemoryStream(WithReadAhead(compressed, useFastPath));
        using var decoder = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        ZlibException exception;
        if (useAsync)
        {
            exception = await Assert.ThrowsAsync<ZlibException>(() =>
                decoder.CopyToAsync(output, 1024, CancellationToken.None)
            );
        }
        else
        {
            exception = Assert.Throws<ZlibException>(() => decoder.CopyTo(output));
        }

        Assert.Contains("invalid distance too far back", exception.Message);
        Assert.Empty(output.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deflate_Invalid_Distance_Does_Not_Copy_Uninitialized_History(bool useFastPath)
    {
        var codec = new ZlibCodec();
        codec.InitializeInflate(false);
        try
        {
            // Seed this decoder's window directly, without relying on shared-pool reuse.
            codec.istate.blocks.window.Memory.Span.Fill(0xa5);
            SetInput(codec, WithReadAhead([0x73, 0x04, 0x62, 0x27, 0x00], useFastPath));
            codec.OutputBuffer = new byte[16];
            codec.AvailableBytesOut = codec.OutputBuffer.Length;

            Assert.Throws<ZlibException>(() => codec.Inflate(FlushType.None));

            Assert.Equal(1, codec.NextOut);
            Assert.Equal((byte)'A', codec.OutputBuffer[0]);
            Assert.Equal(new byte[15], codec.OutputBuffer.AsSpan(1).ToArray());
        }
        finally
        {
            codec.EndInflate();
        }
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(false, 4)]
    [InlineData(true, 3)]
    [InlineData(true, 4)]
    public void Deflate_Preset_Dictionary_Limits_Available_History(
        bool useFastPath,
        int dictionarySize
    )
    {
        var codec = new ZlibCodec();
        codec.InitializeInflate(false);
        try
        {
            byte[] dictionary = [(byte)'A', (byte)'B', (byte)'C', (byte)'D'];
            codec.istate.blocks.SetDictionary(dictionary, 0, dictionarySize);
            // Length 3/distance 4, with no preceding literal.
            SetInput(codec, WithReadAhead([0x03, 0x62, 0x00], useFastPath));
            codec.OutputBuffer = new byte[16];
            codec.AvailableBytesOut = codec.OutputBuffer.Length;

            if (dictionarySize < 4)
            {
                var exception = Assert.Throws<ZlibException>(() => codec.Inflate(FlushType.None));
                Assert.Contains("invalid distance too far back", exception.Message);
                Assert.Equal(0, codec.NextOut);
            }
            else
            {
                Assert.Equal(ZlibConstants.Z_STREAM_END, codec.Inflate(FlushType.None));
                Assert.Equal(3, codec.NextOut);
                Assert.Equal(
                    dictionary.AsSpan(0, 3).ToArray(),
                    codec.OutputBuffer.AsSpan(0, 3).ToArray()
                );
            }
        }
        finally
        {
            codec.EndInflate();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deflate_Reset_Discards_Previous_History(bool useFastPath)
    {
        var codec = new ZlibCodec();
        codec.InitializeInflate(false);
        try
        {
            codec.istate.blocks.SetDictionary([1, 2, 3, 4], 0, 4);
            codec.istate.Reset();
            SetInput(codec, WithReadAhead([0x03, 0x62, 0x00], useFastPath));
            codec.OutputBuffer = new byte[16];
            codec.AvailableBytesOut = codec.OutputBuffer.Length;

            var exception = Assert.Throws<ZlibException>(() => codec.Inflate(FlushType.None));
            Assert.Contains("invalid distance too far back", exception.Message);
            Assert.Equal(0, codec.NextOut);
        }
        finally
        {
            codec.EndInflate();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deflate_Rejects_Distance_Beyond_Window_Size(bool useFastPath)
    {
        var codec = new ZlibCodec();
        codec.InitializeInflate(9, false);
        try
        {
            // Produce more than the 512-byte window, then request length 3/distance 513.
            using var compressed = new MemoryStream();
            WriteStoredBlock(compressed, new byte[513]);
            var match = WithReadAhead([0x03, 0x26, 0x00, 0x00], useFastPath);
            compressed.Write(match, 0, match.Length);
            SetInput(codec, compressed.ToArray());
            codec.OutputBuffer = new byte[1024];
            codec.AvailableBytesOut = codec.OutputBuffer.Length;

            var exception = Assert.Throws<ZlibException>(() => codec.Inflate(FlushType.None));
            Assert.Contains("invalid distance too far back", exception.Message);
            Assert.Equal(513, codec.NextOut);
        }
        finally
        {
            codec.EndInflate();
        }
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    [InlineData(false, 32768)]
    [InlineData(true, 32768)]
    [InlineData(false, 32769)]
    [InlineData(true, 32769)]
    public async Task Deflate_History_Survives_Stored_Blocks_And_Window_Wrap(
        bool useAsync,
        int historySize
    )
    {
        var history = new byte[historySize];
        for (var i = 0; i < history.Length; i++)
        {
            history[i] = (byte)(i % 251);
        }

        // Follow a non-final stored block with a fixed block referencing the available history.
        var distance = Math.Min(historySize, 32768);
        byte[] match = distance == 3 ? [0x03, 0x22, 0x00] : [0x03, 0xde, 0xff, 0x0f, 0x00];
        using var compressed = new MemoryStream();
        WriteStoredBlock(compressed, history);
        var tail = WithReadAhead(match, true);
        compressed.Write(tail, 0, tail.Length);
        compressed.Position = 0;

        using var decoder = new DeflateStream(compressed, CompressionMode.Decompress);
        using var output = new MemoryStream();
        if (useAsync)
        {
            await decoder.CopyToAsync(output, 137, CancellationToken.None);
        }
        else
        {
            decoder.CopyTo(output, 137);
        }

        var expected = new byte[history.Length + 3];
        history.CopyTo(expected, 0);
        Array.Copy(history, history.Length - distance, expected, history.Length, 3);
        Assert.Equal(expected, output.ToArray());
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 137)]
    [InlineData(true, 137)]
    public async Task Deflate_History_Survives_Overlapping_And_Partial_Matches(
        bool useAsync,
        int outputBufferSize
    )
    {
        var expected = new byte[70000];
        for (var i = 0; i < expected.Length; i++)
        {
            expected[i] = (byte)(i % 7);
        }
        using var compressed = new MemoryStream();
        using (
            var encoder = new System.IO.Compression.DeflateStream(
                compressed,
                System.IO.Compression.CompressionMode.Compress,
                leaveOpen: true
            )
        )
        {
            encoder.Write(expected, 0, expected.Length);
        }
        compressed.Position = 0;

        using var decoder = new DeflateStream(compressed, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[outputBufferSize];
        int read;
        while (
            (
                read = useAsync
                    ? await decoder.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None)
                    : decoder.Read(buffer, 0, buffer.Length)
            ) != 0
        )
        {
            output.Write(buffer, 0, read);
        }

        Assert.Equal(expected, output.ToArray());
    }

    private static byte[] WithReadAhead(byte[] compressed, bool useFastPath)
    {
        if (!useFastPath)
        {
            return compressed;
        }

        // InflateFast requires at least ten input bytes; bytes after the final block are read-ahead.
        var padded = new byte[compressed.Length + 16];
        compressed.CopyTo(padded, 0);
        return padded;
    }

    private static void WriteStoredBlock(Stream compressed, byte[] history)
    {
        compressed.WriteByte(0);
        compressed.WriteByte((byte)history.Length);
        compressed.WriteByte((byte)(history.Length >> 8));
        compressed.WriteByte((byte)~history.Length);
        compressed.WriteByte((byte)(~history.Length >> 8));
        compressed.Write(history, 0, history.Length);
    }

    private static void SetInput(ZlibCodec codec, byte[] compressed)
    {
        codec.InputBuffer = compressed;
        codec.NextIn = 0;
        codec.AvailableBytesIn = compressed.Length;
    }
}
