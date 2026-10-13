using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class DeflateReadAheadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deflate_One_Byte_Output_Preserves_ReadAhead(bool async)
    {
        using var compressed = new MemoryStream();
        using (
            var writer = new System.IO.Compression.DeflateStream(
                compressed,
                System.IO.Compression.CompressionMode.Compress,
                leaveOpen: true
            )
        )
        {
            writer.WriteByte(42);
        }
        byte[] trailer = [1, 2, 3, 4, 5, 6, 7, 8];
        compressed.Write(trailer, 0, trailer.Length);
        compressed.Position = 0;
        using var decoder = new SharpCompress.Compressors.Deflate.DeflateStream(
            compressed,
            SharpCompress.Compressors.CompressionMode.Decompress
        );
        var buffer = new byte[1];
        if (async)
        {
            Assert.Equal(1, await decoder.ReadAsync(buffer, 0, 1, CancellationToken.None));
            Assert.Equal(0, await decoder.ReadAsync(buffer, 0, 1, CancellationToken.None));
        }
        else
        {
            Assert.Equal(1, decoder.Read(buffer, 0, 1));
            Assert.Equal(0, decoder.Read(buffer, 0, 1));
        }
        Assert.Equal(42, buffer[0]);
        using var remaining = decoder.InputBuffer;
        Assert.Equal(trailer, remaining.ToArray());
    }
}
