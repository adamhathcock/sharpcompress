using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using SharpCompress.Archives.GZip;
using SharpCompress.Readers.GZip;
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
        await using var input = new AsyncOnlyStream(rawInput, disposeStream: false);
        await using var gzip = new SharpCompress.Compressors.Deflate.GZipStream(
            input,
            SharpCompressionMode.Decompress
        );
        using var output = new MemoryStream();

        await gzip.CopyToAsync(output);

        _ = output.ToArray().Should().Equal(EXPECTED_PAYLOAD);
    }

    [Fact]
    public void GZipReader_Read_WithLargeExtraFieldAndName_ShouldReturnEntry()
    {
        using var input = new MemoryStream(CreateGZipWithExtraField(65535, "payload.bin"));
        using var reader = GZipReader.OpenReader(input);

        Assert.True(reader.MoveToNextEntry());
        Assert.Equal("payload.bin", reader.Entry.Key);

        using var output = new MemoryStream();
        reader.WriteEntryTo(output);

        _ = output.ToArray().Should().Equal(EXPECTED_PAYLOAD);
    }

    [Fact]
    public async ValueTask GZipReader_ReadAsync_WithLargeExtraFieldAndName_ShouldReturnEntry()
    {
        await using var input = new AsyncOnlyStream(
            new MemoryStream(CreateGZipWithExtraField(65535, "payload.bin")),
            disposeStream: false
        );
        await using var reader = await GZipReader.OpenAsyncReader(input);

        Assert.True(await reader.MoveToNextEntryAsync());
        Assert.Equal("payload.bin", reader.Entry.Key);

        using var output = new MemoryStream();
        await reader.WriteEntryToAsync(output);

        _ = output.ToArray().Should().Equal(EXPECTED_PAYLOAD);
    }

    [Fact]
    public void GZipArchive_Read_WithLargeExtraFieldAndName_ShouldReturnEntry()
    {
        using var input = new MemoryStream(CreateGZipWithExtraField(65535, "payload.bin"));
        using var archive = GZipArchive.OpenArchive(input);
        var entry = archive.Entries.Single();

        Assert.Equal("payload.bin", entry.Key);
        using var entryStream = entry.OpenEntryStream();
        using var output = new MemoryStream();
        entryStream.CopyTo(output);

        _ = output.ToArray().Should().Equal(EXPECTED_PAYLOAD);
    }

    [Fact]
    public async ValueTask GZipArchive_ReadAsync_WithLargeExtraFieldAndName_ShouldReturnEntry()
    {
        await using var input = new AsyncOnlyStream(
            new MemoryStream(CreateGZipWithExtraField(65535, "payload.bin")),
            disposeStream: false
        );
        await using var archive = await GZipArchive.OpenAsyncArchive(input);
        var entry = await archive.EntriesAsync.FirstAsync();

        Assert.Equal("payload.bin", entry.Key);
#if NETFRAMEWORK
        using (var entryStream = await entry.OpenEntryStreamAsync())
#else
        await using (var entryStream = await entry.OpenEntryStreamAsync())
#endif
        {
            using var output = new MemoryStream();
            await entryStream.CopyToAsync(output);

            _ = output.ToArray().Should().Equal(EXPECTED_PAYLOAD);
        }
    }

    private static byte[] CreateGZipWithExtraField(int extraLength, string? fileName = null)
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
        var fileNameBytes = fileName is null
            ? []
            : Encoding.GetEncoding("iso-8859-1").GetBytes(fileName + "\0");
        var result = new byte[original.Length + extraLength + 2 + fileNameBytes.Length];
        Buffer.BlockCopy(original, 0, result, 0, 10);
        result[3] |= 0x04;
        if (fileNameBytes.Length > 0)
        {
            result[3] |= 0x08;
        }
        result[10] = (byte)extraLength;
        result[11] = (byte)(extraLength >> 8);
        Buffer.BlockCopy(fileNameBytes, 0, result, 12 + extraLength, fileNameBytes.Length);
        Buffer.BlockCopy(
            original,
            10,
            result,
            12 + extraLength + fileNameBytes.Length,
            original.Length - 10
        );
        return result;
    }
}
