using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA.Utilites;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Rar;

public class RarArchiveTests : ArchiveTests
{
    [Fact]
    public void Rar_EncryptedFileAndHeader_Archive() =>
        ReadRarPassword("Rar.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public void Rar_EncryptedFileAndHeader_NoPasswordExceptionTest() =>
        Assert.Throws(
            typeof(CryptographicException),
            () => ReadRarPassword("Rar.encrypted_filesAndHeader.rar", null)
        );

    [Fact]
    public void Rar5_EncryptedFileAndHeader_Archive() =>
        ReadRarPassword("Rar5.encrypted_filesAndHeader.rar", "test");

    [Fact]
    public void Rar5_EncryptedFileAndHeader_Archive_Err() =>
        Assert.Throws(
            typeof(CryptographicException),
            () => ReadRarPassword("Rar5.encrypted_filesAndHeader.rar", "failed")
        );

    [Fact]
    public void Rar5_EncryptedFileAndHeader_NoPasswordExceptionTest() =>
        Assert.Throws(
            typeof(CryptographicException),
            () => ReadRarPassword("Rar5.encrypted_filesAndHeader.rar", null)
        );

    [Fact]
    public void Rar_EncryptedFileOnly_Archive() =>
        ReadRarPassword("Rar.encrypted_filesOnly.rar", "test");

    [Fact]
    public void Rar_EncryptedFileOnly_Archive_Err() =>
        Assert.Throws(
            typeof(CryptographicException),
            () => ReadRarPassword("Rar5.encrypted_filesOnly.rar", "failed")
        );

    [Fact]
    public void Rar5_EncryptedFileOnly_Archive() =>
        ReadRarPassword("Rar5.encrypted_filesOnly.rar", "test");

    [Fact]
    public void Rar_Encrypted_Archive() => ReadRarPassword("Rar.Encrypted.rar", "test");

    [Fact]
    public void Rar5_Encrypted_Archive() =>
        ReadRarPassword("Rar5.encrypted_filesAndHeader.rar", "test");

    private void ReadRarPassword(string testArchive, string? password)
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
                    entry.WriteToDirectory(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Rar_Multi_Archive_Encrypted() =>
        Assert.Throws<InvalidFormatException>(() =>
            ArchiveFileReadPassword("Rar.EncryptedParts.part01.rar", "test")
        );

    protected void ArchiveFileReadPassword(string archiveName, string password)
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
                entry.WriteToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Rar_None_ArchiveStreamRead() => ArchiveStreamRead("Rar.none.rar");

    [Fact]
    public void Rar5_None_ArchiveStreamRead() => ArchiveStreamRead("Rar5.none.rar");

    [Fact]
    public void Rar_ArchiveStreamRead() => ArchiveStreamRead("Rar.rar");

    [Fact]
    public void Rar5_ArchiveStreamRead() => ArchiveStreamRead("Rar5.rar");

    [Fact]
    public void Rar_test_invalid_exttime_ArchiveStreamRead() =>
        DoRar_test_invalid_exttime_ArchiveStreamRead("Rar.test_invalid_exttime.rar");

    private void DoRar_test_invalid_exttime_ArchiveStreamRead(string filename)
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename));
        using var archive = ArchiveFactory.Open(stream);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            entry.WriteToDirectory(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
    }

