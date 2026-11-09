using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Crypto;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.SevenZip;

[CollectionDefinition(nameof(SevenZipHeaderAllocationTests), DisableParallelization = true)]
public class SevenZipHeaderAllocationCollection { }

[Collection(nameof(SevenZipHeaderAllocationTests))]
public class SevenZipHeaderAllocationTests
{
    public static IEnumerable<object[]> MalformedHeaders()
    {
        var count = Number(100_000);
        yield return ["folders", Join([1, 4, 7, 11], count, [0])];
        yield return ["files", Join([1, 5], count, [0])];
        yield return ["coders", Join([1, 4, 7, 11, 1, 0], count)];
        yield return ["properties", Join([1, 4, 7, 11, 1, 0, 1, 0x21, 0x21], count)];
        yield return ["pack streams", Join([1, 4, 6, 0], count, [9, 0])];
        yield return ["input streams", Join([1, 4, 7, 11, 1, 0, 1, 0x11, 0], count, [1])];
        yield return ["output streams", Join([1, 4, 7, 11, 1, 0, 1, 0x11, 0, 1], count)];
        yield return ["zero coders", new byte[] { 1, 4, 7, 11, 1, 0, 0 }];
        yield return ["zero input streams", new byte[] { 1, 4, 7, 11, 1, 0, 1, 0x11, 0, 0, 1 }];
        yield return ["zero output streams", new byte[] { 1, 4, 7, 11, 1, 0, 1, 0x11, 0, 1, 0 }];
        yield return ["truncated empty bitmap", Join([1, 5], count, [14, 1, 0x80, 0])];
        // One valid Copy folder followed by forged per-folder substream counts.
        byte[] folder = [1, 4, 7, 11, 1, 0, 1, 1, 0, 12, 0, 0, 8, 13];
        yield return ["substream CRCs", Join(folder, count, [10, 1])];
        yield return ["substream sizes", Join(folder, count, [9, 0])];
        yield return ["implicit substream CRCs", Join(folder, count, [0])];
        yield return ["substream size exceeds folder", Join(folder, [2, 9, 1, 0])];
        yield return ["invalid bind index", new byte[] { 1, 4, 7, 11, 1, 0, 2, 1, 0, 1, 0, 2, 0 }];
        yield return
        [
            "invalid pack index",
            new byte[] { 1, 4, 7, 11, 1, 0, 1, 0x11, 0, 2, 1, 0, 2 },
        ];
        yield return
        [
            "duplicate pack index",
            new byte[] { 1, 4, 7, 11, 1, 0, 1, 0x11, 0, 2, 1, 0, 0 },
        ];
        yield return
        [
            "oversized decoded metadata",
            MetadataHeader(true, (ulong)int.MaxValue + 1, 0, [1, 0]),
        ];
        yield return
        [
            "overflowing inputs",
            Join(
                [1, 4, 7, 11, 1, 0, 2, 0x11, 0],
                Number(int.MaxValue),
                [1, 0x11, 0],
                Number(int.MaxValue),
                [1]
            ),
        ];
    }

    [Theory]
    [MemberData(nameof(MalformedHeaders))]
    public void SevenZipArchive_InvalidHeaderCounts_RejectBeforeAllocation(
        string name,
        byte[] header
    )
    {
        using var stream = CreateArchive(header);
        using var archive = ArchiveFactory.OpenArchive(stream);
#if !LEGACY_DOTNET
        var before = GC.GetAllocatedBytesForCurrentThread();
#endif
        var exception = Record.Exception(() => archive.Entries.ToList());
        Assert.True(
            exception is InvalidFormatException or IncompleteArchiveException,
            $"{name}: {exception}"
        );
#if !LEGACY_DOTNET
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 512 * 1024, $"{name}: allocated {allocated} bytes.");
#endif
        Assert.True(stream.CanRead);
    }

    [Theory]
    [MemberData(nameof(MalformedHeaders))]
    public async Task SevenZipArchive_InvalidHeaderCounts_AsyncRejectBeforeAllocation(
        string name,
        byte[] header
    )
    {
        using var stream = CreateArchive(header);
        using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            asyncStream,
            cancellationToken: CancellationToken.None
        );
#if !LEGACY_DOTNET
        var before = GC.GetTotalAllocatedBytes(precise: true);
