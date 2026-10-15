using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class DeflateStoredWindowTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 32767)]
    [InlineData(true, 32767)]
    [InlineData(false, 65536)]
    [InlineData(true, 65536)]
    public async Task Stored_Blocks_RoundTrip_Across_Window_Slides(bool useAsync, int chunkSize)
    {
        var data = CreateData(256 * 1024 + 17);
        using var output = new MemoryStream();
        if (useAsync)
        {
            await using var compressor = new DeflateStream(
                new AsyncOnlyStream(output, disposeStream: false),
                CompressionMode.Compress,
                CompressionLevel.None
            );
            for (var offset = 0; offset < data.Length; offset += chunkSize)
            {
                await compressor.WriteAsync(
                    data,
                    offset,
                    Math.Min(chunkSize, data.Length - offset),
                    CancellationToken.None
                );
            }
        }
        else
        {
            using var compressor = new DeflateStream(
                new ForwardOnlyStream(output),
                CompressionMode.Compress,
                CompressionLevel.None
            );
            for (var offset = 0; offset < data.Length; offset += chunkSize)
            {
                compressor.Write(data, offset, Math.Min(chunkSize, data.Length - offset));
            }
        }
        AssertRawRoundTrip(output.ToArray(), data);
    }

    [Theory]
    [InlineData(false, FlushType.None)]
    [InlineData(false, FlushType.Partial)]
    [InlineData(false, FlushType.Sync)]
    [InlineData(false, FlushType.Full)]
    [InlineData(true, FlushType.None)]
    [InlineData(true, FlushType.Partial)]
    [InlineData(true, FlushType.Sync)]
    [InlineData(true, FlushType.Full)]
    public void Changing_Levels_Preserves_History_Across_Stored_Windows(
        bool dictionary,
        FlushType flush
    )
    {
        // Each segment crosses several windows. Returning to level 0 after matching
        // has populated the tables must still slide the existing match history.
        var segment = CreateData(128 * 1024 + 13);
        var levels = new[]
        {
            CompressionLevel.None,
            CompressionLevel.BestSpeed,
            CompressionLevel.None,
            CompressionLevel.Default,
            CompressionLevel.None,
            CompressionLevel.BestCompression,
        };
        var expected = levels.SelectMany(_ => segment).ToArray();
        var compressed = new byte[expected.Length * 2 + 65536];
        var codec = new ZlibCodec();
        var preset = segment.Take(32000).ToArray();
        Assert.Equal(0, codec.InitializeDeflate(levels[0], wantRfc1950Header: dictionary));
        try
        {
            if (dictionary)
            {
                Assert.Equal(0, codec.SetDictionary(preset));
            }
            codec.OutputBuffer = compressed;
            codec.AvailableBytesOut = compressed.Length;
            foreach (var level in levels)
            {
                Assert.Equal(0, codec.SetDeflateParams(level, CompressionStrategy.Default));
                codec.InputBuffer = segment;
                codec.NextIn = 0;
                codec.AvailableBytesIn = segment.Length;
                do
                {
                    Assert.Equal(0, codec.Deflate(flush));
                    Assert.True(codec.AvailableBytesOut > 0);
                } while (codec.AvailableBytesIn > 0);
            }
            Assert.Equal(1, codec.Deflate(FlushType.Finish));
            var bytes = compressed.Take(codec.NextOut).ToArray();
            if (dictionary)
            {
                AssertDictionaryRoundTrip(bytes, preset, expected);
            }
            else
            {
                AssertRawRoundTrip(bytes, expected);
            }
        }
        finally
        {
            codec.EndDeflate();
        }
    }

    [Theory]
    [InlineData(9)]
    [InlineData(15)]
    public void Reset_Then_Stored_Compression_Discards_Previous_Hash_History(int windowBits)
    {
        var data = CreateData(256 * 1024 + 17);
        var codec = new ZlibCodec();
        Assert.Equal(0, codec.InitializeDeflate(CompressionLevel.Default, windowBits, false));
        try
        {
            foreach (var level in new[] { CompressionLevel.Default, CompressionLevel.None })
            {
                Assert.Equal(0, codec.SetDeflateParams(level, CompressionStrategy.Default));
                codec.InputBuffer = data;
                codec.NextIn = 0;
                codec.AvailableBytesIn = data.Length;
                codec.OutputBuffer = new byte[data.Length * 2];
                codec.NextOut = 0;
                codec.AvailableBytesOut = codec.OutputBuffer.Length;
                Assert.Equal(1, codec.Deflate(FlushType.Finish));
                AssertRawRoundTrip(codec.OutputBuffer.Take(codec.NextOut).ToArray(), data);
                codec.ResetDeflate();
            }
        }
        finally
        {
            codec.EndDeflate();
        }
    }

    private static void AssertDictionaryRoundTrip(
        byte[] compressed,
        byte[] dictionary,
        byte[] expected
    )
    {
        var codec = new ZlibCodec();
        Assert.Equal(0, codec.InitializeInflate());
        try
        {
            codec.InputBuffer = compressed;
            codec.AvailableBytesIn = compressed.Length;
            codec.OutputBuffer = new byte[expected.Length + 1];
            codec.AvailableBytesOut = codec.OutputBuffer.Length;
            Assert.Equal(2, codec.Inflate(FlushType.None));
            Assert.Equal(0, codec.SetDictionary(dictionary));
            Assert.Equal(1, codec.Inflate(FlushType.Finish));
            Assert.Equal(expected.Length, codec.TotalBytesOut);
            Assert.Equal(expected, codec.OutputBuffer.Take(expected.Length).ToArray());
        }
        finally
        {
            codec.EndInflate();
        }
    }

    private static void AssertRawRoundTrip(byte[] compressed, byte[] expected)
    {
        using (var source = new MemoryStream(compressed))
        using (var decompressor = new DeflateStream(source, CompressionMode.Decompress))
        using (var extracted = new MemoryStream())
        {
            decompressor.CopyTo(extracted);
            Assert.Equal(expected, extracted.ToArray());
        }
#if !LEGACY_DOTNET
        using var nativeSource = new MemoryStream(compressed);
        using var native = new System.IO.Compression.DeflateStream(
            nativeSource,
            System.IO.Compression.CompressionMode.Decompress
        );
        using var nativeOutput = new MemoryStream();
        native.CopyTo(nativeOutput);
        Assert.Equal(expected, nativeOutput.ToArray());
#endif
    }

    private static byte[] CreateData(int length)
    {
        var data = new byte[length];
        var random = new Random(1234);
        random.NextBytes(data);
        // Mix random bytes with repeatable matches, including near window boundaries.
        for (var offset = 4096; offset + 4096 <= length; offset += 8192)
        {
            Array.Copy(data, 0, data, offset, 4096);
        }
        return data;
    }
}
