using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;
using SharpCompress.Readers;
using SharpCompress.Test.Streams;
using Xunit;

namespace SharpCompress.Test.Rar;

[Collection(DictionarySizeLimitCollection.Name)]
public class RarDictionarySizeLimitTests
{
    // The advisory's complete 72-byte archives have valid header CRCs, no packed
    // data, and just 45,056 bytes of declared output. Neither needs a large window.
    private const string Rar256MiB =
        "526172211a070100b28180510c01050800070101c496868000c63c6d612a02030b8080000480e00220c809b1cf8059000c6578652f746573742e6578650a03020058a2ce32ffc101";
    private const string Rar1GiB =
        "526172211a070100b28180510c01050800070101c4968680000fd7ea442a02030b8080000480e00220c809b1cf8069000c6578652f746573742e6578650a03020058a2ce32ffc101";

    public static IEnumerable<object[]> Payloads()
    {
        foreach (var hex in new[] { Rar256MiB, Rar1GiB })
        {
            foreach (var archiveApi in new[] { false, true })
            {
                foreach (var useAsync in new[] { false, true })
                {
                    yield return new object[] { hex, archiveApi, useAsync };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Payloads))]
    public async Task Rar5_Huge_Window_Is_Rejected_Before_Decoding(
        string hex,
        bool archiveApi,
        bool useAsync
    )
    {
        var bytes = DecodeHex(hex);
        Assert.Equal(72, bytes.Length);
        using var input = new MemoryStream(bytes);
        var options = ReaderOptions.ForExternalStream;
        InvalidFormatException exception;
        if (archiveApi && useAsync)
        {
            await using var archive = await ArchiveFactory.OpenAsyncArchive(
                input,
                options,
                CancellationToken.None
            );
            var entry = await archive.EntriesAsync.FirstAsync();
            Assert.Equal(45056, entry.Size);
            Assert.Equal(0, entry.CompressedSize);
            exception = await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            {
                using var stream = await entry.OpenEntryStreamAsync(CancellationToken.None);
            });
        }
        else if (archiveApi)
        {
            using var archive = ArchiveFactory.OpenArchive(input, options);
            var entry = archive.Entries.First();
            Assert.Equal(45056, entry.Size);
            Assert.Equal(0, entry.CompressedSize);
            exception = Assert.Throws<InvalidFormatException>(() =>
            {
                using var stream = entry.OpenEntryStream();
            });
        }
        else if (useAsync)
        {
            using var forwardOnly = new NonSeekableStream(input);
            await using var reader = await ReaderFactory.OpenAsyncReader(
                forwardOnly,
                options,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            exception = await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                await reader.WriteEntryToAsync(Stream.Null, CancellationToken.None)
            );
        }
        else
        {
            using var forwardOnly = new NonSeekableStream(input);
            using var reader = ReaderFactory.OpenReader(forwardOnly, options);
            Assert.True(reader.MoveToNextEntry());
            exception = Assert.Throws<InvalidFormatException>(() =>
                reader.WriteEntryTo(Stream.Null)
            );
        }
        Assert.Contains("RAR5 dictionary size", exception.Message);
        Assert.Contains(DictionarySizeLimits.Rar5.ToString(), exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rar5_Custom_Limit_Accounts_For_Minimum_Window_Allocation(bool useAsync)
    {
        using var limits = new DictionarySizeLimitScope();
        var bytes = DecodeHex(Rar256MiB);
        // Change the compression-info vint from 0x2c80 to 0x0080: a 128 KiB
        // declared dictionary. The unpacker requires at least a 256 KiB window.
        bytes[46] = 1;
        var crc = ~Crc32.Update(Crc32.DefaultSeed, bytes.AsSpan(29));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(25), crc);
        foreach (var maximum in new[] { 128L << 10, 256L << 10 })
        {
            using var input = new MemoryStream(bytes);
            DictionarySizeLimits.Rar5 = maximum;
            var options = ReaderOptions.ForExternalStream;
            if (useAsync)
            {
                await using var archive = await ArchiveFactory.OpenAsyncArchive(
                    input,
                    options,
                    CancellationToken.None
                );
                var entry = await archive.EntriesAsync.FirstAsync();
                if (maximum < 256L << 10)
                {
                    await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                    {
                        using var stream = await entry.OpenEntryStreamAsync(CancellationToken.None);
                    });
                }
                else
                {
                    using var stream = await entry.OpenEntryStreamAsync(CancellationToken.None);
                }
            }
            else
            {
                using var archive = ArchiveFactory.OpenArchive(input, options);
                var entry = archive.Entries.First();
                if (maximum < 256L << 10)
                {
                    Assert.Throws<InvalidFormatException>(() =>
                    {
                        using var stream = entry.OpenEntryStream();
                    });
                }
                else
                {
                    using var stream = entry.OpenEntryStream();
                }
            }
        }
    }

    private static byte[] DecodeHex(string hex) =>
        Enumerable
            .Range(0, hex.Length / 2)
            .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))
            .ToArray();
}
