using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Archive;
using SharpCompress.Archive.GZip;

namespace SharpCompress.Test
{
    [TestClass]
    public class GZipArchiveTests : ArchiveTests
    {
        public GZipArchiveTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [TestMethod]
        public void GZip_Archive_Generic()
        {
            ResetScratch();
            using (Stream stream = File.Open(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"), FileMode.Open))
            using (var archive = ArchiveFactory.Open(stream))
            {
                var entry = archive.Entries.First();
                entry.WriteToFile(Path.Combine(SCRATCH_FILES_PATH, entry.Key));
            }
            CompareArchivesByPath(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"),
                Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        }

        [TestMethod]
        public void GZip_Archive()
        {
            ResetScratch();
            using (Stream stream = File.Open(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"), FileMode.Open))
            using (var archive = GZipArchive.Open(stream))
            {
                var entry = archive.Entries.First();
                entry.WriteToFile(Path.Combine(SCRATCH_FILES_PATH, entry.Key));
            }
            CompareArchivesByPath(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar"),
                Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar"));
        }


        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GZip_Archive_NoAdd()
        {
            string jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg\\test.jpg");
            ResetScratch();
            using (Stream stream = File.Open(Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz"), FileMode.Open))
            using (var archive = GZipArchive.Open(stream))
            {
                archive.AddEntry("jpg\\test.jpg", jpg);
                archive.SaveTo(Path.Combine(SCRATCH_FILES_PATH, "Tar.tar.gz"));
            }
        }
    }
}
