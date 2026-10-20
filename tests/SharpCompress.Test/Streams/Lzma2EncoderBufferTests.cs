using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA;
using Xunit;

namespace SharpCompress.Test.Streams;

public class Lzma2EncoderBufferTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Pooled_Compressed_Output_RoundTrips(bool useAsync, bool incompressible)
    {
        var data = new byte[150000];
        if (incompressible)
        {
            new Random(42).NextBytes(data);
        }
        using var packed = new MemoryStream();
        using (var encoder = new Lzma2EncoderStream(packed, 1 << 16, 32))
        {
            if (useAsync)
            {
                await encoder.WriteAsync(data, 0, data.Length, CancellationToken.None);
                await encoder.DisposeAsync();
            }
            else
            {
                encoder.Write(data, 0, data.Length);
            }
        }
        packed.Position = 0;
        using var decoded = new MemoryStream();
        if (useAsync)
        {
            await using var decoder = await LzmaStream.CreateAsync(
                new byte[] { 8 },
                packed,
                packed.Length,
                data.Length
            );
            await decoder.CopyToAsync(decoded, 81920, CancellationToken.None);
        }
        else
        {
            using var decoder = LzmaStream.Create(
                new byte[] { 8 },
                packed,
                packed.Length,
                data.Length
            );
            decoder.CopyTo(decoded);
        }
        Assert.Equal(data, decoded.ToArray());
    }
}