#endif
        var exception = await Record.ExceptionAsync(async () =>
        {
            await foreach (
                var entry in archive.EntriesAsync.WithCancellation(CancellationToken.None)
            ) { }
        });
        Assert.True(
            exception is InvalidFormatException or IncompleteArchiveException,
            $"{name}: {exception}"
        );
#if !LEGACY_DOTNET
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        Assert.True(allocated < 512 * 1024, $"{name}: allocated {allocated} bytes.");
#endif
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SevenZipArchive_TruncatedDecodedMetadata_DoesNotReserveUnpackSize(
        bool encodedHeader
    )
    {
        // Copy produces only the bytes present, despite advertising 16 MiB.
        var header = MetadataHeader(encodedHeader, 16 * 1024 * 1024, 1, [1, 0]);
        using var stream = CreateArchive(header, [0]);
        using var archive = ArchiveFactory.OpenArchive(stream);
#if !LEGACY_DOTNET
        var before = GC.GetAllocatedBytesForCurrentThread();
#endif
        Assert.Throws<IncompleteArchiveException>(() => archive.Entries.ToList());
#if !LEGACY_DOTNET
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 512 * 1024, $"Allocated {allocated} bytes.");
#endif
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SevenZipArchive_TruncatedDecodedMetadata_AsyncRejects(bool encodedHeader)
    {
        var header = MetadataHeader(encodedHeader, 16 * 1024 * 1024, 1, [1, 0]);
        using var stream = CreateArchive(header, [0]);
        using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            asyncStream,
            cancellationToken: CancellationToken.None
        );
#if !LEGACY_DOTNET
        var before = GC.GetTotalAllocatedBytes(precise: true);
#endif
        await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
        {
            await foreach (
                var entry in archive.EntriesAsync.WithCancellation(CancellationToken.None)
            ) { }
        });
