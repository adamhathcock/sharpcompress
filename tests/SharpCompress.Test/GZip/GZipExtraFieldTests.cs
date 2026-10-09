using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using SharpCompress.Test.Mocks;
using Xunit;
using SharpCompressionMode = SharpCompress.Compressors.CompressionMode;
using SystemCompressionMode = System.IO.Compression.CompressionMode;

namespace SharpCompress.Test.GZip;

public class GZipExtraFieldTests
{
    private static readonly byte[] EXPECTED_PAYLOAD = [.. "gzip payload"u8];

    [Theory]
    [InlineData(32767)]
    [InlineData(32768)]
    [InlineData(65535)]
    public void GZipStream_Read_WithLargeExtraField_ShouldReturnPayload(int extraLength)
    {
        using var input = new MemoryStream(CreateGZipWithExtraField(extraLength));
        using var gzip = new SharpCompress.Compressors.Deflate.GZipStream(
            input,
            SharpCompressionMode.Decompress
        );
        using var output = new MemoryStream();

        gzip.CopyTo(output);

        _ = output.ToArray().Should().Equal(EXPECTED_PAYLOAD);
    }

    [Theory]
    [InlineData(32767)]
    [InlineData(32768)]
    [InlineData(65535)]
    public async ValueTask GZipStream_ReadAsync_WithLargeExtraField_ShouldReturnPayload(
        int extraLength
    )
    {
        using var rawInput = new MemoryStream(CreateGZipWithExtraField(extraLength));
        using var input = new AsyncOnlyStream(rawInput, disposeStream: false);
        await using var gzip = new SharpCompress.Compressors.Deflate.GZipStream(
            input,
            SharpCompressionMode.Decompress
        );
        using var output = new MemoryStream();

        await gzip.CopyToAsync(output);

        _ = output.ToArray().Should().Equal(EXPECTED_PAYLOAD);
    }

    private static byte[] CreateGZipWithExtraField(int extraLength)
    {
        using var compressed = new MemoryStream();
        using (
            var gzip = new System.IO.Compression.GZipStream(
                compressed,
                SystemCompressionMode.Compress,
                leaveOpen: true
            )
        )
        {
            gzip.Write(EXPECTED_PAYLOAD, 0, EXPECTED_PAYLOAD.Length);
        }

        var original = compressed.ToArray();
        var result = new byte[original.Length + extraLength + 2];
        Buffer.BlockCopy(original, 0, result, 0, 10);
        result[3] |= 0x04;
        result[10] = (byte)extraLength;
        result[11] = (byte)(extraLength >> 8);
        Buffer.BlockCopy(original, 10, result, 12 + extraLength, original.Length - 10);
        return result;
    }
}
