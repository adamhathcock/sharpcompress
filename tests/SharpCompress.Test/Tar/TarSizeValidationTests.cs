using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarSizeValidationTests
{
    [Theory]
    [InlineData(-1L, false)]
    [InlineData(-512L, false)]
    [InlineData(-1024L, false)]
    [InlineData(long.MinValue, false)]
    [InlineData(long.MaxValue, false)]
    [InlineData(TarHeader.MAX_ENTRY_SIZE + 1, false)]
    [InlineData(-1L, true)]
    [InlineData(-512L, true)]
    [InlineData(-1024L, true)]
    [InlineData(long.MinValue, true)]
    public async Task Tar_Header_Rejects_Invalid_Size(long size, bool negativeMarker)
    {
        using var stream = new MemoryStream(CreateHeader(size, negativeMarker));
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var header = new TarHeader(new ArchiveEncoding());

        Assert.Throws<InvalidFormatException>(() => header.Read(reader));

        stream.Position = 0;
        using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
        using var asyncReader = new AsyncBinaryReader(asyncStream, leaveOpen: true);
        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await header.ReadAsync(asyncReader, cancellationToken: CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(TarHeader.MAX_ENTRY_SIZE - 1)]
    [InlineData(TarHeader.MAX_ENTRY_SIZE)]
    public async Task Tar_Header_Accepts_Maximum_Paddable_Size(long size)
    {
        using var stream = new MemoryStream(CreateHeader(size));
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var header = new TarHeader(new ArchiveEncoding());

        Assert.True(header.Read(reader));
        Assert.Equal(size, header.Size);

        stream.Position = 0;
        using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
        using var asyncReader = new AsyncBinaryReader(asyncStream, leaveOpen: true);
        Assert.True(await header.ReadAsync(asyncReader, cancellationToken: CancellationToken.None));
        Assert.Equal(size, header.Size);
    }

    [Theory]
    [InlineData(124, false)]
    [InlineData(125, false)]
    [InlineData(126, false)]
    [InlineData(127, false)]
    [InlineData(124, true)]
    [InlineData(125, true)]
    [InlineData(126, true)]
    [InlineData(127, true)]
    public async Task Tar_ArchiveFactory_Rejects_Binary_Size_With_Ignored_High_Bits(
        int offset,
        bool useAsync
    )
    {
        var header = CreateHeader(0);
        // Keep the binary marker, but encode a size larger than a long can hold.
        header[offset] |= 0x01;
        WriteChecksum(header);
        using var stream = new MemoryStream(header);
        if (useAsync)
        {
            using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
            await Assert.ThrowsAsync<ArchiveOperationException>(async () =>
            {
                await using var archive = await ArchiveFactory.OpenAsyncArchive(
                    asyncStream,
                    ReaderOptions.ForExternalStream,
                    CancellationToken.None
                );
            });
        }
        else
        {
            Assert.Throws<ArchiveOperationException>(() =>
            {
                using var archive = ArchiveFactory.OpenArchive(
                    stream,
                    ReaderOptions.ForExternalStream
                );
            });
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tar_ArchiveFactory_Rejects_Negative_First_Entry(bool useAsync)
    {
        // The advisory payload is just one checksum-valid header with size -512.
        using var stream = new MemoryStream(CreateHeader(-512));
        if (useAsync)
        {
            using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
            await Assert.ThrowsAsync<ArchiveOperationException>(async () =>
            {
                await using var archive = await ArchiveFactory.OpenAsyncArchive(
                    asyncStream,
                    ReaderOptions.ForExternalStream,
                    CancellationToken.None
                );
            });
        }
        else
        {
            Assert.Throws<ArchiveOperationException>(() =>
            {
                using var archive = ArchiveFactory.OpenArchive(
                    stream,
                    ReaderOptions.ForExternalStream
                );
            });
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(-512L, false)]
    [InlineData(-1024L, false)]
    [InlineData(long.MinValue, false)]
    [InlineData(long.MaxValue, false)]
    [InlineData(long.MaxValue - 511, false)]
    [InlineData(-1L, true)]
    [InlineData(-512L, true)]
    [InlineData(-1024L, true)]
    [InlineData(long.MinValue, true)]
    [InlineData(long.MaxValue, true)]
    [InlineData(long.MaxValue - 511, true)]
    public async Task Tar_Archive_Rejects_Invalid_Entry_Size(long size, bool useAsync)
    {
        using var stream = CreateArchive(size);
        // A valid first entry bypasses format detection so enumeration must validate
        // the next header. Only advance twice to keep regressions from looping.
        if (useAsync)
        {
            using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
            await using var archive = await ArchiveFactory.OpenAsyncArchive(
                asyncStream,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            await using var entries = archive.EntriesAsync.GetAsyncEnumerator(
                CancellationToken.None
            );
            Assert.True(await entries.MoveNextAsync());
            await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
                await entries.MoveNextAsync()
            );
        }
        else
        {
            using var archive = ArchiveFactory.OpenArchive(stream, ReaderOptions.ForExternalStream);
            using var entries = archive.Entries.GetEnumerator();
            Assert.True(entries.MoveNext());
            Assert.Throws<IncompleteArchiveException>(() => entries.MoveNext());
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tar_Reader_Rejects_Negative_Entry_Size(bool useAsync)
    {
        using var stream = new ForwardOnlyStream(CreateArchive(-512));
        if (useAsync)
        {
            using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
            await using var reader = await ReaderFactory.OpenAsyncReader(
                asyncStream,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
                await reader.MoveToNextEntryAsync(CancellationToken.None)
            );
        }
        else
        {
            using var reader = ReaderFactory.OpenReader(stream, ReaderOptions.ForExternalStream);
            Assert.True(reader.MoveToNextEntry());
            Assert.Throws<IncompleteArchiveException>(() => reader.MoveToNextEntry());
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(511, false)]
    [InlineData(512, false)]
    [InlineData(513, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(511, true)]
    [InlineData(512, true)]
    [InlineData(513, true)]
    public async Task Tar_Archive_Preserves_Nonnegative_Binary_Sizes(int size, bool useAsync)
    {
        using var stream = CreateArchive(size);
        if (useAsync)
        {
            using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
            await using var archive = await ArchiveFactory.OpenAsyncArchive(
                asyncStream,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            await using var entries = archive.EntriesAsync.GetAsyncEnumerator(
                CancellationToken.None
            );
            Assert.True(await entries.MoveNextAsync());
            Assert.Equal(0, entries.Current.Size);
            Assert.True(await entries.MoveNextAsync());
            Assert.Equal(size, entries.Current.Size);
            Assert.False(await entries.MoveNextAsync());
        }
        else
        {
            using var archive = ArchiveFactory.OpenArchive(stream, ReaderOptions.ForExternalStream);
            using var entries = archive.Entries.GetEnumerator();
            Assert.True(entries.MoveNext());
            Assert.Equal(0, entries.Current.Size);
            Assert.True(entries.MoveNext());
            Assert.Equal(size, entries.Current.Size);
            Assert.False(entries.MoveNext());
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(511, false)]
    [InlineData(512, false)]
    [InlineData(513, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(511, true)]
    [InlineData(512, true)]
    [InlineData(513, true)]
    public async Task Tar_Reader_Preserves_Nonnegative_Binary_Sizes(int size, bool useAsync)
    {
        using var stream = new ForwardOnlyStream(CreateArchive(size));
        if (useAsync)
        {
            using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
            await using var reader = await ReaderFactory.OpenAsyncReader(
                asyncStream,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            Assert.Equal(0, reader.Entry.Size);
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            Assert.Equal(size, reader.Entry.Size);
            Assert.False(await reader.MoveToNextEntryAsync(CancellationToken.None));
        }
        else
        {
            using var reader = ReaderFactory.OpenReader(stream, ReaderOptions.ForExternalStream);
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal(0, reader.Entry.Size);
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal(size, reader.Entry.Size);
            Assert.False(reader.MoveToNextEntry());
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(long.MaxValue, false)]
    [InlineData(long.MaxValue, true)]
    [InlineData(TarHeader.MAX_ENTRY_SIZE + 1, false)]
    [InlineData(TarHeader.MAX_ENTRY_SIZE + 1, true)]
    [InlineData(513L, false)]
    [InlineData(513L, true)]
    public async Task Tar_Reader_Validates_Effective_Pax_Size(long size, bool useAsync)
    {
        using var data = CreatePaxArchive(size);
        using var stream = new ForwardOnlyStream(data);
        var invalid = size > TarHeader.MAX_ENTRY_SIZE;
        if (useAsync)
        {
            using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
            await using var reader = await ReaderFactory.OpenAsyncReader(
                asyncStream,
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            if (invalid)
            {
                await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
                    await reader.MoveToNextEntryAsync(CancellationToken.None)
                );
            }
            else
            {
                Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
                Assert.Equal(size, reader.Entry.Size);
                await reader.WriteEntryToAsync(Stream.Null, CancellationToken.None);
                Assert.False(await reader.MoveToNextEntryAsync(CancellationToken.None));
            }
        }
        else
        {
            using var reader = ReaderFactory.OpenReader(stream, ReaderOptions.ForExternalStream);
            Assert.True(reader.MoveToNextEntry());
            if (invalid)
            {
                Assert.Throws<IncompleteArchiveException>(() => reader.MoveToNextEntry());
            }
            else
            {
                Assert.True(reader.MoveToNextEntry());
                Assert.Equal(size, reader.Entry.Size);
                reader.WriteEntryTo(Stream.Null);
                Assert.False(reader.MoveToNextEntry());
            }
        }
        Assert.True(stream.CanRead);
    }

    private static MemoryStream CreatePaxArchive(long size)
    {
        var record = $" size={size.ToString(CultureInfo.InvariantCulture)}\n";
        var length = record.Length + 1;
        while (length != record.Length + length.ToString(CultureInfo.InvariantCulture).Length)
        {
            length = record.Length + length.ToString(CultureInfo.InvariantCulture).Length;
        }
        var payload = Encoding.ASCII.GetBytes(
            length.ToString(CultureInfo.InvariantCulture) + record
        );
        var paxHeader = CreateHeader(payload.Length);
        paxHeader[156] = (byte)'x';
        WriteChecksum(paxHeader);
        var stream = new MemoryStream();
        var firstHeader = CreateHeader(0);
        stream.Write(firstHeader, 0, firstHeader.Length);
        stream.Write(paxHeader, 0, paxHeader.Length);
        stream.Write(payload, 0, payload.Length);
        stream.SetLength(stream.Length + ((512 - payload.Length % 512) % 512));
        stream.Position = stream.Length;
        var header = CreateHeader(0);
        stream.Write(header, 0, header.Length);
        if (size >= 0 && size <= 1024)
        {
            stream.SetLength(stream.Length + ((size + 511) / 512 * 512));
        }
        stream.SetLength(stream.Length + 1024);
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateArchive(long size)
    {
        var stream = new MemoryStream();
        var firstHeader = CreateHeader(0);
        stream.Write(firstHeader, 0, firstHeader.Length);
        var header = CreateHeader(size);
        stream.Write(header, 0, header.Length);
        // Invalid sizes need no payload, and must never cause large test allocations.
        if (size >= 0 && size <= 1024)
        {
            stream.SetLength(stream.Length + ((size + 511) / 512 * 512));
        }
        stream.SetLength(stream.Length + 1024);
        stream.Position = 0;
        return stream;
    }

    private static byte[] CreateHeader(long size, bool negativeMarker = false)
    {
        var header = new byte[512];
        header[0] = (byte)'x';
        header[124] = 0x80;
        if (negativeMarker)
        {
            // Cover GNU negative encoding as well as the advisory's 0x80 prefix.
            for (var i = 124; i < 128; i++)
            {
                header[i] = 0xff;
            }
        }
        BinaryPrimitives.WriteInt64BigEndian(header.AsSpan(128, 8), size);
        header[156] = (byte)'0';
        Encoding.ASCII.GetBytes("ustar\0" + "00").CopyTo(header, 257);
        WriteChecksum(header);
        return header;
    }

    private static void WriteChecksum(byte[] header)
    {
        var checksum = TarHeader.RecalculateChecksum(header);
        Encoding
            .ASCII.GetBytes(Convert.ToString(checksum, 8).PadLeft(6, '0') + "\0 ")
            .CopyTo(header, 148);
    }
}
