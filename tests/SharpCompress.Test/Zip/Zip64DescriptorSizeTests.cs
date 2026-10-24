using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class Zip64DescriptorSizeTests
{
    private const uint DataDescriptorSignature = 0x08074b50;

    public static IEnumerable<object[]> Sizes()
    {
        foreach (var useAsync in new[] { false, true })
        {
            foreach (var signature in new[] { false, true })
            {
                foreach (
                    var size in new[]
                    {
                        0UL,
                        1UL,
                        0xFFFFFFFEUL,
                        0xFFFFFFFFUL,
                        0x100000000UL,
                        0x10000007BUL,
                        0x123456789UL,
                        // The low 32 bits of a ZIP64 descriptor's uncompressed size are read
                        // as a candidate record signature. These values collide with the local
                        // file header and the central directory header signatures.
                        0x04034B50UL,
                        0x02014B50UL,
                        0x104034B50UL,
                        0x102014B50UL,
                    }
                )
                {
                    yield return new object[] { useAsync, signature, size };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public async Task Descriptor_Preserves_Uncompressed_Size_And_Next_Header(
        bool useAsync,
        bool signature,
        ulong size
    )
    {
        var zip = WriteZip64Archive();
        var descriptor = FindDescriptor(zip);
        // Parser-only metadata probe: advertise a large size without allocating a large payload.
        // The Archive API is intentionally not used: this synthetic descriptor differs from the directory.
        BinaryPrimitives.WriteUInt64LittleEndian(zip.AsSpan(descriptor + 16), size);
        if (!signature)
        {
            zip = zip.Take(descriptor).Concat(zip.Skip(descriptor + 4)).ToArray();
        }

        var (firstSize, secondKey) = await ReadFirstTwoEntries(zip, useAsync);
        Assert.Equal((long)size, firstSize);
        Assert.Equal("second", secondKey);
    }

    [Theory]
    [InlineData(false, false, 0x04034B50)]
    [InlineData(false, true, 0x04034B50)]
    [InlineData(true, false, 0x04034B50)]
    [InlineData(true, true, 0x04034B50)]
    [InlineData(false, false, 0x02014B50)]
    [InlineData(false, true, 0x02014B50)]
    [InlineData(true, false, 0x02014B50)]
    [InlineData(true, true, 0x02014B50)]
    public async Task Signature_Size_RoundTrips(bool useAsync, bool signature, int size)
    {
        // Write real payloads in small chunks: unlike the metadata probes, the local
        // headers, descriptors and central directory all describe the same entry.
        using var output = new MemoryStream();
        using (
            var writer = new ZipWriter(
                new ForwardOnlyStream(output),
                new ZipWriterOptions(CompressionType.Deflate) { UseZip64 = true }
            )
        )
        {
            using (var first = writer.WriteToStream("first", new ZipWriterEntryOptions()))
            {
                var buffer = new byte[8192];
                for (var remaining = size; remaining > 0; )
                {
                    var count = Math.Min(remaining, buffer.Length);
                    first.Write(buffer, 0, count);
                    remaining -= count;
                }
            }
            using var second = new MemoryStream([43]);
            writer.Write("second", second, DateTime.Now);
        }

        var zip = output.ToArray();
        if (!signature)
        {
            var descriptor = FindDescriptor(zip);
            zip = zip.Take(descriptor).Concat(zip.Skip(descriptor + 4)).ToArray();
        }
        var (firstSize, secondKey) = await ReadFirstTwoEntries(zip, useAsync);
        Assert.Equal(size, firstSize);
        Assert.Equal("second", secondKey);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Classic_Descriptor_With_High_Version_Is_Read(bool useAsync, bool signature)
    {
        var zip = WriteZip64Archive(useZip64: false);
        var descriptor = FindDescriptor(zip);
        // Higher versions are also used by non-ZIP64 compression methods. Version alone
        // must not select 64-bit descriptor sizes.
        BinaryPrimitives.WriteUInt16LittleEndian(zip.AsSpan(4), 63);
        if (!signature)
        {
            zip = zip.Take(descriptor).Concat(zip.Skip(descriptor + 4)).ToArray();
        }

        var (firstSize, secondKey) = await ReadFirstTwoEntries(zip, useAsync);
        Assert.Equal(1L, firstSize);
        Assert.Equal("second", secondKey);
    }

    public static IEnumerable<object[]> CompressedSizes()
    {
        foreach (var useAsync in new[] { false, true })
        {
            foreach (var signature in new[] { false, true })
            {
                foreach (var zip64Header in new[] { false, true })
                {
                    foreach (var size in new[] { 0xFFFFFFFFUL, 0x1FFFFFFFFUL })
                    {
                        yield return new object[] { useAsync, signature, zip64Header, size };
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(CompressedSizes))]
    public async Task Descriptor_Preserves_Compressed_Size_Words(
        bool useAsync,
        bool signature,
        bool zip64Header,
        ulong size
    )
    {
        var zip = WriteZip64Archive();
        var descriptor = FindDescriptor(zip);
        // Only the descriptor is changed: decompression still consumes the tiny payload.
        BinaryPrimitives.WriteUInt64LittleEndian(zip.AsSpan(descriptor + 8), size);
        if (!zip64Header)
        {
            // Preserve support for older writers that emitted 64-bit descriptors without
            // declaring ZIP64 in the local header.
            BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(18), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(22), 0);
            var extra = 30 + BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(26));
            BinaryPrimitives.WriteUInt16LittleEndian(zip.AsSpan(extra), 0xFFFF);
        }
        if (!signature)
        {
            zip = zip.Take(descriptor).Concat(zip.Skip(descriptor + 4)).ToArray();
        }

        await ReadFirstTwoEntries(zip, useAsync, expectedCompressedSize: (long)size);
    }

    /// <summary>
    /// The descriptor width is also resolved when the archive is read from a seekable
    /// stream, where the entry data position is calculated from the descriptor sizes.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Descriptor_Size_Collision_Is_Resolved_On_Seekable_Stream(bool useAsync)
    {
        var zip = WriteZip64Archive();
        var descriptor = FindDescriptor(zip);
        BinaryPrimitives.WriteUInt64LittleEndian(zip.AsSpan(descriptor + 16), 0x04034B50UL);

        var (firstSize, secondKey) = await ReadFirstTwoEntries(zip, useAsync, seekable: true);
        Assert.Equal(0x04034B50L, firstSize);
        Assert.Equal("second", secondKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Zip64_Extra_Field_Declares_Width_With_Zero_Local_Sizes(bool useAsync)
    {
        var zip = WriteZip64Archive();
        var descriptor = FindDescriptor(zip);
        BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(18), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(22), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(zip.AsSpan(descriptor + 16), 0x02014B50UL);

        var (firstSize, secondKey) = await ReadFirstTwoEntries(zip, useAsync);
        Assert.Equal(0x02014B50L, firstSize);
        Assert.Equal("second", secondKey);
    }

    /// <summary>
    /// A record with an unknown signature after a ZIP64 descriptor ends the enumeration,
    /// but the sizes of the entry that was read are kept.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_Record_After_Zip64_Descriptor_Keeps_Sizes(bool useAsync)
    {
        var zip = WriteZip64Archive();
        var descriptor = FindDescriptor(zip);
        const long size = 0x04034B50;
        BinaryPrimitives.WriteUInt64LittleEndian(zip.AsSpan(descriptor + 16), (ulong)size);
        // Corrupt the signature of the local header that follows the first descriptor.
        BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(descriptor + 24), 0x04034B51);

        using var input = new ForwardOnlyStream(new MemoryStream(zip));
        if (useAsync)
        {
            await using var reader = await ReaderFactory.OpenAsyncReader(
                new AsyncOnlyStream(input),
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            var first = reader.Entry;
            Assert.False(await reader.MoveToNextEntryAsync(CancellationToken.None));
            Assert.Equal(size, first.Size);
        }
        else
        {
            using var reader = ReaderFactory.OpenReader(input, ReaderOptions.ForExternalStream);
            Assert.True(reader.MoveToNextEntry());
            var first = reader.Entry;
            Assert.False(reader.MoveToNextEntry());
            Assert.Equal(size, first.Size);
        }
    }

    /// <summary>
    /// Writes two entries to a forward-only stream, which makes the writer use data
    /// descriptors and reserve the ZIP64 fields in the local headers.
    /// </summary>
    private static byte[] WriteZip64Archive(bool useZip64 = true)
    {
        using var output = new MemoryStream();
        using (
            var writer = new ZipWriter(
                new ForwardOnlyStream(output),
                new ZipWriterOptions(CompressionType.Deflate) { UseZip64 = useZip64 }
            )
        )
        {
            using var first = new MemoryStream([42]);
            writer.Write("first", first, DateTime.Now);
            using var second = new MemoryStream([43]);
            writer.Write("second", second, DateTime.Now);
        }
        return output.ToArray();
    }

    private static int FindDescriptor(byte[] zip) =>
        Enumerable
            .Range(0, zip.Length - 24)
            .First(i =>
                BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(i)) == DataDescriptorSignature
            );

    private static async Task<(long FirstSize, string? SecondKey)> ReadFirstTwoEntries(
        byte[] zip,
        bool useAsync,
        bool seekable = false,
        long? expectedCompressedSize = null
    )
    {
        using var input = seekable
            ? (Stream)new MemoryStream(zip)
            : new ForwardOnlyStream(new MemoryStream(zip));
        if (useAsync)
        {
            await using var reader = await ReaderFactory.OpenAsyncReader(
                seekable ? input : new AsyncOnlyStream(input),
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            var first = reader.Entry;
#if LEGACY_DOTNET
            using (var stream = await reader.OpenEntryStreamAsync(CancellationToken.None))
#else
            await using (var stream = await reader.OpenEntryStreamAsync(CancellationToken.None))
#endif
            {
                await stream.CopyToAsync(Stream.Null, 81920, CancellationToken.None);
            }
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            if (expectedCompressedSize.HasValue)
            {
                Assert.Equal(expectedCompressedSize.Value, first.CompressedSize);
            }
            Assert.Equal("second", reader.Entry.Key);
            using var second = new MemoryStream();
#if LEGACY_DOTNET
            using (var stream = await reader.OpenEntryStreamAsync(CancellationToken.None))
#else
            await using (var stream = await reader.OpenEntryStreamAsync(CancellationToken.None))
#endif
            {
                await stream.CopyToAsync(second, 81920, CancellationToken.None);
            }
            Assert.Equal(new byte[] { 43 }, second.ToArray());
            var secondKey = reader.Entry.Key;
            Assert.False(await reader.MoveToNextEntryAsync(CancellationToken.None));
            return (first.Size, secondKey);
        }
        else
        {
            using var reader = ReaderFactory.OpenReader(input, ReaderOptions.ForExternalStream);
            Assert.True(reader.MoveToNextEntry());
            var first = reader.Entry;
            using (var stream = reader.OpenEntryStream())
            {
                stream.CopyTo(Stream.Null);
            }
            Assert.True(reader.MoveToNextEntry());
            if (expectedCompressedSize.HasValue)
            {
                Assert.Equal(expectedCompressedSize.Value, first.CompressedSize);
            }
            Assert.Equal("second", reader.Entry.Key);
            using var second = new MemoryStream();
            using (var stream = reader.OpenEntryStream())
            {
                stream.CopyTo(second);
            }
            Assert.Equal(new byte[] { 43 }, second.ToArray());
            var secondKey = reader.Entry.Key;
            Assert.False(reader.MoveToNextEntry());
            return (first.Size, secondKey);
        }
    }
}
