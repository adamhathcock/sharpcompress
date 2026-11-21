using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarHeaderNumberRangeTests
{
    // 1969-12-31 23:59:59 is -1, the smallest value that used to spill out of
    // the twelve-byte mtime field. 2300 is past the largest eleven-digit octal
    // value (2242-03-16).
    public static IEnumerable<object[]> OutOfOctalRangeTimes()
    {
        foreach (var useAsync in new[] { false, true })
        {
            yield return [useAsync, new DateTime(1969, 12, 31, 23, 59, 59, DateTimeKind.Utc)];
            yield return [useAsync, new DateTime(1901, 12, 13, 20, 45, 52, DateTimeKind.Utc)];
            yield return [useAsync, new DateTime(2300, 1, 1, 0, 0, 0, DateTimeKind.Utc)];
        }
    }

    [Theory]
    [MemberData(nameof(OutOfOctalRangeTimes))]
    public async Task Gnu_Writer_Round_Trips_Times_Outside_Octal_Range(
        bool useAsync,
        DateTime modified
    )
    {
        var content = Encoding.ASCII.GetBytes("hello");
        using var saved = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true);
        if (useAsync)
        {
            await using (var writer = new TarWriter(new AsyncOnlyStream(saved, false), options))
            {
                await writer.WriteAsync(
                    "old.txt",
                    new MemoryStream(content),
                    modified,
                    CancellationToken.None
                );
            }
        }
        else
        {
            using var writer = new TarWriter(saved, options);
            writer.Write("old.txt", new MemoryStream(content), modified);
        }

        saved.Position = 0;
        using (var archive = TarArchive.OpenArchive(saved, ReaderOptions.ForExternalStream))
        {
            var entry = (TarEntry)Assert.Single(archive.Entries);
            Assert.Equal("old.txt", entry.Key);
            Assert.Equal(content.Length, entry.Size);
            Assert.Equal(modified, entry.LastModifiedTime!.Value.ToUniversalTime());
        }

        saved.Position = 0;
        if (useAsync)
        {
            await using var reader = await TarReader.OpenAsyncReader(
                new AsyncOnlyStream(saved, false),
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            Assert.Equal(modified, reader.Entry.LastModifiedTime!.Value.ToUniversalTime());
            Assert.Equal(content.Length, reader.Entry.Size);
        }
        else
        {
            using var reader = TarReader.OpenReader(saved, ReaderOptions.ForExternalStream);
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal(modified, reader.Entry.LastModifiedTime!.Value.ToUniversalTime());
            Assert.Equal(content.Length, reader.Entry.Size);
        }
    }

    [Theory]
    [MemberData(nameof(OutOfOctalRangeTimes))]
    public async Task Ustar_Header_Rejects_Times_Outside_Octal_Range(
        bool useAsync,
        DateTime modified
    )
    {
        var header = new TarHeader(new ArchiveEncoding(), TarHeaderWriteFormat.USTAR)
        {
            Name = "old.txt",
            Size = 5,
            LastModifiedTime = modified,
        };

        await AssertWriteRejected(header, useAsync);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ustar_Header_Rejects_Sizes_Over_Eleven_Octal_Digits(bool useAsync)
    {
        var header = new TarHeader(new ArchiveEncoding(), TarHeaderWriteFormat.USTAR)
        {
            Name = "big.bin",
            Size = 0x200000000, // 8 GiB, twelve octal digits
            LastModifiedTime = TarHeader.EPOCH,
        };

        await AssertWriteRejected(header, useAsync);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Gnu_Header_Large_Size_Leaves_Group_Id_Field_Alone(bool useAsync)
    {
        var header = new TarHeader(new ArchiveEncoding())
        {
            Name = "big.bin",
            Size = 0x200000000,
            LastModifiedTime = TarHeader.EPOCH,
            Metadata = new TarEntryMetadata { GroupId = 0 },
        };

        var bytes = await WriteHeader(header, useAsync);

        // The gid field is bytes 116..123; the size field starts at 124.
        Assert.Equal(0, bytes[123]);
        Assert.Equal(0x80, bytes[124]);
        Assert.True(TarHeader.checkChecksum(bytes));
    }

    private static async Task AssertWriteRejected(TarHeader header, bool useAsync)
    {
        using var saved = new MemoryStream();
        if (useAsync)
        {
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                await header.WriteAsync(saved, CancellationToken.None)
            );
        }
        else
        {
            Assert.Throws<InvalidFormatException>(() => header.Write(saved));
        }
        Assert.Equal(0, saved.Length);
    }

    private static async Task<byte[]> WriteHeader(TarHeader header, bool useAsync)
    {
        using var saved = new MemoryStream();
        if (useAsync)
        {
            await header.WriteAsync(saved, CancellationToken.None);
        }
        else
        {
            header.Write(saved);
        }
        return saved.ToArray();
    }
}
