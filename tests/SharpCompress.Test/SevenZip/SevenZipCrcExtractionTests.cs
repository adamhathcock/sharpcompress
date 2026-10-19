using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipCrcExtractionTests : ArchiveTests
{
    [Theory]
    [InlineData("7Zip.LZMA.7z")]
    [InlineData("7Zip.LZMA2.7z")]
    [InlineData("7Zip.solid.7z")]
    public void SevenZip_Archive_WriteToFile_Throws_On_Crc_Mismatch(string archiveName)
    {
        using var archive = SevenZipArchive.OpenArchive(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName)
        );
        var entry = CorruptFirstFileCrc(archive);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "7zip-crc-mismatch.txt");

        var exception = Assert.Throws<InvalidFormatException>(() => entry.WriteToFile(destination));

        Assert.Contains(entry.Key!, exception.Message);
    }

    [Theory]
    [InlineData("7Zip.LZMA.7z")]
    [InlineData("7Zip.LZMA2.7z")]
    [InlineData("7Zip.solid.7z")]
    public void SevenZip_Archive_WriteToFile_Skips_Crc_Mismatch_When_Disabled(string archiveName)
    {
        using var archive = SevenZipArchive.OpenArchive(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName)
        );
        var entry = CorruptFirstFileCrc(archive);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "7zip-crc-disabled.txt");

        entry.WriteToFile(destination, new ExtractionOptions { CheckCrc = false });

        Assert.True(new FileInfo(destination).Length > 0);
    }

    [Theory]
    [InlineData("7Zip.LZMA.7z")]
    [InlineData("7Zip.LZMA2.7z")]
    [InlineData("7Zip.solid.7z")]
    public async Task SevenZip_Archive_WriteToFileAsync_Throws_On_Crc_Mismatch(string archiveName)
    {
        await using var archive = await SevenZipArchive.OpenAsyncArchive(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName),
            cancellationToken: CancellationToken.None
        );
        var entries = await archive.EntriesAsync.ToListAsync();
        var entry = CorruptFirstFileCrc(entries);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "7zip-crc-mismatch-async.txt");

        var exception = await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await entry.WriteToFileAsync(destination, cancellationToken: CancellationToken.None)
        );

        Assert.Contains(entry.Key!, exception.Message);
    }

    [Theory]
    [InlineData("7Zip.LZMA.7z")]
    [InlineData("7Zip.LZMA2.7z")]
    [InlineData("7Zip.solid.7z")]
    public async Task SevenZip_Archive_WriteToFileAsync_Skips_Crc_Mismatch_When_Disabled(
        string archiveName
    )
    {
        await using var archive = await SevenZipArchive.OpenAsyncArchive(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName),
            cancellationToken: CancellationToken.None
        );
        var entries = await archive.EntriesAsync.ToListAsync();
        var entry = CorruptFirstFileCrc(entries);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "7zip-crc-disabled-async.txt");

        await entry.WriteToFileAsync(
            destination,
            new ExtractionOptions { CheckCrc = false },
            cancellationToken: CancellationToken.None
        );

        Assert.True(new FileInfo(destination).Length > 0);
    }

    private static IArchiveEntry CorruptFirstFileCrc(IArchive archive)
    {
        var entry = archive.Entries.First(e => !e.IsDirectory);
        CorruptCrc(entry);
        return entry;
    }

    private static IArchiveEntry CorruptFirstFileCrc(
        System.Collections.Generic.IEnumerable<IArchiveEntry> entries
    )
    {
        var entry = entries.First(e => !e.IsDirectory);
        CorruptCrc(entry);
        return entry;
    }

    private static void CorruptCrc(IArchiveEntry entry)
    {
        var sevenZipEntry = Assert.IsAssignableFrom<SevenZipEntry>(entry);
        var crc = sevenZipEntry.FilePart.Header.Crc.NotNull();
        sevenZipEntry.FilePart.Header.Crc = crc ^ 0xFFFFFFFF;
    }
}