    [Fact]
    public void Rar_Jpg_ArchiveStreamRead()
    {
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.jpeg.jpg"));
        using (var archive = RarArchive.Open(stream, new ReaderOptions { LookForHeader = true }))
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Rar_IsSolidArchiveCheck() => DoRar_IsSolidArchiveCheck("Rar.rar");

    [Fact]
    public void Rar5_IsSolidArchiveCheck() => DoRar_IsSolidArchiveCheck("Rar5.rar");

    private void DoRar_IsSolidArchiveCheck(string filename)
    {
        using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename)))
        {
            using var archive = RarArchive.Open(stream);
            Assert.False(archive.IsSolid);
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(
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
    public void Rar_Solid_ArchiveStreamRead() => ArchiveStreamRead("Rar.solid.rar");

    [Fact]
    public void Rar5_Solid_ArchiveStreamRead() => ArchiveStreamRead("Rar5.solid.rar");

    [Fact]
    public void Rar_Solid_StreamRead_Extract_All() =>
        ArchiveStreamReadExtractAll("Rar.solid.rar", CompressionType.Rar);

    [Fact]
    public void Rar5_Solid_StreamRead_Extract_All() =>
        ArchiveStreamReadExtractAll("Rar5.solid.rar", CompressionType.Rar);

    [Fact]
    public void Rar_Multi_ArchiveStreamRead() =>
        DoRar_Multi_ArchiveStreamRead(
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
    public void Rar5_Multi_ArchiveStreamRead() =>
        DoRar_Multi_ArchiveStreamRead(
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

    private void DoRar_Multi_ArchiveStreamRead(string[] archives, bool isSolid)
    {
        using var archive = RarArchive.Open(
            archives.Select(s => Path.Combine(TEST_ARCHIVES_PATH, s)).Select(File.OpenRead)
        );
        Assert.Equal(archive.IsSolid, isSolid);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            entry.WriteToDirectory(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
    }

    [Fact]
    public void Rar5_MultiSolid_ArchiveStreamRead() =>
        DoRar_Multi_ArchiveStreamRead(
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
    public void RarNoneArchiveFileRead() => ArchiveFileRead("Rar.none.rar");

    [Fact]
    public void Rar5NoneArchiveFileRead() => ArchiveFileRead("Rar5.none.rar");

    [Fact]
    public void Rar_ArchiveFileRead() => ArchiveFileRead("Rar.rar");

    [Fact]
    public void Rar5_ArchiveFileRead() => ArchiveFileRead("Rar5.rar");

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
    public void Rar_Jpg_ArchiveFileRead()
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
                entry.WriteToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public void Rar_Solid_ArchiveFileRead() => ArchiveFileRead("Rar.solid.rar");

    [Fact]
    public void Rar5_Solid_ArchiveFileRead() => ArchiveFileRead("Rar5.solid.rar");

    [Fact]
    public void Rar2_Multi_ArchiveStreamRead() =>
        DoRar_Multi_ArchiveStreamRead(
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
    public void Rar2_Multi_ArchiveFileRead() => ArchiveFileRead("Rar2.multi.rar"); //r00, r01...

    [Fact]
    public void Rar2_ArchiveFileRead() => ArchiveFileRead("Rar2.rar");

    [Fact]
    public void Rar15_ArchiveFileRead()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        ArchiveFileRead("Rar15.rar");
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
    public void Rar4_Multi_ArchiveFileRead() => ArchiveFileRead("Rar4.multi.part01.rar");

    [Fact]
    public void Rar4_ArchiveFileRead() => ArchiveFileRead("Rar4.rar");

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
    public void Rar4_Multi_ArchiveStreamRead() =>
        DoRar_Multi_ArchiveStreamRead(
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
    public void Rar4_Split_ArchiveStreamRead() =>
        ArchiveStreamMultiRead(
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
    public void Rar4_Multi_ArchiveFirstFileRead() => ArchiveFileRead("Rar4.multi.part01.rar");

    //"Rar4.multi.part02.rar",
    //"Rar4.multi.part03.rar",
    //"Rar4.multi.part04.rar",
    //"Rar4.multi.part05.rar",
    //"Rar4.multi.part06.rar",
    //"Rar4.multi.part07.rar"
    //will detect and load other files
    [Fact]
    public void Rar4_Split_ArchiveFirstFileRead() => ArchiveFileRead("Rar4.split.001");

    //"Rar4.split.002",
    //"Rar4.split.003",
    //"Rar4.split.004",
    //"Rar4.split.005",
    //"Rar4.split.006"
    //will detect and load other files
    [Fact]
    public void Rar4_Split_ArchiveStreamFirstFileRead() =>
        ArchiveStreamMultiRead(
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
    public void Rar4_Split_ArchiveOpen() =>
        ArchiveOpenStreamRead(
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
    public void Rar4_Multi_ArchiveOpen() =>
        ArchiveOpenStreamRead(
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
    public void Rar_Multi_ArchiveFileRead() => ArchiveFileRead("Rar.multi.part01.rar");

    [Fact]
    public void Rar5_Multi_ArchiveFileRead() => ArchiveFileRead("Rar5.multi.part01.rar");

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
    public void Rar5_CRC_Blake2_Archive() => ArchiveFileRead("Rar5.crc_blake2.rar");

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

    [Fact]
    public void Rar_TestEncryptedDetection()
    {
        using var passwordProtectedFilesArchive = RarArchive.Open(
            Path.Combine(TEST_ARCHIVES_PATH, "Rar.encrypted_filesOnly.rar")
        );
        Assert.True(passwordProtectedFilesArchive.IsEncrypted);
    }

    /// <summary>
    /// Test for issue: InvalidOperationException when extracting RAR files.
    /// This test verifies the fix for the validation logic that was changed from
    /// (_position != Length) to (_position &lt; Length).
    /// The old logic would throw an exception when position exceeded expected length,
    /// but the new logic only throws when decompression ends prematurely (position &lt; expected).
    /// </summary>
    [Fact]
    public void Rar_StreamValidation_OnlyThrowsOnPrematureEnd()
    {
        // Test normal extraction - should NOT throw InvalidOperationException
        // even if actual decompressed size differs from header
        var testFiles = new[] { "Rar.rar", "Rar5.rar", "Rar4.rar", "Rar2.rar" };

        foreach (var testFile in testFiles)
        {
            using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, testFile));
            using var archive = RarArchive.Open(stream);

            // Extract all entries and read them completely
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                using var entryStream = entry.OpenEntryStream();
                using var ms = new MemoryStream();

                // This should complete without throwing InvalidOperationException
                // The fix ensures we only throw when position &lt; expected length, not when position >= expected
                entryStream.CopyTo(ms);

                // Verify we read some data
                Assert.True(
                    ms.Length > 0,
                    $"Failed to extract data from {entry.Key} in {testFile}"
                );
            }
        }
    }

    /// <summary>
    /// Negative test case: Verifies that InvalidOperationException IS thrown when
    /// a RAR stream ends prematurely (position &lt; expected length).
    /// This tests the validation condition (_position &lt; Length) works correctly.
    /// </summary>
    [Fact]
    public void Rar_StreamValidation_ThrowsOnTruncatedStream()
    {
        // This test verifies the exception is thrown when decompression ends prematurely
        // by using a truncated stream that stops reading after a small number of bytes
        var testFile = "Rar.rar";
        using var fileStream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, testFile));

        // Wrap the file stream with a truncated stream that will stop reading early
        // This simulates a corrupted or truncated RAR file
        using var truncatedStream = new TruncatedStream(fileStream, 1000);

        // Opening the archive should work, but extracting should throw
        // when we try to read beyond the truncated data
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using var archive = RarArchive.Open(truncatedStream);
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                using var entryStream = entry.OpenEntryStream();
                using var ms = new MemoryStream();
                // This should throw InvalidOperationException when it can't read all expected bytes
                entryStream.CopyTo(ms);
            }
        });

        // Verify the exception message matches our expectation
        Assert.Contains("unpacked file size does not match header", exception.Message);
    }
}
