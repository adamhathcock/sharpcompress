using System.IO;
using System.Text;
using AwesomeAssertions;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class ZLibBaseStreamTests
{
    [Fact]
    public void TestChunkedZlibCompressesEverything()
    {
        var plainData = new byte[]
        {
            0xf7,
            0x1b,
            0xda,
            0x0f,
            0xb6,
            0x2b,
            0x3d,
            0x91,
            0xd7,
            0xe1,
            0xb5,
            0x11,
            0x34,
            0x5a,
            0x51,
            0x3f,
            0x8b,
            0xce,
            0x49,
            0xd2,
        };
        var buf = new byte[plainData.Length * 2];

        var plainStream1 = new MemoryStream(plainData);
        var compressor1 = new DeflateStream(plainStream1, CompressionMode.Compress);
        // This is enough to read the entire data
        var realCompressedSize = compressor1.Read(buf, 0, plainData.Length * 2);

        var plainStream2 = new MemoryStream(plainData);
        var compressor2 = new DeflateStream(plainStream2, CompressionMode.Compress);
        var total = 0;
        var r = -1; // Jumpstart
        while (r != 0)
        {
            // Reading in chunks
            r = compressor2.Read(buf, 0, plainData.Length);
            total += r;
        }

        Assert.Equal(total, realCompressedSize);
    }

    [Fact]
    public void Zlib_should_read_the_previously_written_message()
    {
        var message = new string('a', 131073); // 131073 causes the failure, but 131072 (-1) doesn't
        var bytes = Encoding.ASCII.GetBytes(message);

        using var inputStream = new MemoryStream(bytes);
        using var compressedStream = new MemoryStream();
        using var byteBufferStream = new BufferedStream(inputStream); // System.IO
        Compress(byteBufferStream, compressedStream, compressionLevel: 1);
        compressedStream.Position = 0;

        using var decompressedStream = new MemoryStream();
        Decompress(compressedStream, decompressedStream);

        byteBufferStream.Position = 0;
        var result = Encoding.ASCII.GetString(GetBytes(byteBufferStream));
        result.Should().Be(message);
    }

    private void Compress(Stream input, Stream output, int compressionLevel)
    {
        using var zlibStream = new ZlibStream(
            SharpCompressStream.Create(output, leaveOpen: true),
            CompressionMode.Compress,
            (CompressionLevel)compressionLevel
        );
        zlibStream.FlushMode = FlushType.Sync;
        input.CopyTo(zlibStream);
    }

    private void Decompress(Stream input, Stream output)
    {
        using var zlibStream = new ZlibStream(
            SharpCompressStream.Create(input, leaveOpen: true),
            CompressionMode.Decompress
        );
        zlibStream.CopyTo(output);
    }

    private byte[] GetBytes(BufferedStream stream)
    {
        var bytes = new byte[stream.Length];
        stream.Read(bytes, 0, (int)stream.Length);
        return bytes;
    }
}
