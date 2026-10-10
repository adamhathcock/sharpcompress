using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using SharpCompress.Archives.GZip;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Test.Mocks;
using Xunit;
using SharpCompressionMode = SharpCompress.Compressors.CompressionMode;
using SystemCompressionMode = System.IO.Compression.CompressionMode;

namespace SharpCompress.Test.GZip;

public class GZipExtraFieldTests
{
    private static readonly byte[] EXPECTED_PAYLOAD = [.. "gzip payload"u8];

    public static IEnumerable<object[]> FragmentedExtraFields()
    {
        foreach (var api in new[] { "Stream", "Reader", "Archive" })
        {
            foreach (var useAsync in new[] { false, true })
            {
                foreach (var seekable in new[] { false, true })
                {
                    foreach (var extraLength in new[] { 0, 32768, 65535 })
                    {
                        foreach (var chunkSize in new[] { 1, 7, 1024 })
                        {
                            if (api != "Archive" || seekable)
                            {
                                yield return [api, useAsync, seekable, extraLength, chunkSize];
                            }
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(FragmentedExtraFields))]
    public async Task GZip_Read_WithFragmentedExtraField_ShouldReturnPayload(
        string api,
        bool useAsync,
        bool seekable,
        int extraLength,
        int chunkSize
    )
    {
        using var input = new ShortReadStream(
            new MemoryStream(CreateGZipWithExtraField(extraLength, "payload.bin")),
            chunkSize,
            seekable
        );
        using var asyncOnly = new AsyncOnlyStream(input, disposeStream: false);
        Assert.Equal(
            EXPECTED_PAYLOAD,
            await ReadPayload(useAsync ? asyncOnly : input, api, useAsync, CancellationToken.None)
        );
        Assert.False(input.IsDisposed);
    }

    public static IEnumerable<object[]> TruncatedExtraFields()
    {
        foreach (var api in new[] { "Stream", "Reader", "Archive" })
        {
            foreach (var useAsync in new[] { false, true })
            {
                foreach (var length in new[] { 1, 9, 10, 11, 12, 13, 43 })
                {
                    yield return [api, useAsync, length];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TruncatedExtraFields))]
    public async Task GZip_Read_RejectsTruncatedExtraField(string api, bool useAsync, int length)
    {
        using var input = new ShortReadStream(
            new MemoryStream(CreateGZipWithExtraField(32).Take(length).ToArray())
        );
        await Assert.ThrowsAsync<ZlibException>(() =>
            ReadPayload(input, api, useAsync, CancellationToken.None)
        );
        Assert.False(input.IsDisposed);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 7)]
    [InlineData(true, 7)]
    [InlineData(false, 8)]
    [InlineData(true, 8)]
    public async Task GZipStream_Read_RejectsTruncatedTrailer(bool useAsync, int missingBytes)
    {
        var data = CreateGZipWithExtraField(32);
        using var input = new ShortReadStream(
            new MemoryStream(data.Take(data.Length - missingBytes).ToArray())
        );
        await Assert.ThrowsAsync<ZlibException>(() =>
            ReadPayload(input, "Stream", useAsync, CancellationToken.None)
        );
        Assert.False(input.IsDisposed);
    }

    [Theory]
    [InlineData("Stream")]
    [InlineData("Reader")]
    [InlineData("Archive")]
    public async Task GZip_ReadAsync_WithExtraField_HonorsCancellation(string api)
    {
        using var input = new ShortReadStream(new MemoryStream(CreateGZipWithExtraField(65535)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ReadPayload(input, api, true, cancellation.Token)
        );
        Assert.False(input.IsDisposed);
    }

    private static async Task<byte[]> ReadPayload(
        Stream input,
        string api,
        bool useAsync,
        CancellationToken cancellationToken
    )
    {
        var options = ReaderOptions.ForExternalStream;
        using var output = new MemoryStream();
        switch (api)
        {
            case "Stream":
                if (useAsync)
                {
                    await using var gzip = new SharpCompress.Compressors.Deflate.GZipStream(
                        input,
                        SharpCompressionMode.Decompress,
                        options
                    );
                    await gzip.CopyToAsync(output, 81920, cancellationToken);
                }
                else
                {
                    using var gzip = new SharpCompress.Compressors.Deflate.GZipStream(
                        input,
                        SharpCompressionMode.Decompress,
                        options
                    );
                    gzip.CopyTo(output);
                }
                break;
            case "Reader":
                if (useAsync)
                {
                    await using var reader = await GZipReader.OpenAsyncReader(
                        input,
                        options,
                        cancellationToken
                    );
                    Assert.True(await reader.MoveToNextEntryAsync(cancellationToken));
                    await reader.WriteEntryToAsync(output, cancellationToken);
                }
                else
                {
                    using var reader = GZipReader.OpenReader(input, options);
                    Assert.True(reader.MoveToNextEntry());
                    reader.WriteEntryTo(output);
                }
                break;
            case "Archive":
                if (useAsync)
                {
                    await using var archive = await GZipArchive.OpenAsyncArchive(
                        input,
                        options,
                        cancellationToken
                    );
                    await foreach (
                        var entry in archive.EntriesAsync.WithCancellation(cancellationToken)
                    )
                    {
                        using var entryStream = await entry.OpenEntryStreamAsync(cancellationToken);
                        await entryStream.CopyToAsync(output, 81920, cancellationToken);
                        break;
                    }
                }
                else
                {
                    using var archive = GZipArchive.OpenArchive(input, options);
                    using var entryStream = archive.Entries.Single().OpenEntryStream();
                    entryStream.CopyTo(output);
                }
                break;
            default:
                throw new ArgumentException("Unknown GZip API", nameof(api));
        }
        return output.ToArray();
    }

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
        using var rawInput = new MemoryStream(CreateGZipWithExtraField(65535, "payload.bin"));
        using var input = new AsyncOnlyStream(rawInput, disposeStream: false);
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
        using var rawInput = new MemoryStream(CreateGZipWithExtraField(65535, "payload.bin"));
        using var input = new AsyncOnlyStream(rawInput, disposeStream: false);
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
