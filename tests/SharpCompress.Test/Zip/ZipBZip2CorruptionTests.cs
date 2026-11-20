using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipBZip2CorruptionTests : TestBase
{
    [Theory]
    [InlineData(240, 0x8d, 0x5a, false)]
    [InlineData(11318, 0x35, 0xa8, false)]
    [InlineData(41884, 0x12, 0x89, false)]
    [InlineData(240, 0x8d, 0x5a, true)]
    [InlineData(11318, 0x35, 0xa8, true)]
    [InlineData(41884, 0x12, 0x89, true)]
    public void Zip_BZip2_Reader_Throws_On_Corruption(
        int offset,
        byte original,
        byte replacement,
        bool skipEntry
    )
    {
        using var stream = new ForwardOnlyStream(
            CreateCorruptArchive(offset, original, replacement)
        );
        using var reader = ReaderFactory.OpenReader(stream, ReaderOptions.ForExternalStream);

        var exception = Assert.ThrowsAny<SharpCompressException>(() =>
        {
            while (reader.MoveToNextEntry())
            {
                // Disposal must also terminate when a failed read leaves the decoder between states.
                using var entry = reader.OpenEntryStream();
                if (!skipEntry)
                {
                    entry.CopyTo(Stream.Null);
                }
            }
        });

        Assert.StartsWith("BZip2", exception.Message);
        if (!skipEntry)
        {
            Assert.IsType<InvalidFormatException>(exception);
        }
    }

    [Theory]
    [InlineData(240, 0x8d, 0x5a, false)]
    [InlineData(11318, 0x35, 0xa8, false)]
    [InlineData(41884, 0x12, 0x89, false)]
    [InlineData(240, 0x8d, 0x5a, true)]
    [InlineData(11318, 0x35, 0xa8, true)]
    [InlineData(41884, 0x12, 0x89, true)]
    public async Task Zip_BZip2_Reader_Throws_On_Corruption_Async(
        int offset,
        byte original,
        byte replacement,
        bool skipEntry
    )
    {
        using var stream = new AsyncOnlyStream(
            new ForwardOnlyStream(CreateCorruptArchive(offset, original, replacement))
        );
        await using var reader = await ReaderFactory.OpenAsyncReader(
            stream,
            ReaderOptions.ForExternalStream,
            cancellationToken: CancellationToken.None
        );

        var exception = await Assert.ThrowsAnyAsync<SharpCompressException>(async () =>
        {
            while (await reader.MoveToNextEntryAsync(CancellationToken.None))
            {
                await using var entry = await reader.OpenEntryStreamAsync(CancellationToken.None);
                if (!skipEntry)
                {
                    await entry.CopyToAsync(Stream.Null, 81920, CancellationToken.None);
                }
            }
        });

        Assert.StartsWith("BZip2", exception.Message);
        if (!skipEntry)
        {
            Assert.IsType<InvalidFormatException>(exception);
        }
    }

    [Theory]
    [InlineData(240, 0x8d, 0x5a)]
    [InlineData(11318, 0x35, 0xa8)]
    [InlineData(41884, 0x12, 0x89)]
    public void Zip_BZip2_Archive_Throws_On_Corruption(int offset, byte original, byte replacement)
    {
        using var stream = CreateCorruptArchive(offset, original, replacement);
        using var archive = ArchiveFactory.OpenArchive(stream, ReaderOptions.ForExternalStream);

        Assert.ThrowsAny<SharpCompressException>(() =>
        {
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory)
                {
                    using var entryStream = entry.OpenEntryStream();
                    entryStream.CopyTo(Stream.Null);
                }
            }
        });
    }

    [Theory]
    [InlineData(240, 0x8d, 0x5a)]
    [InlineData(11318, 0x35, 0xa8)]
    [InlineData(41884, 0x12, 0x89)]
    public async Task Zip_BZip2_Archive_Throws_On_Corruption_Async(
        int offset,
        byte original,
        byte replacement
    )
    {
        using var stream = new AsyncOnlyStream(CreateCorruptArchive(offset, original, replacement));
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            stream,
            ReaderOptions.ForExternalStream,
            cancellationToken: CancellationToken.None
        );

        await Assert.ThrowsAnyAsync<SharpCompressException>(async () =>
        {
            await foreach (var entry in archive.EntriesAsync)
            {
                if (!entry.IsDirectory)
                {
#if LEGACY_DOTNET
                    using var entryStream = await entry.OpenEntryStreamAsync(
                        CancellationToken.None
                    );
#else
                    await using var entryStream = await entry.OpenEntryStreamAsync(
                        CancellationToken.None
                    );
#endif
                    await entryStream.CopyToAsync(Stream.Null, 81920, CancellationToken.None);
                }
            }
        });
    }

    private static MemoryStream CreateCorruptArchive(int offset, byte original, byte replacement)
    {
        // Mutate the existing fixture in memory so each regression differs by exactly one byte.
        var bytes = File.ReadAllBytes(Path.Combine(TEST_ARCHIVES_PATH, "Zip.bzip2.dd.zip"));
        Assert.Equal(original, bytes[offset]);
        bytes[offset] = replacement;
        return new MemoryStream(bytes);
    }
}
