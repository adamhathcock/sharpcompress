using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.Crypto;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipHeaderValidationTests
{
    [Theory]
    [InlineData(0, 64 * 1024 * 1024, 0)]
    [InlineData(0, 256 * 1024 * 1024, 0)]
    [InlineData(0, 2, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(2, 1, 2)]
    public void SevenZipArchive_TruncatedNextHeader_ThrowsBeforeReadingHeader(
        int offset,
        int size,
        int available
    )
    {
        using var stream = CreateArchive(offset, size, available);
        using var archive = ArchiveFactory.OpenArchive(stream);

        var exception = Assert.Throws<IncompleteArchiveException>(() => archive.Entries.ToList());

        Assert.Equal("Next header extends beyond the end of stream.", exception.Message);
        Assert.Equal(32L, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(0, 64 * 1024 * 1024, 0)]
    [InlineData(0, 256 * 1024 * 1024, 0)]
    [InlineData(0, 2, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(2, 1, 2)]
    public async Task SevenZipArchive_TruncatedNextHeader_AsyncThrowsBeforeReadingHeader(
        int offset,
        int size,
        int available
    )
    {
        using var stream = CreateArchive(offset, size, available);
        using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            asyncStream,
            cancellationToken: CancellationToken.None
        );

        var exception = await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
        {
            await foreach (
                var entry in archive.EntriesAsync.WithCancellation(CancellationToken.None)
            ) { }
        });

        Assert.Equal("Next header extends beyond the end of stream.", exception.Message);
        Assert.Equal(32L, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 2, 2)]
    [InlineData(3, 2, 5)]
    public void SevenZipArchive_NextHeaderAtEnd_IsValid(int offset, int size, int available)
    {
        using var stream = CreateArchive(offset, size, available);
        using var archive = ArchiveFactory.OpenArchive(stream);

        Assert.Empty(archive.Entries);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 2, 2)]
    [InlineData(3, 2, 5)]
    public async Task SevenZipArchive_NextHeaderAtEnd_AsyncIsValid(
        int offset,
        int size,
        int available
    )
    {
        using var stream = CreateArchive(offset, size, available);
        using var asyncStream = new AsyncOnlyStream(stream, disposeStream: false);
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            asyncStream,
            cancellationToken: CancellationToken.None
        );

        var count = 0;
        await foreach (var entry in archive.EntriesAsync.WithCancellation(CancellationToken.None))
        {
            count++;
        }

        Assert.Equal(0, count);
    }

    private static MemoryStream CreateArchive(int offset, int size, int available)
    {
        // A minimal uncompressed next header: kHeader followed by kEnd.
        byte[] header = [1, 0];
        var crc = Crc32Stream.Compute(
            Crc32Stream.DEFAULT_POLYNOMIAL,
            Crc32Stream.DEFAULT_SEED,
            header
        );
        var stream = new MemoryStream();
        SevenZipSignatureHeaderWriter.WriteFinal(stream, (ulong)offset, (ulong)size, crc);
        stream.SetLength(32 + available);
        if (available >= offset + header.Length)
        {
            stream.Position = 32 + offset;
            stream.Write(header, 0, header.Length);
        }

        stream.Position = 0;
        return stream;
    }
}
