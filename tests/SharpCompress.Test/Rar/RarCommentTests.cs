using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Compressors.Rar;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarCommentTests : TestBase
{
    public static IEnumerable<object[]> Comments()
    {
        // Generated with RAR 7.22: rar a -ma5 -m0 -qo- -zcomment.txt [-hptest|-ptest] archive.rar payload.txt.
        foreach (var useAsync in new[] { false, true })
        {
            yield return ["Rar5.comment.encrypted15.rar", "123456789012345", useAsync];
            yield return ["Rar5.comment.encrypted16.rar", "1234567890123456", useAsync];
            yield return ["Rar5.comment.encrypted17.rar", "123456789012345é", useAsync];
            yield return ["Rar5.comment.plain.rar", "123456789012345é", useAsync];
            yield return ["Rar5.comment.filesOnly.rar", "123456789012345é", useAsync];
        }
    }

    [Theory]
    [MemberData(nameof(Comments))]
    public async Task Rar5_Comment_Archive(string filename, string expected, bool useAsync)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        var options = ReaderOptions.ForExternalStream.WithPassword("test");
        using var output = new MemoryStream();
        if (useAsync)
        {
            await using var archive = await RarArchive.OpenAsyncArchive(stream, options);
            await foreach (var entry in archive.EntriesAsync)
            {
                Assert.Equal("payload.txt", entry.Key);
                await entry.WriteToAsync(output, cancellationToken: CancellationToken.None);
            }
            await foreach (var volume in archive.VolumesAsync)
            {
                Assert.Equal(expected, ((RarVolume)volume).Comment);
            }
        }
        else
        {
            using var archive = RarArchive.OpenArchive(stream, options);
            var entry = Assert.Single(archive.Entries);
            Assert.Equal("payload.txt", entry.Key);
            entry.WriteTo(output);
            Assert.Equal(expected, archive.Volumes.OfType<RarVolume>().First().Comment);
        }
        Assert.Equal("hello from RAR!\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("wrong", false)]
    [InlineData("wrong", true)]
    public async Task Rar5_Comment_Rejects_Invalid_Password(string? password, bool useAsync)
    {
        using var stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar5.comment.encrypted17.rar")
        );
        var options = ReaderOptions.ForExternalStream with { Password = password };
        if (useAsync)
        {
            await Assert.ThrowsAsync<SharpCompress.Common.CryptographicException>(async () =>
            {
                await using var archive = await RarArchive.OpenAsyncArchive(stream, options);
                await foreach (var entry in archive.EntriesAsync)
                {
                    _ = entry.Key;
                }
            });
        }
        else
        {
            Assert.Throws<SharpCompress.Common.CryptographicException>(() =>
            {
                using var archive = RarArchive.OpenArchive(stream, options);
                _ = archive.Entries.ToArray();
            });
        }
    }

    [Theory]
    [MemberData(nameof(Comments))]
    public async Task Rar5_Comment_Reader(string filename, string expected, bool useAsync)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var forwardOnly = new ForwardOnlyStream(stream);
        var options = ReaderOptions.ForExternalStream.WithPassword("test");
        using var output = new MemoryStream();
        if (useAsync)
        {
            await using var reader = await ReaderFactory.OpenAsyncReader(forwardOnly, options);
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            Assert.Equal(expected, ((RarReader)reader).Volume!.Comment);
            await reader.WriteEntryToAsync(output, CancellationToken.None);
            Assert.False(await reader.MoveToNextEntryAsync(CancellationToken.None));
        }
        else
        {
            using var reader = RarReader.OpenReader(forwardOnly, options);
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal(expected, ((RarReader)reader).Volume!.Comment);
            reader.WriteEntryTo(output);
            Assert.False(reader.MoveToNextEntry());
        }
        Assert.Equal("hello from RAR!\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.True(forwardOnly.CanRead);
    }

    [Fact]
    public async Task Rar5_Comment_Volume_Async_Cancellation()
    {
        using var stream = File.OpenRead(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar5.comment.encrypted17.rar")
        );
        using var volume = new StreamRarArchiveVolume(
            stream,
            ReaderOptions.ForExternalStream.WithPassword("test"),
            0
        );
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var part in volume.GetVolumeFilePartsAsync(cancellation.Token))
            {
                _ = part.FileHeader;
            }
        });
    }

    public static IEnumerable<object[]> InvalidCommentSizes()
    {
        foreach (var useAsync in new[] { false, true })
        {
            foreach (var useReader in new[] { false, true })
            {
                foreach (
                    var size in new long[] { 16 * 1024 * 1024 + 1, int.MaxValue, long.MaxValue }
                )
                {
                    yield return [size, useAsync, useReader];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(InvalidCommentSizes))]
    public async Task Rar5_Comment_Rejects_Oversized_Data(long size, bool useAsync, bool useReader)
    {
        using var stream = CreateCommentArchive(size, 0, [1]);
        var exception = await Assert.ThrowsAsync<InvalidFormatException>(() =>
            ReadCommentArchive(stream, useAsync, useReader)
        );
        Assert.Contains("16 MiB", exception.Message);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Rar5_Comment_Allows_Size_Limit(bool useAsync, bool useReader)
    {
        var data = new byte[16 * 1024 * 1024];
        data[0] = (byte)'a';
        using var stream = CreateCommentArchive(data.Length, 0, data);
        Assert.Equal("a", await ReadCommentArchive(stream, useAsync, useReader));
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Rar5_Comment_Rejects_Truncated_Data(bool useAsync, bool useReader)
    {
        using var stream = CreateCommentArchive(10, 0, [1, 2, 3]);
        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            ReadCommentArchive(stream, useAsync, useReader)
        );
        Assert.True(stream.CanRead);
    }

    public static IEnumerable<object[]> UnsupportedCommentMethods()
    {
        foreach (var useAsync in new[] { false, true })
        {
            foreach (var useReader in new[] { false, true })
            {
                foreach (var method in Enumerable.Range(1, 7))
                {
                    yield return [method, useAsync, useReader];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(UnsupportedCommentMethods))]
    public async Task Rar5_Comment_Skips_Nonzero_Methods(int method, bool useAsync, bool useReader)
    {
        // Ignored comments must not allocate from their unpacked size or disturb the next entry.
        using var stream = CreateCommentArchive(int.MaxValue, method, [1, 2, 3]);
        Assert.Null(await ReadCommentArchive(stream, useAsync, useReader));
        Assert.True(stream.CanRead);
    }

    private static async Task<string?> ReadCommentArchive(
        Stream stream,
        bool useAsync,
        bool useReader
    )
    {
        var options = ReaderOptions.ForExternalStream;
        if (useReader)
        {
            using var forwardOnly = new ForwardOnlyStream(
                SharpCompressStream.CreateNonDisposing(stream)
            );
            using var output = new MemoryStream();
            string? comment;
            if (useAsync)
            {
                await using var reader = await RarReader.OpenAsyncReader(
                    forwardOnly,
                    options,
                    CancellationToken.None
                );
                Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
                Assert.Equal("payload.txt", reader.Entry.Key);
                comment = ((RarReader)reader).Volume!.Comment;
                await reader.WriteEntryToAsync(output, CancellationToken.None);
                Assert.False(await reader.MoveToNextEntryAsync(CancellationToken.None));
            }
            else
            {
                using var reader = RarReader.OpenReader(forwardOnly, options);
                Assert.True(reader.MoveToNextEntry());
                Assert.Equal("payload.txt", reader.Entry.Key);
                comment = ((RarReader)reader).Volume!.Comment;
                reader.WriteEntryTo(output);
                Assert.False(reader.MoveToNextEntry());
            }
            Assert.Equal("hello from RAR!\n", Encoding.UTF8.GetString(output.ToArray()));
            return comment;
        }

        using var extracted = new MemoryStream();
        if (useAsync)
        {
            await using var archive = await RarArchive.OpenAsyncArchive(
                stream,
                options,
                CancellationToken.None
            );
            var count = 0;
            await foreach (var entry in archive.EntriesAsync)
            {
                Assert.Equal("payload.txt", entry.Key);
                await entry.WriteToAsync(extracted, cancellationToken: CancellationToken.None);
                count++;
            }
            Assert.Equal(1, count);
            Assert.Equal("hello from RAR!\n", Encoding.UTF8.GetString(extracted.ToArray()));
            await foreach (var volume in archive.VolumesAsync)
            {
                return ((RarVolume)volume).Comment;
            }
            throw new InvalidOperationException("Expected a RAR volume.");
        }

        using var syncArchive = RarArchive.OpenArchive(stream, options);
        var syncEntry = Assert.Single(syncArchive.Entries);
        Assert.Equal("payload.txt", syncEntry.Key);
        syncEntry.WriteTo(extracted);
        Assert.Equal("hello from RAR!\n", Encoding.UTF8.GetString(extracted.ToArray()));
        return syncArchive.Volumes.OfType<RarVolume>().First().Comment;
    }

    private static MemoryStream CreateCommentArchive(long size, int method, byte[] commentData)
    {
        // Build small headers with valid CRCs so malformed sizes reach comment validation.
        var archive = new MemoryStream();
        using var writer = new BinaryWriter(archive, Encoding.UTF8, leaveOpen: true);
        writer.Write(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x01, 0x00 });
        WriteHeader(writer, [1, 0, 0]);

        using var body = new MemoryStream();
        using var header = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true);
        WriteVInts(
            header,
            3,
            2,
            commentData.Length,
            size == long.MaxValue ? 8 : 0,
            size,
            0,
            method << 7,
            1,
            3
        );
        header.Write(Encoding.UTF8.GetBytes("CMT"));
        WriteHeader(writer, body.ToArray());
        writer.Write(commentData);

        body.SetLength(0);
        var payload = Encoding.UTF8.GetBytes("hello from RAR!\n");
        WriteVInts(header, 2, 2, payload.Length, 4, payload.Length, 0);
        header.Write(~RarCRC.CheckCrc(uint.MaxValue, payload, 0, payload.Length));
        WriteVInts(header, 0, 1, 11);
        header.Write(Encoding.UTF8.GetBytes("payload.txt"));
        WriteHeader(writer, body.ToArray());
        writer.Write(payload);
        WriteHeader(writer, [5, 0, 0]);
        archive.Position = 0;
        return archive;
    }

    private static void WriteHeader(BinaryWriter writer, byte[] body)
    {
        using var bytes = new MemoryStream();
        using var header = new BinaryWriter(bytes);
        WriteVInts(header, body.Length);
        header.Write(body);
        var data = bytes.ToArray();
        writer.Write(~RarCRC.CheckCrc(uint.MaxValue, data, 0, data.Length));
        writer.Write(data);
    }

    private static void WriteVInts(BinaryWriter writer, params long[] values)
    {
        foreach (var value in values)
        {
            var remaining = (ulong)value;
            while (remaining >= 0x80)
            {
                writer.Write((byte)((remaining & 0x7f) | 0x80));
                remaining >>= 7;
            }
            writer.Write((byte)remaining);
        }
    }
}
