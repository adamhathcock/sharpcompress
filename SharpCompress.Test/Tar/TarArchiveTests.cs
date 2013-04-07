
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Archive;
using SharpCompress.Archive.Tar;
using SharpCompress.Common;

namespace SharpCompress.Test
{
    [TestClass]
    public class TarArchiveTests : ArchiveTests
    {
        public TarArchiveTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [TestMethod]
        public void TarArchiveStreamRead()
        {
            ArchiveStreamRead("Tar.tar");
        }

        [TestMethod]
        public void TarArchivePathRead()
        {
            ArchiveFileRead("Tar.tar");
        }


        [TestMethod]
        public void TarArchivePathReadLongName()
        {
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "very long filename.tar");
            using (var archive = TarArchive.Open(unmodified))
            {
                Assert.AreEqual(2, archive.Entries.Count);
                Assert.AreEqual(archive.Entries.Last().FilePath, @"very long filename/very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename.jpg");
            }
        }

        [TestMethod]
        public void Tar_Create_New()
        {
            string scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.tar");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

            base.ResetScratch();
            using (var archive = TarArchive.Create())
            {
                archive.AddAllFromDirectory(ORIGINAL_FILES_PATH);
                archive.SaveTo(scratchPath, CompressionType.None);
            }
            CompareArchivesByPath(unmodified, scratchPath);
        }
        [TestMethod]
        public void Tar_Random_Write_Add()
        {
            string jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg\\test.jpg");
            string scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.mod.tar");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.mod.tar");
            string modified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

            base.ResetScratch();
            using (var archive = TarArchive.Open(unmodified))
            {
                archive.AddEntry("jpg\\test.jpg", jpg);
                archive.SaveTo(scratchPath, CompressionType.None);
            }
            CompareArchivesByPath(modified, scratchPath);
        }

        [TestMethod]
        public void Tar_Random_Write_Remove()
        {
            string scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.mod.tar");
            string modified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.mod.tar");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

            base.ResetScratch();
            using (var archive = TarArchive.Open(unmodified))
            {
                var entry = archive.Entries.Where(x => x.FilePath.EndsWith("jpg")).Single();
                archive.RemoveEntry(entry);
                archive.SaveTo(scratchPath, CompressionType.None);
            }
            CompareArchivesByPath(modified, scratchPath);
        }
    }
}
