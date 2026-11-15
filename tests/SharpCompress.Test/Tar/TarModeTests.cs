using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarModeTests
{
    [Fact]
    public void Tar_Metadata_Is_Unspecified_By_Default()
    {
        var metadata = new TarHeader(new ArchiveEncoding()).Metadata;
        Assert.Null(metadata.Mode);
        Assert.Null(metadata.UserId);
        Assert.Null(metadata.GroupId);
    }

    [Theory]
    [InlineData(null, null, null, "777", 0L, 0L)]
    [InlineData("000", null, null, "000", 0L, 0L)]
    [InlineData(null, 42L, null, "777", 42L, 0L)]
    [InlineData(null, null, 73L, "777", 0L, 73L)]
    [InlineData("000", 42L, 73L, "000", 42L, 73L)]
    [InlineData(null, 0L, 0L, "777", 0L, 0L)]
    [InlineData("700", null, 73L, "700", 0L, 73L)]
    [InlineData("100644", 42L, null, "644", 42L, 0L)]
    public async Task TarHeader_Only_Defaults_Unspecified_Metadata(
        string? octalMode,
        long? userId,
        long? groupId,
        string expectedMode,
        long expectedUserId,
        long expectedGroupId
    )
    {
        long? mode = octalMode is null ? null : Convert.ToInt64(octalMode, 8);
        foreach (var useAsync in new[] { false, true })
        {
            var header = new TarHeader(new ArchiveEncoding())
            {
                Name = "entry",
                LastModifiedTime = TarHeader.EPOCH,
                Metadata = new TarEntryMetadata
                {
                    Mode = mode,
                    UserId = userId,
                    GroupId = groupId,
                },
            };
            using var saved = new MemoryStream();
            if (useAsync)
            {
                await header.WriteAsync(saved, CancellationToken.None);
            }
            else
            {
                header.Write(saved);
            }

            var bytes = saved.ToArray();
            Assert.Equal(Convert.ToInt64(expectedMode, 8), ReadOctal(bytes, 100, 8));
            Assert.Equal(expectedUserId, ReadOctal(bytes, 108, 8));
            Assert.Equal(expectedGroupId, ReadOctal(bytes, 116, 8));
            Assert.Equal(mode, header.Metadata.Mode);
            Assert.Equal(userId, header.Metadata.UserId);
            Assert.Equal(groupId, header.Metadata.GroupId);
        }
    }

    public static IEnumerable<object[]> ModeCases()
    {
        foreach (var options in EntryCases())
        {
            foreach (
                var mode in new[] { "0000", "0600", "0700", "0755", "1777", "2770", "4755", "7777" }
            )
            {
                yield return [options[0], options[1], options[2], mode];
            }
        }
    }

    public static IEnumerable<object[]> EntryCases()
    {
        foreach (var options in WriterCases())
        {
            foreach (var isDirectory in new[] { false, true })
            {
                yield return [options[0], options[1], isDirectory];
            }
        }
    }

    public static IEnumerable<object[]> WriterCases()
    {
        foreach (var useAsync in new[] { false, true })
        {
            foreach (
                var format in new[]
                {
                    TarHeaderWriteFormat.USTAR,
                    TarHeaderWriteFormat.GNU_TAR_LONG_LINK,
                }
            )
            {
                yield return [useAsync, format];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ModeCases))]
    public async Task Tar_SaveTo_Keeps_Zero_And_Special_Mode_Bits(
        bool useAsync,
        TarHeaderWriteFormat format,
        bool isDirectory,
        string octalMode
    )
    {
        var mode = Convert.ToInt64(octalMode, 8);
        var name = new string('p', 80) + "/entry" + new string('n', 30) + (isDirectory ? "/" : "");
        using var source = CreateArchive(name, mode, isDirectory);
        using var saved = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true, format);
        using var cancellation = new CancellationTokenSource();

        if (useAsync)
        {
            await using var archive = await TarArchive.OpenAsyncArchive(
                source,
                ReaderOptions.ForExternalStream,
                cancellation.Token
            );
            await archive.SaveToAsync(saved, options, cancellation.Token);
        }
        else
        {
            using var archive = TarArchive.OpenArchive(source, ReaderOptions.ForExternalStream);
            archive.SaveTo(saved, options);
        }

        Assert.True(source.CanRead);
        Assert.True(saved.CanRead);
        // Inspect the raw header: TarEntry.Mode historically adds 01000 to directories,
        // which would otherwise hide accidental sticky-bit additions or removals.
        Assert.Equal(mode, ReadWrittenMode(saved));
        saved.Position = 0;
        using var reread = TarArchive.OpenArchive(saved, ReaderOptions.ForExternalStream);
        var entry = (TarEntry)Assert.Single(reread.Entries);
        Assert.Equal(name, entry.Key);
        Assert.Equal(isDirectory, entry.IsDirectory);
        Assert.Equal(mode, entry.Metadata.Mode);
    }

    [Theory]
    [MemberData(nameof(EntryCases))]
    public async Task Tar_SaveTo_Keeps_Ownership_With_Privileged_Modes(
        bool useAsync,
        TarHeaderWriteFormat format,
        bool isDirectory
    )
    {
        using var source = CreateArchive(
            isDirectory ? "setgid/" : "setuid",
            Convert.ToInt64("6755", 8),
            isDirectory,
            1000,
            1001
        );
        using var saved = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true, format);
        if (useAsync)
        {
            await using var archive = await TarArchive.OpenAsyncArchive(
                source,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            await archive.SaveToAsync(saved, options, CancellationToken.None);
        }
        else
        {
            using var archive = TarArchive.OpenArchive(source, ReaderOptions.ForExternalStream);
            archive.SaveTo(saved, options);
        }

        var bytes = saved.ToArray();
        Assert.Equal(Convert.ToInt64("6755", 8), ReadOctal(bytes, 100, 8));
        Assert.Equal(1000, ReadOctal(bytes, 108, 8));
        Assert.Equal(1001, ReadOctal(bytes, 116, 8));
    }

    [Theory]
    [InlineData(false, 2097152L, 4294967294L)]
    [InlineData(true, 2097152L, 4294967294L)]
    [InlineData(false, 4294967296L, 4611686018427387903L)]
    [InlineData(true, 4294967296L, 4611686018427387903L)]
    [InlineData(false, -1L, -4611686018427387904L)]
    [InlineData(true, -1L, -4611686018427387904L)]
    public async Task Tar_SaveTo_Keeps_Gnu_Binary_Owner_Ids(
        bool useAsync,
        long userId,
        long groupId
    )
    {
        using var source = CreateArchive(
            "owned",
            Convert.ToInt64("755", 8),
            false,
            userId,
            groupId
        );
        using var saved = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true);
        if (useAsync)
        {
            await using var archive = await TarArchive.OpenAsyncArchive(
                source,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            await archive.SaveToAsync(saved, options, CancellationToken.None);
        }
        else
        {
            using var archive = TarArchive.OpenArchive(source, ReaderOptions.ForExternalStream);
            archive.SaveTo(saved, options);
        }

        saved.Position = 0;
        using var reread = TarArchive.OpenArchive(saved, ReaderOptions.ForExternalStream);
        var entry = (TarEntry)Assert.Single(reread.Entries);
        Assert.Equal(userId, entry.UserID);
        Assert.Equal(groupId, entry.GroupId);
    }

    [Theory]
    [InlineData(false, 2097152L, 0L)]
    [InlineData(true, 2097152L, 0L)]
    [InlineData(false, 0L, 2097152L)]
    [InlineData(true, 0L, 2097152L)]
    [InlineData(false, -1L, 0L)]
    [InlineData(true, -1L, 0L)]
    public async Task Tar_SaveTo_Ustar_Rejects_Unrepresentable_Owner_Ids(
        bool useAsync,
        long userId,
        long groupId
    )
    {
        using var source = CreateArchive(
            "owned",
            Convert.ToInt64("755", 8),
            false,
            userId,
            groupId
        );
        using var saved = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true, TarHeaderWriteFormat.USTAR);
        if (useAsync)
        {
            await using var archive = await TarArchive.OpenAsyncArchive(
                source,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                await archive.SaveToAsync(saved, options, CancellationToken.None)
            );
        }
        else
        {
            using var archive = TarArchive.OpenArchive(source, ReaderOptions.ForExternalStream);
            Assert.Throws<InvalidFormatException>(() => archive.SaveTo(saved, options));
        }
    }

    [Theory]
    [InlineData(false, long.MinValue, 0L)]
    [InlineData(true, long.MinValue, 0L)]
    [InlineData(false, 0L, long.MaxValue)]
    [InlineData(true, 0L, long.MaxValue)]
    public async Task TarHeader_Gnu_Rejects_Unrepresentable_Owner_Ids(
        bool useAsync,
        long userId,
        long groupId
    )
    {
        var header = new TarHeader(new ArchiveEncoding())
        {
            Name = "owned",
            Metadata = new TarEntryMetadata { UserId = userId, GroupId = groupId },
        };
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

    [Theory]
    [MemberData(nameof(WriterCases))]
    public async Task Tar_SaveTo_New_Entries_Keep_Default_Mode(
        bool useAsync,
        TarHeaderWriteFormat format
    )
    {
        using var source = CreateArchive("existing", 0, false);
        using var content = new MemoryStream();
        using var saved = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true, format);
        using var cancellation = new CancellationTokenSource();

        if (useAsync)
        {
            await using var archive = await TarArchive.OpenAsyncArchive(
                source,
                ReaderOptions.ForExternalStream,
                cancellation.Token
            );
            await archive.AddDirectoryEntryAsync("new", cancellationToken: cancellation.Token);
            await archive.AddEntryAsync(
                "new/file",
                content,
                false,
                cancellationToken: cancellation.Token
            );
            await archive.SaveToAsync(saved, options, cancellation.Token);
        }
        else
        {
            using var archive = TarArchive.OpenArchive(source, ReaderOptions.ForExternalStream);
            archive.AddDirectoryEntry("new");
            archive.AddEntry("new/file", content, false);
            archive.SaveTo(saved, options);
        }

        var bytes = saved.ToArray();
        Assert.Equal(0, ReadOctal(bytes, 100, 8));
        Assert.Equal(Convert.ToInt64("777", 8), ReadOctal(bytes, 512 + 100, 8));
        Assert.Equal(Convert.ToInt64("777", 8), ReadOctal(bytes, 1024 + 100, 8));
        Assert.Equal(0, ReadOctal(bytes, 512 + 108, 8));
        Assert.Equal(0, ReadOctal(bytes, 512 + 116, 8));
        Assert.Equal(0, ReadOctal(bytes, 1024 + 108, 8));
        Assert.Equal(0, ReadOctal(bytes, 1024 + 116, 8));
        Assert.True(source.CanRead);
        Assert.True(content.CanRead);
        Assert.True(saved.CanRead);
    }

    [Theory]
    [MemberData(nameof(WriterCases))]
    public async Task TarWriter_New_Entries_Keep_Default_Mode(
        bool useAsync,
        TarHeaderWriteFormat format
    )
    {
        using var saved = new MemoryStream();
        using var content = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true, format);
        using var cancellation = new CancellationTokenSource();
        if (useAsync)
        {
            await using var writer = new TarWriter(saved, options);
            await writer.WriteDirectoryAsync("new", null, cancellation.Token);
            await writer.WriteAsync("new/file", content, null, null, cancellation.Token);
        }
        else
        {
            using var writer = new TarWriter(saved, options);
            writer.WriteDirectory("new", null);
            writer.Write("new/file", content, null, null);
        }

        var bytes = saved.ToArray();
        Assert.Equal(Convert.ToInt64("777", 8), ReadOctal(bytes, 100, 8));
        Assert.Equal(Convert.ToInt64("777", 8), ReadOctal(bytes, 512 + 100, 8));
        Assert.Equal(0, ReadOctal(bytes, 108, 8));
        Assert.Equal(0, ReadOctal(bytes, 116, 8));
        Assert.Equal(0, ReadOctal(bytes, 512 + 108, 8));
        Assert.Equal(0, ReadOctal(bytes, 512 + 116, 8));
        Assert.True(saved.CanRead);
        Assert.True(content.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TarReader_Directory_Mode_Keeps_Legacy_Marker(bool useAsync)
    {
        using var source = CreateArchive("private/", Convert.ToInt64("700", 8), true);
        using var cancellation = new CancellationTokenSource();
        if (useAsync)
        {
            await using var reader = await TarReader.OpenAsyncReader(
                source,
                ReaderOptions.ForExternalStream,
                cancellation.Token
            );
            Assert.True(await reader.MoveToNextEntryAsync(cancellation.Token));
            Assert.Equal(Convert.ToInt64("1700", 8), ((TarEntry)reader.Entry).Mode);
        }
        else
        {
            using var reader = TarReader.OpenReader(source, ReaderOptions.ForExternalStream);
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal(Convert.ToInt64("1700", 8), ((TarEntry)reader.Entry).Mode);
        }
    }

    private static MemoryStream CreateArchive(
        string name,
        long mode,
        bool isDirectory,
        long userId = 0,
        long groupId = 0
    )
    {
        // Build an independent USTAR fixture so the writer under test cannot silently
        // normalize a mode before the round trip starts.
        var bytes = new byte[1536];
        var split = name.TrimEnd('/').LastIndexOf('/');
        if (name.Length > 100)
        {
            Encoding.ASCII.GetBytes(name.Substring(0, split)).CopyTo(bytes, 345);
            Encoding.ASCII.GetBytes(name.Substring(split + 1)).CopyTo(bytes, 0);
        }
        else
        {
            Encoding.ASCII.GetBytes(name).CopyTo(bytes, 0);
        }
        Encoding.ASCII.GetBytes(Convert.ToString(mode, 8).PadLeft(7, '0')).CopyTo(bytes, 100);
        WriteOwnerId(bytes, 108, userId);
        WriteOwnerId(bytes, 116, groupId);
        Encoding.ASCII.GetBytes("00000000000").CopyTo(bytes, 124);
        Encoding.ASCII.GetBytes("00000000000").CopyTo(bytes, 136);
        bytes[156] = (byte)(isDirectory ? '5' : '0');
        Encoding.ASCII.GetBytes("ustar\0" + "00").CopyTo(bytes, 257);
        for (var i = 148; i < 156; i++)
        {
            bytes[i] = (byte)' ';
        }
        var checksum = bytes.Sum(value => (int)value);
        Encoding
            .ASCII.GetBytes(Convert.ToString(checksum, 8).PadLeft(6, '0') + "\0 ")
            .CopyTo(bytes, 148);
        return new MemoryStream(bytes);
    }

    private static void WriteOwnerId(byte[] bytes, int offset, long value)
    {
        if (value is >= 0 and <= 2_097_151)
        {
            Encoding
                .ASCII.GetBytes(Convert.ToString(value, 8).PadLeft(7, '0'))
                .CopyTo(bytes, offset);
        }
        else
        {
            BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(offset, 8), value);
            bytes[offset] |= 0x80;
        }
    }

    private static long ReadWrittenMode(MemoryStream stream)
    {
        var bytes = stream.ToArray();
        var offset = 0;
        if (bytes[156] == (byte)'L')
        {
            var length = ReadOctal(bytes, 124, 12);
            offset = 512 + (int)((length + 511) / 512 * 512);
        }
        return ReadOctal(bytes, offset + 100, 8);
    }

    private static long ReadOctal(byte[] bytes, int offset, int count) =>
        Convert.ToInt64(Encoding.ASCII.GetString(bytes, offset, count).Trim('\0', ' '), 8);
}
