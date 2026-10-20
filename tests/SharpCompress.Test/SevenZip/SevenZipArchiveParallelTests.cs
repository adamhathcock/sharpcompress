using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.Factories;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.SevenZip;

// Parallel counterpart of SevenZipArchiveTests: every test here opts in via
// ReaderOptions.EnableParallelism (default is sequential decode), covering the same archives to
// verify decoded content is identical regardless of which decode path 7-Zip's automatic parallel
// LZMA2 solid-folder decode takes.
public class SevenZipArchiveParallelTests : ArchiveTests
{
    private static ReaderOptions Parallel(ReaderOptions options) =>
        options.WithEnableParallelism(true);

    [Fact]
    public void SevenZipArchive_Solid_StreamRead_Parallel() =>
        ArchiveStreamRead("7Zip.solid.7z", Parallel(ReaderOptions.ForExternalStream));

    [Fact]
    public void SevenZipArchive_NonSolid_StreamRead_Parallel() =>
        ArchiveStreamRead("7Zip.nonsolid.7z", Parallel(ReaderOptions.ForExternalStream));

    [Fact]
    public void SevenZipArchive_LZMA_StreamRead_Parallel() =>
        ArchiveStreamRead("7Zip.LZMA.7z", Parallel(ReaderOptions.ForExternalStream));

    [Fact]
    public void SevenZipArchive_LZMA_PathRead_Parallel() =>
        ArchiveFileRead("7Zip.LZMA.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_LZMAAES_StreamRead_Parallel() =>
        ArchiveStreamRead(
            "7Zip.LZMA.Aes.7z",
            Parallel(ReaderOptions.ForExternalStream) with
            {
                Password = "testpassword",
            }
        );

    [Fact]
    public void SevenZipArchive_LZMAAES_PathRead_Parallel() =>
        ArchiveFileRead(
            "7Zip.LZMA.Aes.7z",
            Parallel(ReaderOptions.ForFilePath) with
            {
                Password = "testpassword",
            }
        );

    [Fact]
    public void SevenZipArchive_PPMd_StreamRead_Parallel() =>
        ArchiveStreamRead("7Zip.PPMd.7z", Parallel(ReaderOptions.ForExternalStream));

    [Fact]
    public void SevenZipArchive_PPMd_PathRead_Parallel() =>
        ArchiveFileRead("7Zip.PPMd.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_LZMA2_StreamRead_Parallel() =>
        ArchiveStreamRead("7Zip.LZMA2.7z", Parallel(ReaderOptions.ForExternalStream));

    [Fact]
    public void SevenZipArchive_LZMA2_PathRead_Parallel() =>
        ArchiveFileRead("7Zip.LZMA2.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_LZMA2_EXE_StreamRead_Parallel() =>
        ArchiveStreamRead(
            new SevenZipFactory(),
            "7Zip.LZMA2.exe",
            Parallel(ReaderOptions.ForExternalStream).WithLookForHeader(true)
        );

    [Fact]
    public void SevenZipArchive_LZMA2_EXE_PathRead_Parallel() =>
        ArchiveFileRead(
            "7Zip.LZMA2.exe",
            Parallel(ReaderOptions.ForFilePath).WithLookForHeader(true),
            new SevenZipFactory()
        );

    [Fact]
    public void SevenZipArchive_LZMA2AES_StreamRead_Parallel() =>
        ArchiveStreamRead(
            "7Zip.LZMA2.Aes.7z",
            Parallel(ReaderOptions.ForExternalStream) with
            {
                Password = "testpassword",
            }
        );

    [Fact]
    public void SevenZipArchive_LZMA2AES_PathRead_Parallel() =>
        ArchiveFileRead(
            "7Zip.LZMA2.Aes.7z",
            Parallel(ReaderOptions.ForFilePath) with
            {
                Password = "testpassword",
            }
        );

    [Fact]
    public void SevenZipArchive_BZip2_StreamRead_Parallel() =>
        ArchiveStreamRead("7Zip.BZip2.7z", Parallel(ReaderOptions.ForExternalStream));

    [Fact]
    public void SevenZipArchive_BZip2_PathRead_Parallel() =>
        ArchiveFileRead("7Zip.BZip2.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_Copy_StreamRead_Parallel() =>
        ArchiveStreamRead("7Zip.Copy.7z", Parallel(ReaderOptions.ForExternalStream));

    [Fact]
    public void SevenZipArchive_Copy_PathRead_Parallel() =>
        ArchiveFileRead("7Zip.Copy.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_Copy_CompressionType_Parallel()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.Copy.7z")))
        using (
            var archive = SevenZipArchive.OpenArchive(
                stream,
                Parallel(ReaderOptions.ForExternalStream)
            )
        )
        {
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                Assert.Equal(CompressionType.None, entry.CompressionType);
            }
        }
    }

