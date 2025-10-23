using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        public Task SevenZipArchive_Solid_StreamRead() => ArchiveStreamReadAsync("7Zip.solid.7z");

        [Fact]
        public Task SevenZipArchive_NonSolid_StreamRead() => ArchiveStreamReadAsync("7Zip.nonsolid.7z");

        [Fact]
        public Task SevenZipArchive_LZMA_StreamRead() => ArchiveStreamReadAsync("7Zip.LZMA.7z");

        [Fact]
        public Task SevenZipArchive_LZMA_PathRead() => ArchiveFileReadAsync("7Zip.LZMA.7z");

        [Fact]
        public Task SevenZipArchive_LZMAAES_StreamRead() =>
            ArchiveStreamReadAsync("7Zip.LZMA.Aes.7z", new ReaderOptions { Password = "testpassword" });

        [Fact]
        public Task SevenZipArchive_LZMAAES_PathRead() =>
            ArchiveFileReadAsync("7Zip.LZMA.Aes.7z", new ReaderOptions { Password = "testpassword" });

        [Fact]
        public Task SevenZipArchive_LZMAAES_NoPasswordExceptionTest() =>
            Assert.ThrowsAsync(
                typeof(CryptographicException),
                async () => await ArchiveFileReadAsync("7Zip.LZMA.Aes.7z", new ReaderOptions { Password = null })
            ); //was failing with ArgumentNullException not CryptographicException like rar

        [Fact]
        public Task SevenZipArchive_PPMd_StreamRead() => ArchiveStreamReadAsync("7Zip.PPMd.7z");

        [Fact]
        public Task SevenZipArchive_PPMd_StreamRead_Extract_All() =>
            ArchiveStreamReadExtractAllAsync("7Zip.PPMd.7z", CompressionType.PPMd);

        [Fact]
        public Task SevenZipArchive_PPMd_PathRead() => ArchiveFileReadAsync("7Zip.PPMd.7z");

        [Fact]
        public Task SevenZipArchive_LZMA2_StreamRead() => ArchiveStreamReadAsync("7Zip.LZMA2.7z");

        [Fact]
        public Task SevenZipArchive_LZMA2_PathRead() => ArchiveFileReadAsync("7Zip.LZMA2.7z");

        [Fact]
        public Task SevenZipArchive_LZMA2_EXE_StreamRead() =>
            ArchiveStreamReadAsync(new SevenZipFactory(), "7Zip.LZMA2.exe", new() { LookForHeader = true });

        [Fact]
        public Task SevenZipArchive_LZMA2_EXE_PathRead() =>
            ArchiveFileReadAsync(new SevenZipFactory(), "7Zip.LZMA2.exe", new() { LookForHeader = true });

        [Fact]
        public Task SevenZipArchive_LZMA2AES_StreamRead() =>
            ArchiveStreamReadAsync("7Zip.LZMA2.Aes.7z", new ReaderOptions { Password = "testpassword" });

        [Fact]
        public Task SevenZipArchive_LZMA2AES_PathRead() =>
            ArchiveFileReadAsync("7Zip.LZMA2.Aes.7z", new ReaderOptions { Password = "testpassword" });

        [Fact]
        public Task SevenZipArchive_BZip2_StreamRead() => ArchiveStreamReadAsync("7Zip.BZip2.7z");

        [Fact]
        public Task SevenZipArchive_BZip2_PathRead() => ArchiveFileReadAsync("7Zip.BZip2.7z");

        [Fact]
        public Task SevenZipArchive_LZMA_Time_Attributes_PathRead() =>
            ArchiveFileReadExAsync("7Zip.LZMA.7z");

        [Fact]
        public Task SevenZipArchive_BZip2_Split() =>
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                                                         await ArchiveStreamReadAsync(
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
        public Task SevenZipArchive_BZip2_Split_Working() =>
            ArchiveStreamMultiReadAsync(
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
        public Task SevenZipArchive_BZip2_Split_FirstFileRead() =>
            ArchiveFileReadAsync("7Zip.BZip2.split.001");

        //"7Zip.BZip2.split.002",
        //"7Zip.BZip2.split.003",
        //"7Zip.BZip2.split.004",
        //"7Zip.BZip2.split.005",
        //"7Zip.BZip2.split.006",
        //"7Zip.BZip2.split.007"

        [Fact]
        public Task SevenZipArchive_ZSTD_StreamRead() => ArchiveStreamReadAsync("7Zip.ZSTD.7z");

        [Fact]
        public Task SevenZipArchive_ZSTD_PathRead() => ArchiveFileReadAsync("7Zip.ZSTD.7z");

        [Fact]
        public Task SevenZipArchive_ZSTD_Split() =>
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await ArchiveStreamReadAsync(
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
        public Task SevenZipArchive_EOS_FileRead() => ArchiveFileReadAsync("7Zip.eos.7z");

        [Fact]
        public Task SevenZipArchive_Delta_FileRead() => ArchiveFileReadAsync("7Zip.delta.7z");

        [Fact]
        public Task SevenZipArchive_ARM_FileRead() => ArchiveFileReadAsync("7Zip.ARM.7z");

        [Fact]
        public Task SevenZipArchive_ARMT_FileRead() => ArchiveFileReadAsync("7Zip.ARMT.7z");

        [Fact]
        public Task SevenZipArchive_BCJ_FileRead() => ArchiveFileReadAsync("7Zip.BCJ.7z");

        [Fact]
        public Task SevenZipArchive_BCJ2_FileRead() => ArchiveFileReadAsync("7Zip.BCJ2.7z");

        [Fact]
        public Task SevenZipArchive_IA64_FileRead() => ArchiveFileReadAsync("7Zip.IA64.7z");

        [Fact]
        public Task SevenZipArchive_PPC_FileRead() => ArchiveFileReadAsync("7Zip.PPC.7z");

        [Fact]
        public Task SevenZipArchive_SPARC_FileRead() => ArchiveFileReadAsync("7Zip.SPARC.7z");

        [Fact]
        public Task SevenZipArchive_ARM64_FileRead() => ArchiveFileReadAsync("7Zip.ARM64.7z");

        [Fact]
        public Task SevenZipArchive_RISCV_FileRead() => ArchiveFileReadAsync("7Zip.RISCV.7z");

        [Fact]
        public Task SevenZipArchive_Filters_FileRead() => ArchiveFileReadAsync("7Zip.Filters.7z");

        [Fact]
        public Task SevenZipArchive_Delta_Distance() =>
            ArchiveDeltaDistanceReadAsync("7Zip.delta.distance.7z");

        [Fact]
        public async Task SevenZipArchive_Tar_PathRead()
        {
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.Tar.tar.7z")))
            using (var archive = SevenZipArchive.Open(stream))
            {
                var entry = archive.Entries.First();
                await entry.WriteToFileAsync(Path.Combine(SCRATCH_FILES_PATH, entry.Key.NotNull()));

                var size = entry.Size;
                var scratch = new FileInfo(Path.Combine(SCRATCH_FILES_PATH, "7Zip.Tar.tar"));
                var test = new FileInfo(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.Tar.tar"));

                Assert.Equal(size, scratch.Length);
                Assert.Equal(size, test.Length);
            }

            await CompareArchivesByPathAsync(
                Path.Combine(SCRATCH_FILES_PATH, "7Zip.Tar.tar"),
                Path.Combine(TEST_ARCHIVES_PATH, "7Zip.Tar.tar")
            );
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
