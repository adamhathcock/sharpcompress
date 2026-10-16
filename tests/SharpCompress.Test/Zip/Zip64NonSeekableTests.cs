using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class Zip64NonSeekableTests
{
    private static readonly DateTime ModificationTime = new(2024, 5, 15, 10, 30, 0);

    [Theory]
    [InlineData(false, CompressionType.None)]
    [InlineData(true, CompressionType.None)]
    [InlineData(false, CompressionType.Deflate)]
    [InlineData(true, CompressionType.Deflate)]
    [InlineData(false, CompressionType.BZip2)]
    [InlineData(true, CompressionType.BZip2)]
    [InlineData(false, CompressionType.LZMA)]
    [InlineData(true, CompressionType.LZMA)]
    [InlineData(false, CompressionType.PPMd)]
    [InlineData(true, CompressionType.PPMd)]
    [InlineData(false, CompressionType.ZStandard)]
    [InlineData(true, CompressionType.ZStandard)]
    public async Task Zip64_NonSeekable_RoundTrip(bool useAsync, CompressionType compressionType)
    {
        var entries = new (string Name, byte[] Content)[]
        {
            ("empty.txt", []),
            ("nested/first.txt", Encoding.UTF8.GetBytes(new string('a', 10000))),
            ("second.txt", Encoding.UTF8.GetBytes("A trailing entry with different content.")),
        };
        using var output = new MemoryStream();
        using var forwardOnly = new ForwardOnlyStream(output);
        var options = new ZipWriterOptions(compressionType)
        {
            UseZip64 = true,
            LeaveStreamOpen = true,
        };
        if (useAsync)
        {
            await using var writer = await WriterFactory.OpenAsyncWriter(
                new AsyncOnlyStream(forwardOnly),
                ArchiveType.Zip,
                options,
                CancellationToken.None
            );
            await writer.WriteDirectoryAsync("nested", ModificationTime, CancellationToken.None);
            foreach (var (name, content) in entries)
            {
                using var source = new MemoryStream(content);
                await writer.WriteAsync(name, source, ModificationTime, CancellationToken.None);
            }
        }
        else
        {
            using var writer = WriterFactory.OpenWriter(forwardOnly, ArchiveType.Zip, options);
            writer.WriteDirectory("nested", ModificationTime);
            foreach (var (name, content) in entries)
            {
                using var source = new MemoryStream(content);
                writer.Write(name, source, ModificationTime);
            }
        }

        Assert.True(forwardOnly.CanWrite);
        var zip = output.ToArray();
        AssertLayout(zip, [true, true, true, true]);

        using (var archiveStream = new MemoryStream(zip))
        using (
            var archive = ArchiveFactory.OpenArchive(archiveStream, ReaderOptions.ForExternalStream)
        )
        {
            Assert.Equal(entries.Length + 1, archive.Entries.Count());
            foreach (var (name, content) in entries)
            {
                var entry = archive.Entries.Single(e => e.Key == name);
                Assert.Equal(content.Length, entry.Size);
                using var source = entry.OpenEntryStream();
                using var extracted = new MemoryStream();
                source.CopyTo(extracted);
                Assert.Equal(content, extracted.ToArray());
            }
        }

        // ZStandard's streaming decoder reads beyond the frame into the ZIP descriptor.
        // Verify that method through the seekable Archive API above instead.
        if (compressionType == CompressionType.ZStandard)
        {
            return;
        }

        using (var input = new ForwardOnlyStream(new MemoryStream(zip)))
        using (var reader = ReaderFactory.OpenReader(input, ReaderOptions.ForExternalStream))
        {
            var index = 0;
            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.IsDirectory)
                {
                    continue;
                }
                Assert.Equal(entries[index].Name, reader.Entry.Key);
                using var source = reader.OpenEntryStream();
                using var extracted = new MemoryStream();
                source.CopyTo(extracted);
                Assert.Equal(entries[index++].Content, extracted.ToArray());
            }
            Assert.Equal(entries.Length, index);
        }

        // Stored streaming entries currently use DataDescriptorStream's synchronous scanner.
        Stream asyncInput = new ForwardOnlyStream(new MemoryStream(zip));
        if (compressionType != CompressionType.None)
        {
            asyncInput = new AsyncOnlyStream(asyncInput);
        }
        await using (
            var reader = await ReaderFactory.OpenAsyncReader(
                asyncInput,
                new ReaderOptions(),
                CancellationToken.None
            )
        )
        {
            var index = 0;
            while (await reader.MoveToNextEntryAsync(CancellationToken.None))
            {
                if (reader.Entry.IsDirectory)
                {
                    continue;
                }
                Assert.Equal(entries[index].Name, reader.Entry.Key);
#if LEGACY_DOTNET
                using var source = await reader.OpenEntryStreamAsync(CancellationToken.None);
#else
                await using var source = await reader.OpenEntryStreamAsync(CancellationToken.None);
#endif
                using var extracted = new MemoryStream();
                await source.CopyToAsync(extracted, 81920, CancellationToken.None);
                Assert.Equal(entries[index++].Content, extracted.ToArray());
            }
            Assert.Equal(entries.Length, index);
        }

