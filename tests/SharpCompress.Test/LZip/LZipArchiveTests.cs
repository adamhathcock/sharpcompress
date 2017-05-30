using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.LZip;
using SharpCompress.Common.LZip;
using Xunit;

namespace SharpCompress.Test
{
    public class LZipArchiveTests : ArchiveTests
    {
        public LZipArchiveTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [Fact]
        public void LZip_Archive_Generic()
        {
            ResetScratch();
            using (Stream stream = File.Open(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.lz"), FileMode.Open))
            using (var archive = ArchiveFactory.Open(stream))
            {
                var entry = archive.Entries.First();
                entry.WriteToFile(Path.Combine(SCRATCH_FILES_PATH, entry.Key));
            }
            CompareArchivesByPath(Path.Combine(SCRATCH_FILES_PATH, LZipEntry.LZIP_FILE_NAME),
                Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        }

        [Fact]
        public void LZip_Archive()
        {
            ResetScratch();
            using (Stream stream = File.Open(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.lz"), FileMode.Open))
            using (var archive = LZipArchive.Open(stream))
            {
                var entry = archive.Entries.First();
                entry.WriteToFile(Path.Combine(SCRATCH_FILES_PATH, entry.Key));
            }
            CompareArchivesByPath(Path.Combine(SCRATCH_FILES_PATH, LZipEntry.LZIP_FILE_NAME),
                Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        }


        [Fact]
        public void LZip_Archive_NoAdd()
        {
            string jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
            ResetScratch();
            using (Stream stream = File.Open(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.lz"), FileMode.Open))
            using (var archive = LZipArchive.Open(stream))
            {
                Assert.Throws<InvalidOperationException>(() => archive.AddEntry("jpg\\test.jpg", jpg));
                archive.SaveTo(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.lz"));
            }
        }


        [Fact]
        public void LZip_Archive_Multiple_Reads()
        {
            ResetScratch();
            var inputStream = new MemoryStream();
            using (var fileStream = File.Open(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.lz"), FileMode.Open))
            {
                fileStream.CopyTo(inputStream);
                inputStream.Position = 0;
            }
            using (var archive = LZipArchive.Open(inputStream))
            {
                var archiveEntry = archive.Entries.First();

                MemoryStream tarStream;
                using (var entryStream = archiveEntry.OpenEntryStream())
                {
                    tarStream = new MemoryStream();
                    entryStream.CopyTo(tarStream);
                }
                var size = tarStream.Length;
                using (var entryStream = archiveEntry.OpenEntryStream())
                {
                    tarStream = new MemoryStream();
                    entryStream.CopyTo(tarStream);
                }
                Assert.Equal(size, tarStream.Length);
                using (var entryStream = archiveEntry.OpenEntryStream())
                {
                    var result = Archives.Tar.TarArchive.IsTarFile(entryStream);
                }
                Assert.Equal(size, tarStream.Length);
                using (var entryStream = archiveEntry.OpenEntryStream())
                {
                    tarStream = new MemoryStream();
                    entryStream.CopyTo(tarStream);
                }
                Assert.Equal(size, tarStream.Length);
            }
        }
    }
}
