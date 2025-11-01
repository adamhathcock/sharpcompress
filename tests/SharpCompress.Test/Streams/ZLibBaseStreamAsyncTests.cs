using System.IO;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class ZLibBaseStreamAsyncTests
{
    [Fact]
    public async Task TestChunkedZlibCompressesEverythingAsync()
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
        var realCompressedSize = await compressor1
            .ReadAsync(buf, 0, plainData.Length * 2)
            .ConfigureAwait(false);

        var plainStream2 = new MemoryStream(plainData);
        var compressor2 = new DeflateStream(plainStream2, CompressionMode.Compress);
        var total = 0;
        var r = -1; // Jumpstart
        while (r != 0)
        {
            // Reading in chunks
            r = await compressor2.ReadAsync(buf, 0, plainData.Length).ConfigureAwait(false);
            total += r;
        }

        Assert.Equal(total, realCompressedSize);
    }

    [Fact]
    public async Task Zlib_should_read_the_previously_written_message_async()
    {
        var message = new string('a', 131073); // 131073 causes the failure, but 131072 (-1) doesn't
        var bytes = Encoding.ASCII.GetBytes(message);

        using var inputStream = new MemoryStream(bytes);
        using var compressedStream = new MemoryStream();
        using var byteBufferStream = new BufferedStream(inputStream); // System.IO
        await CompressAsync(byteBufferStream, compressedStream, compressionLevel: 1)
            .ConfigureAwait(false);
        compressedStream.Position = 0;

        using var decompressedStream = new MemoryStream();
        await DecompressAsync(compressedStream, decompressedStream).ConfigureAwait(false);

        byteBufferStream.Position = 0;
        var result = Encoding.ASCII.GetString(
            await GetBytesAsync(byteBufferStream).ConfigureAwait(false)
        );
        result.Should().Be(message);
    }

    private async Task CompressAsync(Stream input, Stream output, int compressionLevel)
    {
        using var zlibStream = new ZlibStream(
            SharpCompressStream.Create(output, leaveOpen: true),
            CompressionMode.Compress,
            (CompressionLevel)compressionLevel
        );
        zlibStream.FlushMode = FlushType.Sync;
        await input.CopyToAsync(zlibStream).ConfigureAwait(false);
    }

    private async Task DecompressAsync(Stream input, Stream output)
    {
        using var zlibStream = new ZlibStream(
            SharpCompressStream.Create(input, leaveOpen: true),
            CompressionMode.Decompress
        );
        await zlibStream.CopyToAsync(output).ConfigureAwait(false);
    }

    private async Task<byte[]> GetBytesAsync(BufferedStream stream)
    {
        var bytes = new byte[stream.Length];
        await stream.ReadAsync(bytes, 0, (int)stream.Length).ConfigureAwait(false);
        return bytes;
    }
}
