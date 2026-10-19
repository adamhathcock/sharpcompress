using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Xz;
using Xunit;

namespace SharpCompress.Test.Streams;

public class Lzma2TruncationTests
{
    private const string TruncatedXzHex =
        "FD377A585A0000016922DE360200210100000000372797D6010011616C7068610A616C7068610A616C7068610A";
    private const string MutatedSevenZipBase64 =
        "N3q8ryccAATrtqxIDgAAAAAAAABaAAAAAAAAAKyqq98BAApoZWxsbyB3b3JsZAEEBgABCQ4ABwsBAAEhIQEADAwACAoBhRFKDQAABQEZDAAAAAAAAAAAAAAAABETAHQAZQBzAHQALgB0AHgAdAAAABkAFAoBAI6gjYxMV90BFQYBACAAAAAAAA==";

    public static IEnumerable<object[]> PartialHeaders()
    {
        // Start with a complete dictionary-reset chunk so a missing control byte
        // cannot be rejected merely because the dictionary still needs resetting.
        byte[] prefix = [0x01, 0x00, 0x00, 0x58];
        yield return [prefix];
        byte[][] headers =
        [
            [0x01, 0x00, 0x00],
            [0x02, 0x00, 0x00],
            [0xE0, 0x00, 0x00, 0x00, 0x04, 0x00],
        ];
        foreach (var header in headers)
        {
            for (var length = 1; length < header.Length; length++)
            {
                var data = new byte[prefix.Length + length];
                prefix.CopyTo(data, 0);
                Array.Copy(header, 0, data, prefix.Length, length);
                yield return [data];
            }
        }
    }

    [Theory]
    [MemberData(nameof(PartialHeaders))]
    public void Lzma2_Rejects_Partial_Header(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var decoder = LzmaStream.Create([0x00], input);
        Assert.ThrowsAny<SharpCompressException>(() => decoder.CopyTo(Stream.Null));
    }

    [Theory]
    [MemberData(nameof(PartialHeaders))]
    public void Lzma2_ReadByte_Rejects_Partial_Header(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var decoder = LzmaStream.Create([0x00], input);
        Assert.Equal(0x58, decoder.ReadByte());
        Assert.ThrowsAny<SharpCompressException>(() => decoder.ReadByte());
    }

    [Theory]
    [MemberData(nameof(PartialHeaders))]
    public async Task Lzma2_Async_Rejects_Partial_Header(byte[] data)
    {
        using var input = new MemoryStream(data);
        await using var decoder = await LzmaStream.CreateAsync([0x00], input, -1, -1);
        var buffer = new byte[64];
        await Assert.ThrowsAnyAsync<SharpCompressException>(async () =>
            await decoder.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None)
        );
#if !LEGACY_DOTNET
        // Use a fresh decoder because parser state is undefined after a failed read.
        using var memoryInput = new MemoryStream(data);
        await using var memoryDecoder = await LzmaStream.CreateAsync([0x00], memoryInput, -1, -1);
        await Assert.ThrowsAnyAsync<SharpCompressException>(async () =>
            await memoryDecoder.ReadAsync(buffer.AsMemory(), CancellationToken.None)
        );
#endif
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Xz_Rejects_Missing_Lzma2_End_Marker(bool useAsync)
    {
        var data = new byte[TruncatedXzHex.Length / 2];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = Convert.ToByte(TruncatedXzHex.Substring(i * 2, 2), 16);
        }
        using var input = new MemoryStream(data);
        var decoder = new XZStream(input);
        await using var scope = decoder.DisposeAsyncScope().ConfigureAwait(false);
        if (useAsync)
        {
            await Assert.ThrowsAnyAsync<SharpCompressException>(() =>
                decoder.CopyToAsync(Stream.Null, 81920, CancellationToken.None)
            );
        }
        else
        {
            Assert.ThrowsAny<SharpCompressException>(() => decoder.CopyTo(Stream.Null));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SevenZip_Rejects_Missing_Lzma2_End_Marker(bool useAsync)
    {
        using var input = new MemoryStream(Convert.FromBase64String(MutatedSevenZipBase64));
        if (useAsync)
        {
            await using var archive = await ArchiveFactory.OpenAsyncArchive(input);
            await foreach (var entry in archive.EntriesAsync)
            {
                var stream = await entry.OpenEntryStreamAsync(CancellationToken.None);
                await using var scope = stream.DisposeAsyncScope().ConfigureAwait(false);
                await Assert.ThrowsAnyAsync<SharpCompressException>(() =>
                    stream.CopyToAsync(Stream.Null, 81920, CancellationToken.None)
                );
            }
        }
        else
        {
            using var archive = ArchiveFactory.OpenArchive(input);
            foreach (var entry in archive.Entries)
            {
                using var stream = entry.OpenEntryStream();
                Assert.ThrowsAny<SharpCompressException>(() => stream.CopyTo(Stream.Null));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lzma2_Accepts_End_Marker_After_Uncompressed_Chunks(bool useAsync)
    {
        byte[] data = [0x01, 0x00, 0x00, 0x58, 0x02, 0x00, 0x00, 0x59, 0x00];
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        if (useAsync)
        {
            await using var decoder = await LzmaStream.CreateAsync([0x00], input, data.Length, 2);
            await decoder.CopyToAsync(output, 1, CancellationToken.None);
        }
        else
        {
            using var decoder = LzmaStream.Create([0x00], input, data.Length, 2);
            decoder.CopyTo(output, 1);
        }
        Assert.Equal(new byte[] { 0x58, 0x59 }, output.ToArray());
    }
}
