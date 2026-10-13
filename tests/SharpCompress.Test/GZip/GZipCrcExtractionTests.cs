using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.GZip;

public class GZipCrcExtractionTests : TestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GZipReader_Async_Only_NonSeekable_Validates_Fragmented_Trailer(bool corrupt)
    {
        var bytes = corrupt
            ? ReadCorruptedGZipTrailer(true)
            : File.ReadAllBytes(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        using var fragmented = new ShortReadStream(
            new MemoryStream(bytes),
            maxReadSize: 7,
            seekable: false
        );
        using var input = new AsyncOnlyStream(fragmented, disposeStream: false);
        await using var reader = await GZipReader.OpenAsyncReader(
            input,
            new ReaderOptions { LeaveStreamOpen = true },
            CancellationToken.None
        );
        Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
        var destination = Path.Combine(SCRATCH_FILES_PATH, Guid.NewGuid().ToString());
        if (corrupt)
        {
            await Assert.ThrowsAsync<InvalidFormatException>(async () =>
                await reader.WriteEntryToFileAsync(
                    destination,
                    cancellationToken: CancellationToken.None
                )
            );
        }
        else
        {
            await reader.WriteEntryToFileAsync(
                destination,
                cancellationToken: CancellationToken.None
            );
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar")),
                File.ReadAllBytes(destination)
            );
        }
    }

    [Fact]
    public void GZipArchive_WriteToFile_Throws_On_Crc_Mismatch()
    {
        using var stream = new MemoryStream(ReadCorruptedGZipTrailer(corruptCrc: true));
        using var archive = GZipArchive.OpenArchive(stream);
        var entry = archive.Entries.Single();
        var destination = Path.Combine(SCRATCH_FILES_PATH, Guid.NewGuid().ToString());

        Assert.Throws<InvalidFormatException>(() => entry.WriteToFile(destination));
    }

    [Fact]
    public void GZipArchive_WriteToFile_Throws_On_Size_Mismatch()
    {
        using var stream = new MemoryStream(ReadCorruptedGZipTrailer(corruptCrc: false));
        using var archive = GZipArchive.OpenArchive(stream);
        var entry = archive.Entries.Single();
        var destination = Path.Combine(SCRATCH_FILES_PATH, Guid.NewGuid().ToString());

        Assert.Throws<InvalidFormatException>(() => entry.WriteToFile(destination));
    }

    [Fact]
    public void GZipReader_WriteEntryToFile_Throws_On_NonSeekable_Crc_Mismatch()
    {
        using var stream = new MemoryStream(ReadCorruptedGZipTrailer(corruptCrc: true));
        using var nonSeekableStream = new ForwardOnlyStream(stream);
        using var reader = GZipReader.OpenReader(nonSeekableStream);
        var destination = Path.Combine(SCRATCH_FILES_PATH, Guid.NewGuid().ToString());

        Assert.True(reader.MoveToNextEntry());
        Assert.Throws<InvalidFormatException>(() => reader.WriteEntryToFile(destination));
    }

    [Fact]
    public void GZipArchive_WriteToFile_Skips_Trailer_Validation_When_CheckCrc_Is_False()
    {
        using var stream = new MemoryStream(ReadCorruptedGZipTrailer(corruptCrc: true));
        using var archive = GZipArchive.OpenArchive(stream);
        var entry = archive.Entries.Single();
        var destination = Path.Combine(SCRATCH_FILES_PATH, Guid.NewGuid().ToString());

        entry.WriteToFile(destination, new ExtractionOptions { CheckCrc = false });

        Assert.Equal(
            new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar")).Length,
            new FileInfo(destination).Length
        );
    }

    [Fact]
    public async Task GZipArchive_WriteToFileAsync_Throws_On_Crc_Mismatch()
    {
        // MemoryStream has nothing to dispose asynchronously
        using var stream = new MemoryStream(ReadCorruptedGZipTrailer(corruptCrc: true));
        await using var archive = await GZipArchive.OpenAsyncArchive(stream);
        var entry = await archive.EntriesAsync.SingleAsync();
        var destination = Path.Combine(SCRATCH_FILES_PATH, Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await entry.WriteToFileAsync(destination)
        );
    }

    private static byte[] ReadCorruptedGZipTrailer(bool corruptCrc)
    {
        var bytes = File.ReadAllBytes(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"));
        var trailer = bytes.AsSpan(bytes.Length - 8);
        var offset = corruptCrc ? 0 : 4;
        var value = BinaryPrimitives.ReadUInt32LittleEndian(trailer[offset..]);
        BinaryPrimitives.WriteUInt32LittleEndian(trailer[offset..], value + 1);
        return bytes;
    }
}