    [Fact]
    public void SevenZipArchive_ZSTD_StreamRead_Parallel() =>
        ArchiveStreamRead("7Zip.ZSTD.7z", Parallel(ReaderOptions.ForExternalStream));

    [Fact]
    public void SevenZipArchive_ZSTD_PathRead_Parallel() =>
        ArchiveFileRead("7Zip.ZSTD.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_ZSTD_Split_Parallel() =>
        ArchiveStreamMultiRead(
            Parallel(ReaderOptions.ForFilePath),
            "7Zip.ZSTD.Split.7z.001",
            "7Zip.ZSTD.Split.7z.002",
            "7Zip.ZSTD.Split.7z.003",
            "7Zip.ZSTD.Split.7z.004",
            "7Zip.ZSTD.Split.7z.005",
            "7Zip.ZSTD.Split.7z.006"
        );

    [Fact]
    public void SevenZipArchive_EOS_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.eos.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_Delta_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.delta.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_ARM_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.ARM.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_ARMT_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.ARMT.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_BCJ_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.BCJ.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_BCJ2_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.BCJ2.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_IA64_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.IA64.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_PPC_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.PPC.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_SPARC_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.SPARC.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_ARM64_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.ARM64.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_RISCV_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.RISCV.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_Filters_FileRead_Parallel() =>
        ArchiveFileRead("7Zip.Filters.7z", Parallel(ReaderOptions.ForFilePath));

    [Fact]
    public void SevenZipArchive_Tar_PathRead_Parallel()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.Tar.tar.7z")))
        using (
            var archive = SevenZipArchive.OpenArchive(
                stream,
                Parallel(ReaderOptions.ForExternalStream)
            )
        )
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
    public void SevenZipArchive_Solid_ExtractAllEntries_Contiguous_Parallel()
    {
        // This test verifies that solid archives iterate entries as contiguous streams
        // rather than recreating the decompression stream for each entry, with the parallel
        // decode path opted in.
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.7z");
        using var archive = SevenZipArchive.OpenArchive(
            testArchive,
            Parallel(ReaderOptions.ForFilePath)
        );
        Assert.True(archive.IsSolid);

        using var reader = archive.ExtractAllEntries();
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryToDirectory(SCRATCH_FILES_PATH);
            }
        }

        VerifyFiles();
    }

    [Fact]
    public void SevenZipArchive_EmptyStream_WriteToDirectory_Parallel()
    {
        // This test specifically verifies that archives with empty-stream entries
        // (files with size 0 and no compressed data) can be extracted without throwing
        // NullReferenceException, with the parallel decode path opted in.
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.EmptyStream.7z");
        using var archive = SevenZipArchive.OpenArchive(
            testArchive,
            Parallel(ReaderOptions.ForFilePath)
        );

        var emptyStreamFileCount = 0;
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                var sevenZipEntry = entry as SevenZipEntry;
                if (sevenZipEntry?.FilePart.Header.HasStream == false)
                {
                    emptyStreamFileCount++;
                }

                entry.WriteToDirectory(SCRATCH_FILES_PATH);
            }
        }

        Assert.True(
            emptyStreamFileCount > 0,
            "Test archive should contain at least one empty-stream entry"
        );

        var extractedFiles = Directory.GetFiles(
            SCRATCH_FILES_PATH,
            "*",
            SearchOption.AllDirectories
        );
        Assert.NotEmpty(extractedFiles);

        foreach (var file in extractedFiles)
        {
            var fileInfo = new FileInfo(file);
            Assert.Equal(0, fileInfo.Length);
        }
    }
}
