using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA.Utilites;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarArchiveAsyncTests : ArchiveTests
{
    [Fact]
    public async Task Rar_EncryptedFileAndHeader_Archive_Async() =>
        await ReadRarPasswordAsync("Rar.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public async Task Rar_EncryptedFileAndHeader_NoPasswordExceptionTest_Async() =>
        await Assert.ThrowsAsync(
            typeof(CryptographicException),
            async () => await ReadRarPasswordAsync("Rar.encrypted_filesAndHeader.rar", null)
        );

    [Fact]
    public async Task Rar5_EncryptedFileAndHeader_Archive_Async() =>
        await ReadRarPasswordAsync("Rar5.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public async Task Rar5_EncryptedFileAndHeader_Archive_Err_Async() =>
        await Assert.ThrowsAsync(
            typeof(CryptographicException),
            async () => await ReadRarPasswordAsync("Rar5.encrypted_filesAndHeader.rar", "failed")
        );

    [Fact]
    public async Task Rar5_EncryptedFileAndHeader_NoPasswordExceptionTest_Async() =>
        await Assert.ThrowsAsync(
            typeof(CryptographicException),
            async () => await ReadRarPasswordAsync("Rar5.encrypted_filesAndHeader.rar", null)
        );

    [Fact]
    public async Task Rar_EncryptedFileOnly_Archive_Async() =>
        await ReadRarPasswordAsync("Rar.encrypted_filesOnly.rar", "test");

    [Fact]
    public async Task Rar_EncryptedFileOnly_Archive_Err_Async() =>
        await Assert.ThrowsAsync(
            typeof(CryptographicException),
            async () => await ReadRarPasswordAsync("Rar5.encrypted_filesOnly.rar", "failed")
        );

    [Fact]
    public async Task Rar5_EncryptedFileOnly_Archive_Async() =>
        await ReadRarPasswordAsync("Rar5.encrypted_filesOnly.rar", "test");

    [Fact]
    public async Task Rar_Encrypted_Archive_Async() =>
        await ReadRarPasswordAsync("Rar.Encrypted.rar", "test");

    [Fact]
    public async Task Rar5_Encrypted_Archive_Async() =>
        await ReadRarPasswordAsync("Rar5.encrypted_filesAndHeader.rar", "test");

    private async Task ReadRarPasswordAsync(string testArchive, string? password)
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, testArchive)))
        using (
            var archive = RarArchive.Open(
                stream,
                new ReaderOptions { Password = password, LeaveStreamOpen = true }
            )
        )
        {
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory)
                {
                    Assert.Equal(CompressionType.Rar, entry.CompressionType);
                    await entry.WriteToDirectoryAsync(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar_Multi_Archive_Encrypted_Async() =>
        await Assert.ThrowsAsync<InvalidFormatException>(async () =>
            await ArchiveFileReadPasswordAsync("Rar.EncryptedParts.part01.rar", "test")
        );

    protected async Task ArchiveFileReadPasswordAsync(string archiveName, string password)
    {
        using (
            var archive = RarArchive.Open(
                Path.Combine(TEST_ARCHIVES_PATH, archiveName),
                new ReaderOptions { Password = password, LeaveStreamOpen = true }
            )
        )
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                await entry.WriteToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar_None_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Rar.none.rar");

    [Fact]
    public async Task Rar5_None_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Rar5.none.rar");

    [Fact]
    public async Task Rar_ArchiveStreamRead_Async() => await ArchiveStreamReadAsync("Rar.rar");

    [Fact]
    public async Task Rar5_ArchiveStreamRead_Async() => await ArchiveStreamReadAsync("Rar5.rar");

    [Fact]
    public async Task Rar_test_invalid_exttime_ArchiveStreamRead_Async() =>
        await DoRar_test_invalid_exttime_ArchiveStreamReadAsync("Rar.test_invalid_exttime.rar");

    private async Task DoRar_test_invalid_exttime_ArchiveStreamReadAsync(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var archive = ArchiveFactory.Open(stream);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            await entry.WriteToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
    }

    [Fact]
    public async Task Rar_Jpg_ArchiveStreamRead_Async()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.jpeg.jpg"));
        using (var archive = RarArchive.Open(stream, new ReaderOptions { LookForHeader = true }))
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                await entry.WriteToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar_IsSolidArchiveCheck_Async() =>
        await DoRar_IsSolidArchiveCheckAsync("Rar.rar");

    [Fact]
    public async Task Rar5_IsSolidArchiveCheck_Async() =>
        await DoRar_IsSolidArchiveCheckAsync("Rar5.rar");

    private async Task DoRar_IsSolidArchiveCheckAsync(string filename)
    {
        using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename)))
        {
            using var archive = RarArchive.Open(stream);
            Assert.False(archive.IsSolid);
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                await entry.WriteToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar_IsSolidEntryStreamCheck_Async() =>
        await DoRar_IsSolidEntryStreamCheckAsync("Rar.solid.rar");

    private async Task DoRar_IsSolidEntryStreamCheckAsync(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var archive = RarArchive.Open(stream);
        Assert.True(archive.IsSolid);
        IArchiveEntry[] entries = archive.Entries.Where(a => !a.IsDirectory).ToArray();
        Assert.NotInRange(entries.Length, 0, 1);
        Assert.False(entries[0].IsSolid);
        var testEntry = entries[1];
        Assert.True(testEntry.IsSolid);

        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            using (var crcStream = new CrcCheckStream((uint)entry.Crc))
            {
                using var eStream = await entry.OpenEntryStreamAsync();
                await eStream.CopyToAsync(crcStream);
            }
            if (entry == testEntry)
            {
                break;
            }
        }
    }

    [Fact]
    public async Task Rar_Solid_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Rar.solid.rar");

    [Fact]
    public async Task Rar5_Solid_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Rar5.solid.rar");

    [Fact]
    public async Task Rar_Solid_StreamRead_Extract_All_Async() =>
        await ArchiveStreamReadExtractAllAsync("Rar.solid.rar", CompressionType.Rar);

    [Fact]
    public async Task Rar5_Solid_StreamRead_Extract_All_Async() =>
        await ArchiveStreamReadExtractAllAsync("Rar5.solid.rar", CompressionType.Rar);

    [Fact]
    public async Task Rar_Multi_ArchiveStreamRead_Async() =>
        await DoRar_Multi_ArchiveStreamReadAsync(
            [
                "Rar.multi.part01.rar",
                "Rar.multi.part02.rar",
                "Rar.multi.part03.rar",
                "Rar.multi.part04.rar",
                "Rar.multi.part05.rar",
                "Rar.multi.part06.rar",
            ],
            false
        );

    [Fact]
    public async Task Rar5_Multi_ArchiveStreamRead_Async() =>
        await DoRar_Multi_ArchiveStreamReadAsync(
            [
                "Rar5.multi.part01.rar",
                "Rar5.multi.part02.rar",
                "Rar5.multi.part03.rar",
                "Rar5.multi.part04.rar",
                "Rar5.multi.part05.rar",
                "Rar5.multi.part06.rar",
            ],
            false
        );

    private async Task DoRar_Multi_ArchiveStreamReadAsync(string[] archives, bool isSolid)
    {
        using var archive = RarArchive.Open(
            archives.Select(s => Path.Combine(TEST_ARCHIVES_PATH, s)).Select(File.OpenRead)
        );
        Assert.Equal(archive.IsSolid, isSolid);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            await entry.WriteToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
    }

    [Fact]
    public async Task Rar5_MultiSolid_ArchiveStreamRead_Async() =>
        await DoRar_Multi_ArchiveStreamReadAsync(
            [
                "Rar.multi.solid.part01.rar",
                "Rar.multi.solid.part02.rar",
                "Rar.multi.solid.part03.rar",
                "Rar.multi.solid.part04.rar",
                "Rar.multi.solid.part05.rar",
                "Rar.multi.solid.part06.rar",
            ],
            true
        );

    [Fact]
    public async Task RarNoneArchiveFileRead_Async() => await ArchiveFileReadAsync("Rar.none.rar");

    [Fact]
    public async Task Rar5NoneArchiveFileRead_Async() =>
        await ArchiveFileReadAsync("Rar5.none.rar");

    [Fact]
    public async Task Rar_ArchiveFileRead_Async() => await ArchiveFileReadAsync("Rar.rar");

    [Fact]
    public async Task Rar5_ArchiveFileRead_Async() => await ArchiveFileReadAsync("Rar5.rar");

    [Fact]
    public async Task Rar_ArchiveFileRead_HasDirectories_Async() =>
        await DoRar_ArchiveFileRead_HasDirectoriesAsync("Rar.rar");

    [Fact]
    public async Task Rar5_ArchiveFileRead_HasDirectories_Async() =>
        await DoRar_ArchiveFileRead_HasDirectoriesAsync("Rar5.rar");

    private Task DoRar_ArchiveFileRead_HasDirectoriesAsync(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var archive = RarArchive.Open(stream);
        Assert.False(archive.IsSolid);
        Assert.Contains(true, archive.Entries.Select(entry => entry.IsDirectory));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Rar_Jpg_ArchiveFileRead_Async()
    {
        using (
            var archive = RarArchive.Open(
                Path.Combine(TEST_ARCHIVES_PATH, "Rar.jpeg.jpg"),
                new ReaderOptions { LookForHeader = true }
            )
        )
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                await entry.WriteToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Rar_Solid_ArchiveFileRead_Async() =>
        await ArchiveFileReadAsync("Rar.solid.rar");

    [Fact]
    public async Task Rar5_Solid_ArchiveFileRead_Async() =>
        await ArchiveFileReadAsync("Rar5.solid.rar");

    [Fact]
    public async Task Rar2_Multi_ArchiveStreamRead_Async() =>
        await DoRar_Multi_ArchiveStreamReadAsync(
            [
                "Rar2.multi.rar",
                "Rar2.multi.r00",
                "Rar2.multi.r01",
                "Rar2.multi.r02",
                "Rar2.multi.r03",
                "Rar2.multi.r04",
                "Rar2.multi.r05",
            ],
            false
        );

    [Fact]
    public async Task Rar2_Multi_ArchiveFileRead_Async() =>
        await ArchiveFileReadAsync("Rar2.multi.rar");

    [Fact]
    public async Task Rar2_ArchiveFileRead_Async() => await ArchiveFileReadAsync("Rar2.rar");

    [Fact]
    public async Task Rar15_ArchiveFileRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveFileReadAsync("Rar15.rar");
    }

    [Fact]
    public void Rar15_ArchiveVersionTest_Async()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Rar15.rar");

        using var archive = RarArchive.Open(testArchive);
        Assert.Equal(1, archive.MinVersion);
        Assert.Equal(1, archive.MaxVersion);
    }

    [Fact]
    public void Rar2_ArchiveVersionTest_Async()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Rar2.rar");

        using var archive = RarArchive.Open(testArchive);
        Assert.Equal(2, archive.MinVersion);
        Assert.Equal(2, archive.MaxVersion);
    }

    [Fact]
    public void Rar4_ArchiveVersionTest_Async()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Rar4.multi.part01.rar");

        using var archive = RarArchive.Open(testArchive);
        Assert.Equal(3, archive.MinVersion);
        Assert.Equal(4, archive.MaxVersion);
    }

    [Fact]
    public void Rar5_ArchiveVersionTest_Async()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Rar5.solid.rar");

        using var archive = RarArchive.Open(testArchive);
        Assert.Equal(5, archive.MinVersion);
        Assert.Equal(6, archive.MaxVersion);
    }

    [Fact]
    public async Task Rar4_Multi_ArchiveFileRead_Async() =>
        await ArchiveFileReadAsync("Rar4.multi.part01.rar");

    [Fact]
    public async Task Rar4_ArchiveFileRead_Async() => await ArchiveFileReadAsync("Rar4.rar");

    [Fact]
    public void Rar_GetPartsSplit_Async() =>
        ArchiveGetParts(
            new[]
            {
                "Rar4.split.001",
                "Rar4.split.002",
                "Rar4.split.003",
                "Rar4.split.004",
                "Rar4.split.005",
                "Rar4.split.006",
            }
        );

    [Fact]
    public void Rar_GetPartsOld_Async() =>
        ArchiveGetParts(
            new[]
            {
                "Rar2.multi.rar",
                "Rar2.multi.r00",
                "Rar2.multi.r01",
                "Rar2.multi.r02",
                "Rar2.multi.r03",
                "Rar2.multi.r04",
                "Rar2.multi.r05",
            }
        );

    [Fact]
    public void Rar_GetPartsNew_Async() =>
        ArchiveGetParts(
            new[]
            {
                "Rar4.multi.part01.rar",
                "Rar4.multi.part02.rar",
                "Rar4.multi.part03.rar",
                "Rar4.multi.part04.rar",
                "Rar4.multi.part05.rar",
                "Rar4.multi.part06.rar",
                "Rar4.multi.part07.rar",
            }
        );

    [Fact]
    public async Task Rar4_Multi_ArchiveStreamRead_Async() =>
        await DoRar_Multi_ArchiveStreamReadAsync(
            [
                "Rar4.multi.part01.rar",
                "Rar4.multi.part02.rar",
                "Rar4.multi.part03.rar",
                "Rar4.multi.part04.rar",
                "Rar4.multi.part05.rar",
                "Rar4.multi.part06.rar",
                "Rar4.multi.part07.rar",
            ],
            false
        );

    [Fact]
    public async Task Rar4_Split_ArchiveStreamRead_Async() =>
        await ArchiveStreamMultiReadAsync(
            null,
            [
                "Rar4.split.001",
                "Rar4.split.002",
                "Rar4.split.003",
                "Rar4.split.004",
                "Rar4.split.005",
                "Rar4.split.006",
            ]
        );

    [Fact]
    public async Task Rar4_Multi_ArchiveFirstFileRead_Async() =>
        await ArchiveFileReadAsync("Rar4.multi.part01.rar");

    [Fact]
    public async Task Rar4_Split_ArchiveFirstFileRead_Async() =>
        await ArchiveFileReadAsync("Rar4.split.001");

    [Fact]
    public async Task Rar4_Split_ArchiveStreamFirstFileRead_Async() =>
        await ArchiveStreamMultiReadAsync(null, ["Rar4.split.001"]);

    [Fact]
    public async Task Rar4_Split_ArchiveOpen_Async() =>
        await ArchiveOpenStreamReadAsync(
            null,
            "Rar4.split.001",
            "Rar4.split.002",
            "Rar4.split.003",
            "Rar4.split.004",
            "Rar4.split.005",
            "Rar4.split.006"
        );

    [Fact]
    public async Task Rar4_Multi_ArchiveOpen_Async() =>
        await ArchiveOpenStreamReadAsync(
            null,
            "Rar4.multi.part01.rar",
            "Rar4.multi.part02.rar",
            "Rar4.multi.part03.rar",
            "Rar4.multi.part04.rar",
            "Rar4.multi.part05.rar",
            "Rar4.multi.part06.rar",
            "Rar4.multi.part07.rar"
        );

    [Fact]
    public void Rar4_Multi_ArchiveOpenEntryVolumeIndexTest_Async() =>
        ArchiveOpenEntryVolumeIndexTest(
            [
                [0, 1],
                [1, 5],
                [5, 6],
            ],
            null,
            "Rar4.multi.part01.rar",
            "Rar4.multi.part02.rar",
            "Rar4.multi.part03.rar",
            "Rar4.multi.part04.rar",
            "Rar4.multi.part05.rar",
            "Rar4.multi.part06.rar",
            "Rar4.multi.part07.rar"
        );

    [Fact]
    public async Task Rar_Multi_ArchiveFileRead_Async() =>
        await ArchiveFileReadAsync("Rar.multi.part01.rar");

    [Fact]
    public async Task Rar5_Multi_ArchiveFileRead_Async() =>
        await ArchiveFileReadAsync("Rar5.multi.part01.rar");

    [Fact]
    public void Rar_IsFirstVolume_True_Async() => DoRar_IsFirstVolume_True("Rar.multi.part01.rar");

    [Fact]
    public void Rar5_IsFirstVolume_True_Async() =>
        DoRar_IsFirstVolume_True("Rar5.multi.part01.rar");

    private void DoRar_IsFirstVolume_True(string firstFilename)
    {
        using var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, firstFilename));
        Assert.True(archive.IsMultipartVolume());
        Assert.True(archive.IsFirstVolume());
    }

    [Fact]
    public void Rar_IsFirstVolume_False_Async() =>
        DoRar_IsFirstVolume_False("Rar.multi.part03.rar");

    [Fact]
    public void Rar5_IsFirstVolume_False_Async() =>
        DoRar_IsFirstVolume_False("Rar5.multi.part03.rar");

    private void DoRar_IsFirstVolume_False(string notFirstFilename)
    {
        using var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, notFirstFilename));
        Assert.True(archive.IsMultipartVolume());
        Assert.False(archive.IsFirstVolume());
    }

    [Fact]
    public async Task Rar5_CRC_Blake2_Archive_Async() =>
        await ArchiveFileReadAsync("Rar5.crc_blake2.rar");

    [Fact]
    void Rar_Iterate_Archive_Async() =>
        ArchiveFileSkip("Rar.rar", "Failure jpg exe Empty jpg\\test.jpg exe\\test.exe тест.txt");

    [Fact]
    public void Rar2_Iterate_Archive_Async() =>
        ArchiveFileSkip("Rar2.rar", "Failure Empty тест.txt jpg\\test.jpg exe\\test.exe jpg exe");

    [Fact]
    public void Rar4_Iterate_Archive_Async() =>
        ArchiveFileSkip("Rar4.rar", "Failure Empty jpg exe тест.txt jpg\\test.jpg exe\\test.exe");

    [Fact]
    public void Rar5_Iterate_Archive_Async() =>
        ArchiveFileSkip("Rar5.rar", "Failure jpg exe Empty тест.txt jpg\\test.jpg exe\\test.exe");

    [Fact]
    public void Rar_Encrypted_Iterate_Archive_Async() =>
        ArchiveFileSkip(
            "Rar.encrypted_filesOnly.rar",
            "Failure jpg exe Empty тест.txt jpg\\test.jpg exe\\test.exe"
        );

    [Fact]
    public void Rar5_Encrypted_Iterate_Archive_Async() =>
        ArchiveFileSkip(
            "Rar5.encrypted_filesOnly.rar",
            "Failure jpg exe Empty тест.txt jpg\\test.jpg exe\\test.exe"
        );

    private async Task ArchiveStreamReadAsync(string testArchive)
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using var stream = File.OpenRead(testArchive);
        using var archive = ArchiveFactory.Open(stream);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            await entry.WriteToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
        VerifyFiles();
    }

    private async Task ArchiveStreamReadExtractAllAsync(
        string testArchive,
        CompressionType compression
    )
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using var stream = File.OpenRead(testArchive);
        using var archive = ArchiveFactory.Open(stream);
        Assert.True(archive.IsSolid);
        using (var reader = archive.ExtractAllEntries())
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    Assert.Equal(compression, reader.Entry.CompressionType);
                    await reader.WriteEntryToDirectoryAsync(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
        VerifyFiles();

        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            await entry.WriteToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
        VerifyFiles();
    }

    private async Task ArchiveFileReadAsync(string testArchive)
    {
        testArchive = Path.Combine(TEST_ARCHIVES_PATH, testArchive);
        using var archive = ArchiveFactory.Open(testArchive);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            await entry.WriteToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
        VerifyFiles();
    }

    private async Task ArchiveStreamMultiReadAsync(
        ReaderOptions? readerOptions,
        params string[] testArchives
    )
    {
        var paths = testArchives.Select(x => Path.Combine(TEST_ARCHIVES_PATH, x));
        using var archive = ArchiveFactory.Open(paths.Select(a => new FileInfo(a)), readerOptions);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            await entry.WriteToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
        VerifyFiles();
    }

    private async Task ArchiveOpenStreamReadAsync(
        ReaderOptions? readerOptions,
        params string[] testArchives
    )
    {
        var paths = testArchives.Select(x => Path.Combine(TEST_ARCHIVES_PATH, x));
        using var archive = ArchiveFactory.Open(paths.Select(f => new FileInfo(f)), readerOptions);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            await entry.WriteToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
        VerifyFiles();
    }
}
