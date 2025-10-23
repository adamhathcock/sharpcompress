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


public class RarArchiveTests : ArchiveTests
{
    [Fact]
    public Task Rar_EncryptedFileAndHeader_Archive() =>
        ReadRarPasswordAsync("Rar.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public Task Rar_EncryptedFileAndHeader_NoPasswordExceptionTest() =>
        Assert.ThrowsAsync(
            typeof(CryptographicException),
            async () => await ReadRarPasswordAsync("Rar.encrypted_filesAndHeader.rar", null)
        );

    [Fact]
    public Task Rar5_EncryptedFileAndHeader_Archive() =>
       ReadRarPasswordAsync("Rar5.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public Task Rar5_EncryptedFileAndHeader_Archive_Err() =>
        Assert.ThrowsAsync(
            typeof(CryptographicException),
            async () => await ReadRarPasswordAsync("Rar5.encrypted_filesAndHeader.rar", "failed")
        );

    [Fact]
    public Task Rar5_EncryptedFileAndHeader_NoPasswordExceptionTest() =>
        Assert.ThrowsAsync(
            typeof(CryptographicException),
            async () => await ReadRarPasswordAsync("Rar5.encrypted_filesAndHeader.rar", null)
        );

    [Fact]
    public Task Rar_EncryptedFileOnly_Archive() =>
        ReadRarPasswordAsync("Rar.encrypted_filesOnly.rar", "test");

    [Fact]
    public Task Rar_EncryptedFileOnly_Archive_Err() =>
        Assert.ThrowsAsync(
            typeof(CryptographicException),
            async () => await ReadRarPasswordAsync("Rar5.encrypted_filesOnly.rar", "failed")
        );

    [Fact]
    public Task Rar5_EncryptedFileOnly_Archive() =>
        ReadRarPasswordAsync("Rar5.encrypted_filesOnly.rar", "test");

    [Fact]
    public Task Rar_Encrypted_Archive() => ReadRarPasswordAsync("Rar.Encrypted.rar", "test");

    [Fact]
    public Task Rar5_Encrypted_Archive() =>
        ReadRarPasswordAsync("Rar5.encrypted_filesAndHeader.rar", "test");

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
                    await entry.WriteEntryToDirectoryAsync(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
        VerifyFiles();
    }

    [Fact]
    public Task Rar_Multi_Archive_Encrypted() =>
        Assert.ThrowsAsync<InvalidFormatException>(async () =>
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
                await entry.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public Task Rar_None_ArchiveStreamRead() => ArchiveStreamReadAsync("Rar.none.rar");

    [Fact]
    public Task Rar5_None_ArchiveStreamRead() => ArchiveStreamReadAsync("Rar5.none.rar");

    [Fact]
    public Task Rar_ArchiveStreamRead() => ArchiveStreamReadAsync("Rar.rar");

    [Fact]
    public Task Rar5_ArchiveStreamRead() => ArchiveStreamReadAsync("Rar5.rar");

    [Fact]
    public Task Rar_test_invalid_exttime_ArchiveStreamRead() =>
        DoRar_test_invalid_exttime_ArchiveStreamRead("Rar.test_invalid_exttime.rar");

    private async Task DoRar_test_invalid_exttime_ArchiveStreamRead(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var archive = ArchiveFactory.Open(stream);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            await entry.WriteEntryToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
    }

    [Fact]
    public async Task Rar_Jpg_ArchiveStreamRead()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.jpeg.jpg"));
        using (var archive = RarArchive.Open(stream, new ReaderOptions { LookForHeader = true }))
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                await entry.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public Task Rar_IsSolidArchiveCheck() => DoRar_IsSolidArchiveCheck("Rar.rar");

    [Fact]
    public Task Rar5_IsSolidArchiveCheck() => DoRar_IsSolidArchiveCheck("Rar5.rar");

    private async Task DoRar_IsSolidArchiveCheck(string filename)
    {
        using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename)))
        {
            using var archive = RarArchive.Open(stream);
            Assert.False(archive.IsSolid);
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                await entry.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Rar_IsSolidEntryStreamCheck() => DoRar_IsSolidEntryStreamCheck("Rar.solid.rar");

    //Extract the 2nd file in a solid archive to check that the first file is skipped properly
    private void DoRar_IsSolidEntryStreamCheck(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var archive = RarArchive.Open(stream);
        Assert.True(archive.IsSolid);
        IArchiveEntry[] entries = archive.Entries.Where(a => !a.IsDirectory).ToArray();
        Assert.NotInRange(entries.Length, 0, 1);
        Assert.False(entries[0].IsSolid); //first item in a solid archive is not marked solid and is seekable
        var testEntry = entries[1];
        Assert.True(testEntry.IsSolid); //the target. The non seekable entry

        //process all entries in solid archive until the one we want to test
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            using (var crcStream = new CrcCheckStream((uint)entry.Crc)) //use the 7zip CRC stream for convenience (required a bug fix)
            {
                using var eStream = entry.OpenEntryStream(); //bug fix in RarStream to report the correct Position
                eStream.CopyTo(crcStream);
            } //throws if not valid
            if (entry == testEntry)
            {
                break;
            }
        }
    }

    [Fact]
    public Task Rar_Solid_ArchiveStreamRead() => ArchiveStreamReadAsync("Rar.solid.rar");

    [Fact]
    public Task Rar5_Solid_ArchiveStreamRead() => ArchiveStreamReadAsync("Rar5.solid.rar");

    [Fact]
    public Task Rar_Solid_StreamRead_Extract_All() =>
        ArchiveStreamReadExtractAllAsync("Rar.solid.rar", CompressionType.Rar);

    [Fact]
    public Task Rar5_Solid_StreamRead_Extract_All() =>
        ArchiveStreamReadExtractAllAsync("Rar5.solid.rar", CompressionType.Rar);

    [Fact]
    public Task Rar_Multi_ArchiveStreamRead() =>
        DoRar_Multi_ArchiveStreamReadAsync(
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
    public Task Rar5_Multi_ArchiveStreamRead() =>
        DoRar_Multi_ArchiveStreamReadAsync(
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
            await entry.WriteEntryToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
    }

    [Fact]
    public Task Rar5_MultiSolid_ArchiveStreamRead() =>
        DoRar_Multi_ArchiveStreamReadAsync(
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
    public Task RarNoneArchiveFileRead() => ArchiveFileReadAsync("Rar.none.rar");

    [Fact]
    public Task Rar5NoneArchiveFileRead() => ArchiveFileReadAsync("Rar5.none.rar");

    [Fact]
    public Task Rar_ArchiveFileRead() => ArchiveFileReadAsync("Rar.rar");

    [Fact]
    public Task Rar5_ArchiveFileRead() => ArchiveFileReadAsync("Rar5.rar");

    [Fact]
    public void Rar_ArchiveFileRead_HasDirectories() =>
        DoRar_ArchiveFileRead_HasDirectories("Rar.rar");

    [Fact]
    public void Rar5_ArchiveFileRead_HasDirectories() =>
        DoRar_ArchiveFileRead_HasDirectories("Rar5.rar");

    private void DoRar_ArchiveFileRead_HasDirectories(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var archive = RarArchive.Open(stream);
        Assert.False(archive.IsSolid);
        Assert.Contains(true, archive.Entries.Select(entry => entry.IsDirectory));
    }

    [Fact]
    public async Task Rar_Jpg_ArchiveFileRead()
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
                await entry.WriteEntryToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public Task Rar_Solid_ArchiveFileRead() => ArchiveFileReadAsync("Rar.solid.rar");

    [Fact]
    public Task Rar5_Solid_ArchiveFileRead() => ArchiveFileReadAsync("Rar5.solid.rar");

    [Fact]
    public Task Rar2_Multi_ArchiveStreamRead() =>
        DoRar_Multi_ArchiveStreamReadAsync(
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
    public Task Rar2_Multi_ArchiveFileRead() => ArchiveFileReadAsync("Rar2.multi.rar"); //r00, r01...

    [Fact]
    public Task Rar2_ArchiveFileRead() => ArchiveFileReadAsync("Rar2.rar");

    [Fact]
    public async Task Rar15_ArchiveFileRead()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveFileReadAsync("Rar15.rar");
    }

    [Fact]
    public void Rar15_ArchiveVersionTest()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Rar15.rar");

        using var archive = RarArchive.Open(testArchive);
        Assert.Equal(1, archive.MinVersion);
        Assert.Equal(1, archive.MaxVersion);
    }

    [Fact]
    public void Rar2_ArchiveVersionTest()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Rar2.rar");

        using var archive = RarArchive.Open(testArchive);
        Assert.Equal(2, archive.MinVersion);
        Assert.Equal(2, archive.MaxVersion);
    }

    [Fact]
    public void Rar4_ArchiveVersionTest()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Rar4.multi.part01.rar");

        using var archive = RarArchive.Open(testArchive);
        Assert.Equal(3, archive.MinVersion);
        Assert.Equal(4, archive.MaxVersion);
    }

