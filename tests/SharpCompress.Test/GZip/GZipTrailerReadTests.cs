using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.GZip;
using SharpCompress.Crypto;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipTrailerReadTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(7)]
    public async Task GZip_Async_Validation_Reads_Fragmented_Trailer(int length)
    {
        byte[] payload = [1, 2, 3];
        var trailer = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(trailer, Crc32Stream.Compute(payload));
        BinaryPrimitives.WriteUInt32LittleEndian(trailer.AsSpan(4), 3);
        using var input = new MemoryStream(payload);
        using var fragmented = new ShortReadStream(
            new MemoryStream(trailer.AsSpan(0, length).ToArray()),
            seekable: false
        );
        using var raw = new AsyncOnlyStream(fragmented, disposeStream: false);
        using var stream = new GZipChecksumValidationStream(input, raw, "test", null, null);
        using var output = new MemoryStream();
        if (length == 8)
        {
            await stream.CopyToAsync(output, 81920, CancellationToken.None);
            Assert.Equal(payload, output.ToArray());
        }
        else
        {
            await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
                await stream.CopyToAsync(output, 81920, CancellationToken.None)
            );
        }
    }
}
