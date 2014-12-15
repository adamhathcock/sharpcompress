
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
                Assert.AreEqual(archive.Entries.Last().Key, @"very long filename/very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename.jpg");
            }
        }

        [TestMethod]
        public void Tar_UstarArchivePathReadLongName()
        {
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "ustar with long names.tar");
            using(var archive = TarArchive.Open(unmodified))
            {
                Assert.AreEqual(6, archive.Entries.Count);
                Assert.IsTrue(archive.Entries.Any(entry => entry.Key == "Directory/"));
                Assert.IsTrue(archive.Entries.Any(entry => entry.Key == "Directory/Some file with veeeeeeeeeery loooooooooong name"));
                Assert.IsTrue(archive.Entries.Any(entry => entry.Key == "Directory/Directory with veeeeeeeeeery loooooooooong name/"));
                Assert.IsTrue(archive.Entries.Any(entry => entry.Key == "Directory/Directory with veeeeeeeeeery loooooooooong name/Some file with veeeeeeeeeery loooooooooong name"));
                Assert.IsTrue(archive.Entries.Any(entry => entry.Key == "Directory/Directory with veeeeeeeeeery loooooooooong name/Directory with veeeeeeeeeery loooooooooong name/"));
                Assert.IsTrue(archive.Entries.Any(entry => entry.Key == "Directory/Directory with veeeeeeeeeery loooooooooong name/Directory with veeeeeeeeeery loooooooooong name/Some file with veeeeeeeeeery loooooooooong name"));
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
                var entry = archive.Entries.Where(x => x.Key.EndsWith("jpg")).Single();
                archive.RemoveEntry(entry);
                archive.SaveTo(scratchPath, CompressionType.None);
            }
            CompareArchivesByPath(modified, scratchPath);
        }
    }
}