    [Fact]
    public void Rar5_ArchiveVersionTest()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Rar5.solid.rar");

        using var archive = RarArchive.Open(testArchive);
        Assert.Equal(5, archive.MinVersion);
        Assert.Equal(6, archive.MaxVersion);
    }

    [Fact]
    public Task Rar4_Multi_ArchiveFileRead() => ArchiveFileReadAsync("Rar4.multi.part01.rar");

    [Fact]
    public Task Rar4_ArchiveFileRead() => ArchiveFileReadAsync("Rar4.rar");

    [Fact]
    public void Rar_GetPartsSplit() =>
        //uses first part to search for all parts and compares against this array
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
    public void Rar_GetPartsOld() =>
        //uses first part to search for all parts and compares against this array
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
    public void Rar_GetPartsNew() =>
        //uses first part to search for all parts and compares against this array
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
    public Task Rar4_Multi_ArchiveStreamRead() =>
        DoRar_Multi_ArchiveStreamReadAsync(
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

    //no extension to test the lib identifies the archive by content not ext
    [Fact]
    public Task Rar4_Split_ArchiveStreamRead() =>
        ArchiveStreamMultiReadAsync(
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

    //will detect and load other files
    [Fact]
    public Task Rar4_Multi_ArchiveFirstFileRead() => ArchiveFileReadAsync("Rar4.multi.part01.rar");

    //"Rar4.multi.part02.rar",
    //"Rar4.multi.part03.rar",
    //"Rar4.multi.part04.rar",
    //"Rar4.multi.part05.rar",
    //"Rar4.multi.part06.rar",
    //"Rar4.multi.part07.rar"
    //will detect and load other files
    [Fact]
    public Task Rar4_Split_ArchiveFirstFileRead() => ArchiveFileReadAsync("Rar4.split.001");

    //"Rar4.split.002",
    //"Rar4.split.003",
    //"Rar4.split.004",
    //"Rar4.split.005",
    //"Rar4.split.006"
    //will detect and load other files
    [Fact]
    public Task Rar4_Split_ArchiveStreamFirstFileRead() =>
        ArchiveStreamMultiReadAsync(
            null,
            [
                "Rar4.split.001",
                //"Rar4.split.002",
                //"Rar4.split.003",
                //"Rar4.split.004",
                //"Rar4.split.005",
                //"Rar4.split.006"
            ]
        );

    //open with ArchiveFactory.Open and stream
    [Fact]
    public Task Rar4_Split_ArchiveOpen() =>
        ArchiveOpenStreamReadAsync(
            null,
            "Rar4.split.001",
            "Rar4.split.002",
            "Rar4.split.003",
            "Rar4.split.004",
            "Rar4.split.005",
            "Rar4.split.006"
        );

    //open with ArchiveFactory.Open and stream
    [Fact]
    public Task Rar4_Multi_ArchiveOpen() =>
        ArchiveOpenStreamReadAsync(
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
    public void Rar4_Multi_ArchiveOpenEntryVolumeIndexTest() =>
        ArchiveOpenEntryVolumeIndexTest(
            [
                [0, 1], //exe - Rar4.multi.part01.rar to Rar4.multi.part02.rar
                [1, 5], //jpg - Rar4.multi.part02.rar to Rar4.multi.part06.rar
                [5, 6], //txt - Rar4.multi.part06.rar to Rar4.multi.part07.rar
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
    public Task Rar_Multi_ArchiveFileRead() => ArchiveFileReadAsync("Rar.multi.part01.rar");

    [Fact]
    public Task Rar5_Multi_ArchiveFileRead() => ArchiveFileReadAsync("Rar5.multi.part01.rar");

    [Fact]
    public void Rar_IsFirstVolume_True() => DoRar_IsFirstVolume_True("Rar.multi.part01.rar");

    [Fact]
    public void Rar5_IsFirstVolume_True() => DoRar_IsFirstVolume_True("Rar5.multi.part01.rar");

    private void DoRar_IsFirstVolume_True(string firstFilename)
    {
        using var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, firstFilename));
        Assert.True(archive.IsMultipartVolume());
        Assert.True(archive.IsFirstVolume());
    }

    [Fact]
    public void Rar_IsFirstVolume_False() => DoRar_IsFirstVolume_False("Rar.multi.part03.rar");

    [Fact]
    public void Rar5_IsFirstVolume_False() => DoRar_IsFirstVolume_False("Rar5.multi.part03.rar");

    private void DoRar_IsFirstVolume_False(string notFirstFilename)
    {
        using var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, notFirstFilename));
        Assert.True(archive.IsMultipartVolume());
        Assert.False(archive.IsFirstVolume());
    }

    [Fact]
    public Task Rar5_CRC_Blake2_Archive() => ArchiveFileReadAsync("Rar5.crc_blake2.rar");

    [Fact]
    void Rar_Iterate_Archive() =>
        ArchiveFileSkip("Rar.rar", "Failure jpg exe Empty jpg\\test.jpg exe\\test.exe тест.txt");

    [Fact]
    public void Rar2_Iterate_Archive() =>
        ArchiveFileSkip("Rar2.rar", "Failure Empty тест.txt jpg\\test.jpg exe\\test.exe jpg exe");

    [Fact]
    public void Rar4_Iterate_Archive() =>
        ArchiveFileSkip("Rar4.rar", "Failure Empty jpg exe тест.txt jpg\\test.jpg exe\\test.exe");

    [Fact]
    public void Rar5_Iterate_Archive() =>
        ArchiveFileSkip("Rar5.rar", "Failure jpg exe Empty тест.txt jpg\\test.jpg exe\\test.exe");

    [Fact]
    public void Rar_Encrypted_Iterate_Archive() =>
        ArchiveFileSkip(
            "Rar.encrypted_filesOnly.rar",
            "Failure jpg exe Empty тест.txt jpg\\test.jpg exe\\test.exe"
        );

    [Fact]
    public void Rar5_Encrypted_Iterate_Archive() =>
        ArchiveFileSkip(
            "Rar5.encrypted_filesOnly.rar",
            "Failure jpg exe Empty тест.txt jpg\\test.jpg exe\\test.exe"
        );
}