#if !LEGACY_DOTNET
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        Assert.True(allocated < 512 * 1024, $"Allocated {allocated} bytes.");
#endif
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SevenZipArchive_ReportedLzma2MetadataPayload_Rejects(bool async)
    {
        // The report puts the first packed byte at the start of the next header.
        var header = MetadataHeader(false, 16 * 1024 * 1024, 1, [0x21, 0x21, 1, 0x18]);
        using var stream = CreateArchive(header);
        if (async)
        {
            using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
            await using var archive = await ArchiveFactory.OpenAsyncArchive(
                asyncStream,
                cancellationToken: CancellationToken.None
            );
#if !LEGACY_DOTNET
            var before = GC.GetTotalAllocatedBytes(precise: true);
#endif
            await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
            {
                await foreach (
                    var entry in archive.EntriesAsync.WithCancellation(CancellationToken.None)
                ) { }
            });
#if !LEGACY_DOTNET
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
            Assert.True(allocated < 512 * 1024, $"Allocated {allocated} bytes.");
#endif
        }
        else
        {
            using var archive = ArchiveFactory.OpenArchive(stream);
#if !LEGACY_DOTNET
            var before = GC.GetAllocatedBytesForCurrentThread();
#endif
            Assert.Throws<DataErrorException>(() => archive.Entries.ToList());
#if !LEGACY_DOTNET
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated < 512 * 1024, $"Allocated {allocated} bytes.");
#endif
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SevenZipArchive_BitPackedEmptyFiles_AreValid(bool async)
    {
        // Nine nameless directories need only two bitmap bytes. A byte-per-file
        // bound on the remaining header would incorrectly reject this archive.
        using var stream = CreateArchive([1, 5, 9, 14, 2, 0xFF, 0x80, 0, 0]);
        if (async)
        {
            await using var archive = await ArchiveFactory.OpenAsyncArchive(
                stream,
                cancellationToken: CancellationToken.None
            );
            var count = 0;
            await foreach (
                var entry in archive.EntriesAsync.WithCancellation(CancellationToken.None)
            )
            {
                Assert.True(entry.IsDirectory);
                count++;
            }
            Assert.Equal(9, count);
        }
        else
        {
            using var archive = ArchiveFactory.OpenArchive(stream);
            Assert.Equal(9, archive.Entries.Count());
            Assert.All(archive.Entries, entry => Assert.True(entry.IsDirectory));
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SevenZipArchive_ExternalFolderCount_UsesExternalBuffer(bool async)
    {
        // Decode one byte of additional metadata, then reference it as the
        // folder-definition buffer. The inline header has ample padding.
        var header = Join(MetadataHeader(false, 1, 1, [1, 0]), [4, 7, 11, 2, 1, 0], new byte[128]);
        using var stream = CreateArchive(header, [0]);
        if (async)
        {
            using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
            await using var archive = await ArchiveFactory.OpenAsyncArchive(
                asyncStream,
                cancellationToken: CancellationToken.None
            );
            await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
            {
                await foreach (
                    var entry in archive.EntriesAsync.WithCancellation(CancellationToken.None)
                ) { }
            });
        }
        else
        {
            using var archive = ArchiveFactory.OpenArchive(stream);
            Assert.Throws<IncompleteArchiveException>(() => archive.Entries.ToList());
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SevenZipArchive_ExternalFolderDefinition_IsValid(bool async)
    {
        // Additional metadata contains a Copy folder. Its unpack size remains
        // inline; the file's packed byte follows the external folder definition.
        var header = Join(
            MetadataHeader(false, 3, 3, [1, 0]),
            [4, 6, 3, 1, 9, 1, 0, 7, 11, 1, 1, 0, 12, 1, 0, 8, 0, 0, 5, 1, 0, 0]
        );
        using var stream = CreateArchive(header, [1, 1, 0, 42]);
        if (async)
        {
            using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
            await using var archive = await ArchiveFactory.OpenAsyncArchive(
                asyncStream,
                cancellationToken: CancellationToken.None
            );
            var count = 0;
            await foreach (
                var entry in archive.EntriesAsync.WithCancellation(CancellationToken.None)
            )
            {
                using var entryStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
                var data = new byte[1];
                Assert.Equal(1, await entryStream.ReadAsync(data, 0, 1, CancellationToken.None));
                Assert.Equal(42, data[0]);
                count++;
            }
            Assert.Equal(1, count);
        }
        else
        {
            using var archive = ArchiveFactory.OpenArchive(stream);
            using var entryStream = archive.Entries.Single().OpenEntryStream();
            Assert.Equal(42, entryStream.ReadByte());
        }
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SevenZipArchive_DecodedMetadataSpanningBuffers_IsValid(bool async)
    {
        // An unknown archive property forces decoding across several buffers.
        var decoded = Join([1, 2, 26], Number(20_000), new byte[20_000], [0, 0]);
        var header = MetadataHeader(true, (ulong)decoded.Length, (ulong)decoded.Length, [1, 0]);
        using var stream = CreateArchive(header, decoded);
        if (async)
        {
            using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
            await using var archive = await ArchiveFactory.OpenAsyncArchive(
                asyncStream,
                cancellationToken: CancellationToken.None
            );
            await foreach (
                var entry in archive.EntriesAsync.WithCancellation(CancellationToken.None)
            )
            {
                Assert.Fail("Expected an empty archive.");
            }
        }
        else
        {
            using var archive = ArchiveFactory.OpenArchive(stream);
            Assert.Empty(archive.Entries);
        }
        Assert.True(stream.CanRead);
    }

    private static byte[] MetadataHeader(
        bool encoded,
        ulong unpackSize,
        ulong packSize,
        byte[] coder
    ) =>
        Join(
            encoded ? [23] : [1, 3],
            [6, 0, 1, 9],
            Number(packSize),
            [0, 7, 11, 1, 0, 1],
            coder,
            [12],
            Number(unpackSize),
            [0, 0]
        );

    private static byte[] Number(ulong value)
    {
        using var stream = new MemoryStream();
        stream.WriteEncodedUInt64(value);
        return stream.ToArray();
    }

    private static byte[] Join(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

    private static MemoryStream CreateArchive(byte[] header, byte[]? packedData = null)
    {
        packedData ??= [];
        var crc = Crc32Stream.Compute(
            Crc32Stream.DEFAULT_POLYNOMIAL,
            Crc32Stream.DEFAULT_SEED,
            header
        );
        var stream = new MemoryStream();
        SevenZipSignatureHeaderWriter.WriteFinal(
            stream,
            (ulong)packedData.Length,
            (ulong)header.Length,
            crc
        );
        stream.Write(packedData, 0, packedData.Length);
        stream.Write(header, 0, header.Length);
        stream.Position = 0;
        return stream;
    }
}
