using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.Rar
{
    public class RarArchiveTests : ArchiveTests
    {
        [Fact]
        public void Rar_EncryptedFileAndHeader_Archive()
        {
            ReadRarPassword("Rar.encrypted_filesAndHeader.rar", "test");

        }

        [Fact]
        public void Rar_EncryptedFileOnly_Archive()
        {
            ReadRarPassword("Rar.encrypted_filesOnly.rar", "test");

        }

        [Fact]
        public void Rar_Encrypted_Archive()
        {
            ReadRarPassword("Encrypted.rar", "test");
        }

        private void ReadRarPassword(string testArchive, string password)
        {
            ResetScratch();
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, testArchive)))
            using (var archive = RarArchive.Open(stream, new ReaderOptions()
                                                         {
                                                             Password = password,
                                                             LeaveStreamOpen = true
                                                         }))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.Rar, entry.CompressionType);
                        entry.WriteToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void Rar_Multi_Archive_Encrypted()
        {
            Assert.Throws<InvalidFormatException>(() => ArchiveFileReadPassword("EncryptedParts.part01.rar", "test"));
        }

        protected void ArchiveFileReadPassword(string archiveName, string password)
        {
            ResetScratch();
            using (var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, archiveName), new ReaderOptions()
                                                            {
                                                                Password = password,
                                                                LeaveStreamOpen = true
                                                            }))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    entry.WriteToDirectory(SCRATCH_FILES_PATH,
                                            new ExtractionOptions()
                                            {
                                                ExtractFullPath = true,
                                                Overwrite = true
                                            });
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void Rar_None_ArchiveStreamRead()
        {
            ArchiveStreamRead("Rar.none.rar");
        }

        [Fact]
        public void Rar_ArchiveStreamRead()
        {
            ArchiveStreamRead("Rar.rar");
        }

        [Fact]
        public void Rar_test_invalid_exttime_ArchiveStreamRead()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "test_invalid_exttime.rar")))
            {
                using (var archive = ArchiveFactory.Open(stream))
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            }
        }

        [Fact]
        public void Rar_Jpg_ArchiveStreamRead()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "RarJpeg.jpg")))
            {
                using (var archive = RarArchive.Open(stream, new ReaderOptions()
                                                             {
                                                                 LookForHeader = true
                                                             }))
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH,
                                               new ExtractionOptions()
                                               {
                                                   ExtractFullPath = true,
                                                   Overwrite = true
                                               });
                    }
                }
                VerifyFiles();
            }
        }

        [Fact]
        public void Rar_IsSolidArchiveCheck()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.rar")))
            {
                using (var archive = RarArchive.Open(stream))
                {
                    Assert.False(archive.IsSolid);
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {
                        entry.WriteToDirectory(SCRATCH_FILES_PATH,
                                               new ExtractionOptions()
                                               {
                                                   ExtractFullPath = true,
                                                   Overwrite = true
                                               });
                    }
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void Rar_Solid_ArchiveStreamRead()
        {
            Assert.Throws<InvalidFormatException>(() => ArchiveStreamRead("Rar.solid.rar"));
        }

        [Fact]
        public void Rar_Solid_StreamRead_Extract_All()
        {
            ArchiveStreamReadExtractAll("Rar.solid.rar", CompressionType.Rar);
        }

        [Fact]
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
                .Select(File.OpenRead)))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    entry.WriteToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void RarNoneArchiveFileRead()
        {
            ArchiveFileRead("Rar.none.rar");
        }

        [Fact]
        public void Rar_ArchiveFileRead()
        {
            ArchiveFileRead("Rar.rar");
        }

        [Fact]
        public void Rar_ArchiveFileRead_HasDirectories()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.rar")))
            {
                using (var archive = RarArchive.Open(stream))
                {
                    Assert.False(archive.IsSolid);
                    Assert.True(archive.Entries.Any(entry => entry.IsDirectory));
                }
            }
        }

        [Fact]
        public void Rar_Jpg_ArchiveFileRead()
        {
            ResetScratch();
            using (var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "RarJpeg.jpg"), new ReaderOptions()
                                                                                                  {
                                                                                                      LookForHeader = true
                                                                                                  }))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    entry.WriteToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void Rar_Solid_ArchiveFileRead()
        {
            Assert.Throws<InvalidFormatException>(() => ArchiveFileRead("Rar.solid.rar"));
        }

        [Fact]
        public void Rar_Multi_ArchiveFileRead()
        {
            ArchiveFileRead("Rar.multi.part01.rar");
        }

        [Fact]
        public void Rar_IsFirstVolume_True()
        {
            using (var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "Rar.multi.part01.rar")))
            {
                Assert.True(archive.IsMultipartVolume());
                Assert.True(archive.IsFirstVolume());
            }
        }

        [Fact]
        public void Rar_IsFirstVolume_False()
        {
            using (var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "Rar.multi.part03.rar")))
            {
                Assert.True(archive.IsMultipartVolume());
                Assert.False(archive.IsFirstVolume());
            }
        }
    }
}
