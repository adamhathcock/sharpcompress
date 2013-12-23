using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Archive;
using SharpCompress.Archive.Rar;
using SharpCompress.Common;

namespace SharpCompress.Test
{
    [TestClass]
    public class RarArchiveTests : ArchiveTests
    {
        [TestMethod]
        public void Rar_None_ArchiveStreamRead()
        {
            ArchiveStreamRead("Rar.none.rar");
        }

        [TestMethod]
        public void Rar_ArchiveStreamRead()
        {
            ArchiveStreamRead("Rar.rar");
        }


        [TestMethod]
        public void Rar_test_invalid_exttime_ArchiveStreamRead()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "test_invalid_exttime.rar")))
            {
                using (var archive = ArchiveFactory.Open(stream))
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                }
            }
        }

        [TestMethod]
        public void Rar_Jpg_ArchiveStreamRead()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "RarJpeg.jpg")))
            {
                using (var archive = RarArchive.Open(stream, Options.LookForHeader))
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH,
                                               ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                }
                VerifyFiles();
            }
        }

        [TestMethod]
        public void Rar_IsSolidArchiveCheck()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.rar")))
            {
                using (var archive = RarArchive.Open(stream))
                {
                    Assert.IsFalse(archive.IsSolid);
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH,
                                               ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                }
            }
            VerifyFiles();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidFormatException))]
        public void Rar_Solid_ArchiveStreamRead()
        {
            ArchiveStreamRead("Rar.solid.rar");
        }

        [TestMethod]
        public void Rar_Solid_StreamRead_Extract_All()
        {
            ArchiveStreamReadExtractAll("Rar.solid.rar", CompressionType.Rar);
        }

        [TestMethod]
        public void Rar_Multi_ArchiveStreamRead()
        {
            var testArchives = new string[] { "Rar.multi.part01.rar",
                "Rar.multi.part02.rar",
                "Rar.multi.part03.rar",
                "Rar.multi.part04.rar",
                "Rar.multi.part05.rar",
                "Rar.multi.part06.rar"};


            ResetScratch();
            using (var archive = RarArchive.Open(testArchives.Select(s => Path.Combine(TEST_ARCHIVES_PATH, s))
                .Select(p => File.OpenRead(p))))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    entry.WriteToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                }
            }
            VerifyFiles();
        }

        [TestMethod]
        public void RarNoneArchiveFileRead()
        {
            ArchiveFileRead("Rar.none.rar");
        }

        [TestMethod]
        public void Rar_ArchiveFileRead()
        {
            ArchiveFileRead("Rar.rar");
        }

        [TestMethod]
        public void Rar_ArchiveFileRead_HasDirectories()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.rar")))
            {
                using (var archive = RarArchive.Open(stream))
                {
                    Assert.IsFalse(archive.IsSolid);
                    Assert.IsTrue(archive.Entries.Any(entry => entry.IsDirectory));
                }
            }
        }

        [TestMethod]
        public void Rar_Jpg_ArchiveFileRead()
        {
            ResetScratch();
            using (var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "RarJpeg.jpg"), Options.LookForHeader))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    entry.WriteToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                }
            }
            VerifyFiles();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidFormatException))]
        public void Rar_Solid_ArchiveFileRead()
        {
            ArchiveFileRead("Rar.solid.rar");
        }

        [TestMethod]
        public void Rar_Multi_ArchiveFileRead()
        {
            ArchiveFileRead("Rar.multi.part01.rar");
        }

        [TestMethod]
        public void Rar_IsFirstVolume_True()
        {
            using (var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "Rar.multi.part01.rar")))
            {
                Assert.IsTrue(archive.IsMultipartVolume());
                Assert.IsTrue(archive.IsFirstVolume());
            }
        }

        [TestMethod]
        public void Rar_IsFirstVolume_False()
        {
            using (var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "Rar.multi.part03.rar")))
            {
                Assert.IsTrue(archive.IsMultipartVolume());
                Assert.IsFalse(archive.IsFirstVolume());
            }
        }
    }
}