#if !LEGACY_DOTNET
        if (compressionType is CompressionType.None or CompressionType.Deflate)
        {
            using var archiveStream = new MemoryStream(zip);
            using var archive = new System.IO.Compression.ZipArchive(archiveStream);
            Assert.Equal(entries.Length + 1, archive.Entries.Count);
            foreach (var (name, content) in entries)
            {
                using var source = archive.GetEntry(name)!.Open();
                using var extracted = new MemoryStream();
                source.CopyTo(extracted);
                Assert.Equal(content, extracted.ToArray());
            }
        }
#endif
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Zip64_NonSeekable_PerEntry_Overrides(bool useAsync, bool archiveZip64)
    {
        using var output = new MemoryStream();
        using var forwardOnly = new ForwardOnlyStream(output);
        var options = new ZipWriterOptions(CompressionType.Deflate) { UseZip64 = archiveZip64 };
        if (useAsync)
        {
            await using var writer = (ZipWriter)
                await WriterFactory.OpenAsyncWriter(
                    new AsyncOnlyStream(forwardOnly),
                    ArchiveType.Zip,
                    options,
                    CancellationToken.None
                );
            foreach (var enableZip64 in new[] { true, false })
            {
                using var source = new MemoryStream([1, 2, 3]);
                await writer.WriteAsync(
                    enableZip64.ToString(),
                    source,
                    new ZipWriterEntryOptions
                    {
                        EnableZip64 = enableZip64,
                        ModificationDateTime = ModificationTime,
                    },
                    CancellationToken.None
                );
            }
        }
        else
        {
            using var writer = (ZipWriter)
                WriterFactory.OpenWriter(forwardOnly, ArchiveType.Zip, options);
            foreach (var enableZip64 in new[] { true, false })
            {
                using var source = new MemoryStream([1, 2, 3]);
                writer.Write(
                    enableZip64.ToString(),
                    source,
                    new ZipWriterEntryOptions
                    {
                        EnableZip64 = enableZip64,
                        ModificationDateTime = ModificationTime,
                    }
                );
            }
        }
        AssertLayout(output.ToArray(), [true, false]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Zip64_NonSeekable_Closes_Owned_Stream(bool useAsync)
    {
        using var output = new MemoryStream();
        var options = new ZipWriterOptions(CompressionType.Deflate)
        {
            UseZip64 = true,
            LeaveStreamOpen = false,
        };
        using var source = new MemoryStream([1, 2, 3]);
        if (useAsync)
        {
            await using var writer = await WriterFactory.OpenAsyncWriter(
                new AsyncOnlyStream(new ForwardOnlyStream(output)),
                ArchiveType.Zip,
                options,
                CancellationToken.None
            );
            await writer.WriteAsync("entry", source, ModificationTime, CancellationToken.None);
        }
        else
        {
            using var writer = WriterFactory.OpenWriter(
                new ForwardOnlyStream(output),
                ArchiveType.Zip,
                options
            );
            writer.Write("entry", source, ModificationTime);
        }
        Assert.False(output.CanWrite);
    }

    [Theory]
    [InlineData(0xFFFFFFFEUL)]
    [InlineData(0xFFFFFFFFUL)]
    [InlineData(0x100000000UL)]
    [InlineData(0x200000001UL)]
    public void Zip64_Descriptor_And_CentralDirectory_Preserve_64Bit_Sizes(ulong size)
    {
        // Exercise size boundaries without allocating or compressing multi-gigabyte payloads.
        var entry = new ZipCentralDirectoryEntry(
            ZipCompressionMethod.Deflate,
            "entry",
            size,
            new ArchiveEncoding()
        )
        {
            UsesZip64 = true,
            UsesDataDescriptor = true,
            Crc = 0x12345678,
            Compressed = size,
            Decompressed = size + 1,
        };
        var descriptor = entry.CreateDataDescriptor();
        Assert.Equal(24, descriptor.Length);
        Assert.Equal(0x08074b50u, ReadUInt32(descriptor, 0));
        Assert.Equal(entry.Crc, ReadUInt32(descriptor, 4));
        Assert.Equal(size, ReadUInt64(descriptor, 8));
        Assert.Equal(size + 1, ReadUInt64(descriptor, 16));

        using var output = new MemoryStream();
        entry.Write(output);
        var directory = output.ToArray();
        Assert.Equal(8, ReadUInt16(directory, 8) & 8);
        Assert.Equal(uint.MaxValue, ReadUInt32(directory, 20));
        Assert.Equal(uint.MaxValue, ReadUInt32(directory, 24));
        Assert.Equal(uint.MaxValue, ReadUInt32(directory, 42));
        Assert.Equal(28, ReadUInt16(directory, 30));
        Assert.Equal(0, ReadUInt16(directory, 34));
        var extra = 46 + ReadUInt16(directory, 28);
        Assert.Equal(24, ReadUInt16(directory, extra + 2));
        Assert.Equal(size + 1, ReadUInt64(directory, extra + 4));
        Assert.Equal(size, ReadUInt64(directory, extra + 12));
        Assert.Equal(size, ReadUInt64(directory, extra + 20));

        entry.UsesZip64 = false;
        Assert.Throws<NotSupportedException>(() => entry.CreateDataDescriptor());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Zip64_NonSeekable_Empty_Archive(bool useAsync)
    {
        using var output = new MemoryStream();
        using var forwardOnly = new ForwardOnlyStream(output);
        var options = new ZipWriterOptions(CompressionType.Deflate) { UseZip64 = true };
        if (useAsync)
        {
            await using var writer = await WriterFactory.OpenAsyncWriter(
                new AsyncOnlyStream(forwardOnly),
                ArchiveType.Zip,
                options,
                CancellationToken.None
            );
        }
        else
        {
            using var writer = WriterFactory.OpenWriter(forwardOnly, ArchiveType.Zip, options);
        }
        AssertLayout(output.ToArray(), []);
    }

    [Fact]
    public async Task Zip64_NonSeekable_Cancelled_Write_Does_Not_Write_Header()
    {
        using var output = new MemoryStream();
        using var forwardOnly = new ForwardOnlyStream(output);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var source = new MemoryStream([1, 2, 3]);
        await using (
            var writer = await WriterFactory.OpenAsyncWriter(
                new AsyncOnlyStream(forwardOnly),
                ArchiveType.Zip,
                new ZipWriterOptions(CompressionType.Deflate) { UseZip64 = true },
                CancellationToken.None
            )
        )
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await writer.WriteAsync("cancelled", source, ModificationTime, cancellation.Token)
            );
            Assert.Equal(0, output.Length);
            Assert.Equal(0, source.Position);
            await writer.WriteAsync("valid", source, ModificationTime, CancellationToken.None);
        }
        AssertLayout(output.ToArray(), [true]);
    }

    private static void AssertLayout(byte[] zip, bool[] zip64Entries)
    {
        var end = zip.Length - 22;
        Assert.Equal(0x06054b50u, ReadUInt32(zip, end));
        Assert.Equal(zip64Entries.Length, ReadUInt16(zip, end + 10));
        var central = checked((int)ReadUInt32(zip, end + 16));
        var nextLocal = 0;
        foreach (var zip64 in zip64Entries)
        {
            Assert.Equal(0x02014b50u, ReadUInt32(zip, central));
            var nameLength = ReadUInt16(zip, central + 28);
            var extraLength = ReadUInt16(zip, central + 30);
            var commentLength = ReadUInt16(zip, central + 32);
            var name = Encoding.UTF8.GetString(zip, central + 46, nameLength);
            var isDirectory = name.EndsWith('/');
            var centralExtra = central + 46 + nameLength;
            var compressed = (ulong)ReadUInt32(zip, central + 20);
            var uncompressed = (ulong)ReadUInt32(zip, central + 24);
            var local = ReadUInt32(zip, central + 42);
            if (zip64 && !isDirectory)
            {
                Assert.Equal(uint.MaxValue, compressed);
                Assert.Equal(uint.MaxValue, uncompressed);
                Assert.Equal(uint.MaxValue, local);
                Assert.Equal(1, ReadUInt16(zip, centralExtra));
                uncompressed = ReadUInt64(zip, centralExtra + 4);
                compressed = ReadUInt64(zip, centralExtra + 12);
                local = checked((uint)ReadUInt64(zip, centralExtra + 20));
            }
            Assert.Equal(nextLocal, (int)local);
            Assert.Equal(0x04034b50u, ReadUInt32(zip, nextLocal));
            Assert.Equal(ReadUInt16(zip, central + 6), ReadUInt16(zip, nextLocal + 4));
            Assert.Equal(ReadUInt16(zip, central + 8), ReadUInt16(zip, nextLocal + 6));
            Assert.Equal(isDirectory ? 0 : 8, ReadUInt16(zip, nextLocal + 6) & 8);
            Assert.Equal(0u, ReadUInt32(zip, nextLocal + 14));
            Assert.Equal(zip64 ? uint.MaxValue : 0u, ReadUInt32(zip, nextLocal + 18));
            Assert.Equal(zip64 ? uint.MaxValue : 0u, ReadUInt32(zip, nextLocal + 22));
            var localNameLength = ReadUInt16(zip, nextLocal + 26);
            var localExtraLength = ReadUInt16(zip, nextLocal + 28);
            Assert.Equal(zip64 ? 20 : 0, localExtraLength);
            if (zip64)
            {
                var localExtra = nextLocal + 30 + localNameLength;
                Assert.Equal(1, ReadUInt16(zip, localExtra));
                Assert.Equal(16, ReadUInt16(zip, localExtra + 2));
                Assert.Equal(0UL, ReadUInt64(zip, localExtra + 4));
                Assert.Equal(0UL, ReadUInt64(zip, localExtra + 12));
            }
            nextLocal += 30 + localNameLength + localExtraLength + checked((int)compressed);
            if (!isDirectory)
            {
                Assert.Equal(0x08074b50u, ReadUInt32(zip, nextLocal));
                Assert.Equal(ReadUInt32(zip, central + 16), ReadUInt32(zip, nextLocal + 4));
                Assert.Equal(
                    compressed,
                    zip64 ? ReadUInt64(zip, nextLocal + 8) : ReadUInt32(zip, nextLocal + 8)
                );
                Assert.Equal(
                    uncompressed,
                    zip64 ? ReadUInt64(zip, nextLocal + 16) : ReadUInt32(zip, nextLocal + 12)
                );
                nextLocal += zip64 ? 24 : 16;
            }
            central += 46 + nameLength + extraLength + commentLength;
        }
        Assert.Equal(ReadUInt32(zip, end + 16), (uint)nextLocal);
        Assert.Equal(end, central);
    }

    private static ushort ReadUInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));

    private static uint ReadUInt32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));

    private static ulong ReadUInt64(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset));
}
