using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarCrcExtractionTests : ArchiveTests
{
    [Theory]
    [InlineData("Rar.rar")]
    [InlineData("Rar5.rar")]
    public void Rar_Archive_WriteToFile_Throws_On_Crc_Mismatch(string archiveName)
    {
        using var archive = RarArchive.OpenArchive(Path.Combine(TEST_ARCHIVES_PATH, archiveName));
        var entry = CorruptFirstFileCrc(archive);
        var destination = Path.Combine(SCRATCH_FILES_PATH, $"{archiveName}-crc-mismatch.txt");

        Assert.Throws<InvalidFormatException>(() => entry.WriteToFile(destination));
    }

    [Theory]
    [InlineData("Rar.rar", false)]
    [InlineData("Rar5.rar", false)]
    [InlineData("Rar5.crc_blake2.rar", false)]
#if !LEGACY_DOTNET
    [InlineData("Rar.rar", true)]
    [InlineData("Rar5.rar", true)]
    [InlineData("Rar5.crc_blake2.rar", true)]
#endif
    public async Task Rar_Archive_ReadAsync_Throws_On_Crc_Mismatch(
        string archiveName,
        bool useMemory
    )
    {
        await using var archive = await RarArchive.OpenAsyncArchive(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName),
            cancellationToken: CancellationToken.None
        );
        var entry = await archive.EntriesAsync.Where(e => !e.IsDirectory).FirstAsync();
        CorruptCrc((RarArchiveEntry)entry);
        using var input = await entry.OpenEntryStreamAsync(CancellationToken.None);
        var buffer = new byte[4096];

        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
        {
#if !LEGACY_DOTNET
            if (useMemory)
            {
                while (await input.ReadAsync(buffer.AsMemory(), CancellationToken.None) != 0) { }
                return;
            }
#endif
            while (await input.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None) != 0) { }
        });
    }

    [Theory]
    [InlineData("Rar.rar")]
    [InlineData("Rar5.rar")]
    [InlineData("Rar5.crc_blake2.rar")]
    public async Task Rar_Archive_WriteToAsync_Throws_On_Crc_Mismatch(string archiveName)
    {
        await using var archive = await RarArchive.OpenAsyncArchive(
            Path.Combine(TEST_ARCHIVES_PATH, archiveName),
            cancellationToken: CancellationToken.None
        );
        var entry = await archive.EntriesAsync.Where(e => !e.IsDirectory).FirstAsync();
        CorruptCrc((RarArchiveEntry)entry);

        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await entry.WriteToAsync(Stream.Null, cancellationToken: CancellationToken.None)
        );
    }

    private static RarArchiveEntry CorruptFirstFileCrc(IArchive archive)
    {
        var entry = archive.Entries.OfType<RarArchiveEntry>().First(e => !e.IsDirectory);
        CorruptCrc(entry);
        return entry;
    }

    private static void CorruptCrc(RarArchiveEntry entry)
    {
        var crc = entry.FileHeader.FileCrc.NotNull();
        crc[0] ^= 0xFF;
    }
}
