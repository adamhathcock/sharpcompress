using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipArchiveTests : ArchiveTests
{
    [Fact]
    public void SevenZipArchive_Solid_StreamRead() => ArchiveStreamRead("7Zip.solid.7z");

    [Fact]
    public void SevenZipArchive_NonSolid_StreamRead() => ArchiveStreamRead("7Zip.nonsolid.7z");

    [Fact]
    public void SevenZipArchive_LZMA_StreamRead() => ArchiveStreamRead("7Zip.LZMA.7z");

    [Fact]
    public void SevenZipArchive_LZMA_PathRead() => ArchiveFileRead("7Zip.LZMA.7z");

    [Fact]
    public void SevenZipArchive_LZMAAES_StreamRead() =>
        ArchiveStreamRead("7Zip.LZMA.Aes.7z", new ReaderOptions { Password = "testpassword" });

    [Fact]
    public void SevenZipArchive_LZMAAES_PathRead() =>
        ArchiveFileRead("7Zip.LZMA.Aes.7z", new ReaderOptions { Password = "testpassword" });

    [Fact]
    public void SevenZipArchive_LZMAAES_NoPasswordExceptionTest() =>
        Assert.Throws(
            typeof(CryptographicException),
            () => ArchiveFileRead("7Zip.LZMA.Aes.7z", new ReaderOptions { Password = null })
        ); //was failing with ArgumentNullException not CryptographicException like rar

    [Fact]
    public void SevenZipArchive_PPMd_StreamRead() => ArchiveStreamRead("7Zip.PPMd.7z");

    [Fact]
    public void SevenZipArchive_PPMd_StreamRead_Extract_All() =>
        ArchiveStreamReadExtractAll("7Zip.PPMd.7z", CompressionType.PPMd);

    [Fact]
    public void SevenZipArchive_PPMd_PathRead() => ArchiveFileRead("7Zip.PPMd.7z");

    [Fact]
    public void SevenZipArchive_LZMA2_StreamRead() => ArchiveStreamRead("7Zip.LZMA2.7z");

    [Fact]
    public void SevenZipArchive_LZMA2_PathRead() => ArchiveFileRead("7Zip.LZMA2.7z");

    [Fact]
    public void SevenZipArchive_LZMA2_EXE_StreamRead() =>
        ArchiveStreamRead(new SevenZipFactory(), "7Zip.LZMA2.exe", new() { LookForHeader = true });

    [Fact]
    public void SevenZipArchive_LZMA2_EXE_PathRead() =>
        ArchiveFileRead(new SevenZipFactory(), "7Zip.LZMA2.exe", new() { LookForHeader = true });

    [Fact]
    public void SevenZipArchive_LZMA2AES_StreamRead() =>
        ArchiveStreamRead("7Zip.LZMA2.Aes.7z", new ReaderOptions { Password = "testpassword" });

    [Fact]
    public void SevenZipArchive_LZMA2AES_PathRead() =>
        ArchiveFileRead("7Zip.LZMA2.Aes.7z", new ReaderOptions { Password = "testpassword" });

    [Fact]
    public void SevenZipArchive_BZip2_StreamRead() => ArchiveStreamRead("7Zip.BZip2.7z");

    [Fact]
    public void SevenZipArchive_BZip2_PathRead() => ArchiveFileRead("7Zip.BZip2.7z");

    [Fact]
    public void SevenZipArchive_LZMA_Time_Attributes_PathRead() =>
        ArchiveFileReadEx("7Zip.LZMA.7z");

    [Fact]
    public void SevenZipArchive_BZip2_Split() =>
        Assert.Throws<InvalidOperationException>(() =>
            ArchiveStreamRead(
                null,
                "Original.7z.001",
                "Original.7z.002",
                "Original.7z.003",
                "Original.7z.004",
                "Original.7z.005",
                "Original.7z.006",
                "Original.7z.007"
            )
        );

    //Same as archive as Original.7z.001 ... 007 files without the root directory 'Original\' in the archive - this caused the verify to fail
    [Fact]
    public void SevenZipArchive_BZip2_Split_Working() =>
        ArchiveStreamMultiRead(
            null,
            "7Zip.BZip2.split.001",
            "7Zip.BZip2.split.002",
            "7Zip.BZip2.split.003",
            "7Zip.BZip2.split.004",
            "7Zip.BZip2.split.005",
            "7Zip.BZip2.split.006",
            "7Zip.BZip2.split.007"
        );

    //will detect and load other files
    [Fact]
    public void SevenZipArchive_BZip2_Split_FirstFileRead() =>
        ArchiveFileRead("7Zip.BZip2.split.001");

    //"7Zip.BZip2.split.002",
    //"7Zip.BZip2.split.003",
    //"7Zip.BZip2.split.004",
    //"7Zip.BZip2.split.005",
    //"7Zip.BZip2.split.006",
    //"7Zip.BZip2.split.007"

    [Fact]
    public void SevenZipArchive_Copy_StreamRead() => ArchiveStreamRead("7Zip.Copy.7z");

    [Fact]
    public void SevenZipArchive_Copy_PathRead() => ArchiveFileRead("7Zip.Copy.7z");

    [Fact]
    public void SevenZipArchive_Copy_CompressionType()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.Copy.7z")))
        using (var archive = SevenZipArchive.Open(stream))
        {
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                Assert.Equal(CompressionType.None, entry.CompressionType);
            }
        }
    }

    [Fact]
    public void SevenZipArchive_ZSTD_StreamRead() => ArchiveStreamRead("7Zip.ZSTD.7z");

    [Fact]
    public void SevenZipArchive_ZSTD_PathRead() => ArchiveFileRead("7Zip.ZSTD.7z");

    [Fact]
    public void SevenZipArchive_ZSTD_Split() =>
        Assert.Throws<InvalidOperationException>(() =>
            ArchiveStreamRead(
                null,
                "7Zip.ZSTD.Split.7z.001",
                "7Zip.ZSTD.Split.7z.002",
                "7Zip.ZSTD.Split.7z.003",
                "7Zip.ZSTD.Split.7z.004",
                "7Zip.ZSTD.Split.7z.005",
                "7Zip.ZSTD.Split.7z.006"
            )
        );

    [Fact]
    public void SevenZipArchive_EOS_FileRead() => ArchiveFileRead("7Zip.eos.7z");

    [Fact]
    public void SevenZipArchive_Delta_FileRead() => ArchiveFileRead("7Zip.delta.7z");

    [Fact]
    public void SevenZipArchive_ARM_FileRead() => ArchiveFileRead("7Zip.ARM.7z");

    [Fact]
    public void SevenZipArchive_ARMT_FileRead() => ArchiveFileRead("7Zip.ARMT.7z");

    [Fact]
    public void SevenZipArchive_BCJ_FileRead() => ArchiveFileRead("7Zip.BCJ.7z");

    [Fact]
    public void SevenZipArchive_BCJ2_FileRead() => ArchiveFileRead("7Zip.BCJ2.7z");

    [Fact]
    public void SevenZipArchive_IA64_FileRead() => ArchiveFileRead("7Zip.IA64.7z");

    [Fact]
    public void SevenZipArchive_PPC_FileRead() => ArchiveFileRead("7Zip.PPC.7z");

    [Fact]
    public void SevenZipArchive_SPARC_FileRead() => ArchiveFileRead("7Zip.SPARC.7z");

    [Fact]
    public void SevenZipArchive_ARM64_FileRead() => ArchiveFileRead("7Zip.ARM64.7z");

    [Fact]
    public void SevenZipArchive_RISCV_FileRead() => ArchiveFileRead("7Zip.RISCV.7z");

    [Fact]
    public void SevenZipArchive_Filters_FileRead() => ArchiveFileRead("7Zip.Filters.7z");

    [Fact]
    public void SevenZipArchive_Delta_Distance() =>
        ArchiveDeltaDistanceRead("7Zip.delta.distance.7z");

    [Fact]
    public void SevenZipArchive_Tar_PathRead()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.Tar.tar.7z")))
        using (var archive = SevenZipArchive.Open(stream))
        {
            var entry = archive.Entries.First();
            entry.WriteToFile(Path.Combine(SCRATCH_FILES_PATH, entry.Key.NotNull()));

            var size = entry.Size;
            var scratch = new FileInfo(Path.Combine(SCRATCH_FILES_PATH, "7Zip.Tar.tar"));
            var test = new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.Tar.tar"));

            Assert.Equal(size, scratch.Length);
            Assert.Equal(size, test.Length);
        }

        CompareArchivesByPath(
            Path.Combine(SCRATCH_FILES_PATH, "7Zip.Tar.tar"),
            Path.Combine(TEST_ARCHIVES_PATH, "7Zip.Tar.tar")
        );
    }

    [Fact]
    public void SevenZipArchive_TestEncryptedDetection()
    {
        using var passwordProtectedFilesArchive = SevenZipArchive.Open(
            Path.Combine(TEST_ARCHIVES_PATH, "7Zip.encryptedFiles.7z")
        );
        Assert.True(passwordProtectedFilesArchive.IsEncrypted);
    }

    [Fact]
    public void SevenZipArchive_TestSolidDetection()
    {
        using var oneBlockSolidArchive = SevenZipArchive.Open(
            Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.1block.7z")
        );
        Assert.True(oneBlockSolidArchive.IsSolid);

        using var solidArchive = SevenZipArchive.Open(
            Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.7z")
        );
        Assert.True(solidArchive.IsSolid);

        using var nonSolidArchive = SevenZipArchive.Open(
            Path.Combine(TEST_ARCHIVES_PATH, "7Zip.nonsolid.7z")
        );
        Assert.False(nonSolidArchive.IsSolid);
    }
}
