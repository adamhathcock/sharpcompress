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

        /*[Fact]
        public void Rar5_EncryptedFileAndHeader_Archive()
        {
            ReadRarPassword("Rar5.encrypted_filesAndHeader.rar", "test");
        }*/

        [Fact]
        public void Rar_EncryptedFileOnly_Archive()
        {
            ReadRarPassword("Rar.encrypted_filesOnly.rar", "test");
        }

        /*[Fact]
        public void Rar5_EncryptedFileOnly_Archive()
        {
            ReadRarPassword("Rar5.encrypted_filesOnly.rar", "test");
        }*/

        [Fact]
        public void Rar_Encrypted_Archive()
        {
            ReadRarPassword("Rar.Encrypted.rar", "test");
        }

        /*[Fact]
        public void Rar5_Encrypted_Archive()
        {
            ReadRarPassword("Rar5.encrypted_filesAndHeader.rar", "test");
        }*/

        private void ReadRarPassword(string testArchive, string password)
        {
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
            Assert.Throws<InvalidFormatException>(() => ArchiveFileReadPassword("Rar.EncryptedParts.part01.rar", "test"));
        }

        protected void ArchiveFileReadPassword(string archiveName, string password)
        {
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
        public void Rar5_None_ArchiveStreamRead()
        {
            ArchiveStreamRead("Rar5.none.rar");
        }

        [Fact]
        public void Rar_ArchiveStreamRead()
        {
            ArchiveStreamRead("Rar.rar");
        }

        [Fact]
        public void Rar5_ArchiveStreamRead()
        {
            ArchiveStreamRead("Rar5.rar");
        }

        [Fact]
        public void Rar_test_invalid_exttime_ArchiveStreamRead()
        {
            DoRar_test_invalid_exttime_ArchiveStreamRead("Rar.test_invalid_exttime.rar");
        }

        private void DoRar_test_invalid_exttime_ArchiveStreamRead(string filename)
        {
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename)))
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
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.jpeg.jpg")))
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
            DoRar_IsSolidArchiveCheck("Rar.rar");
        }

        [Fact]
        public void Rar5_IsSolidArchiveCheck()
        {
            DoRar_IsSolidArchiveCheck("Rar5.rar");
        }

        private void DoRar_IsSolidArchiveCheck(string filename)
        {
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename)))
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
            ArchiveStreamRead("Rar.solid.rar");
        }

        [Fact]
        public void Rar5_Solid_ArchiveStreamRead()
        {
            ArchiveStreamRead("Rar5.solid.rar");
        }

        [Fact]
        public void Rar_Solid_StreamRead_Extract_All()
        {
            ArchiveStreamReadExtractAll("Rar.solid.rar", CompressionType.Rar);
        }

        [Fact]
        public void Rar5_Solid_StreamRead_Extract_All()
        {
            ArchiveStreamReadExtractAll("Rar5.solid.rar", CompressionType.Rar);
        }

        [Fact]
        public void Rar_Multi_ArchiveStreamRead()
        {
            DoRar_Multi_ArchiveStreamRead(new string[] {
                "Rar.multi.part01.rar",
                "Rar.multi.part02.rar",
                "Rar.multi.part03.rar",
                "Rar.multi.part04.rar",
                "Rar.multi.part05.rar",
                "Rar.multi.part06.rar"}, false);
        }

        [Fact]
        public void Rar5_Multi_ArchiveStreamRead()
        {
            DoRar_Multi_ArchiveStreamRead(new string[] {
                "Rar5.multi.part01.rar",
                "Rar5.multi.part02.rar",
                "Rar5.multi.part03.rar",
                "Rar5.multi.part04.rar",
                "Rar5.multi.part05.rar",
                "Rar5.multi.part06.rar"}, false);
        }

        private void DoRar_Multi_ArchiveStreamRead(string[] archives, bool isSolid)
        {
            using (var archive = RarArchive.Open(archives.Select(s => Path.Combine(TEST_ARCHIVES_PATH, s))
                .Select(File.OpenRead)))
            {
                Assert.Equal(archive.IsSolid, isSolid);
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

        [Fact]
        public void Rar5_MultiSolid_ArchiveStreamRead()
        {
            DoRar_Multi_ArchiveStreamRead(new string[] {
                "Rar.multi.solid.part01.rar",
                "Rar.multi.solid.part02.rar",
                "Rar.multi.solid.part03.rar",
                "Rar.multi.solid.part04.rar",
                "Rar.multi.solid.part05.rar",
                "Rar.multi.solid.part06.rar"}, true);
        }

        [Fact]
        public void RarNoneArchiveFileRead()
        {
            ArchiveFileRead("Rar.none.rar");
        }

        [Fact]
        public void Rar5NoneArchiveFileRead()
        {
            ArchiveFileRead("Rar5.none.rar");
        }

        [Fact]
        public void Rar_ArchiveFileRead()
        {
            ArchiveFileRead("Rar.rar");
        }

        [Fact]
        public void Rar5_ArchiveFileRead()
        {
            ArchiveFileRead("Rar5.rar");
        }

        [Fact]
        public void Rar_ArchiveFileRead_HasDirectories()
        {
            DoRar_ArchiveFileRead_HasDirectories("Rar.rar");
        }

        [Fact]
        public void Rar5_ArchiveFileRead_HasDirectories()
        {
            DoRar_ArchiveFileRead_HasDirectories("Rar5.rar");
        }

        private void DoRar_ArchiveFileRead_HasDirectories(string filename)
        {
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, filename)))
            {
                using (var archive = RarArchive.Open(stream))
                {
                    Assert.False(archive.IsSolid);
                    Assert.Contains(true, archive.Entries.Select(entry => entry.IsDirectory));
                }
            }
        }

        [Fact]
        public void Rar_Jpg_ArchiveFileRead()
        {
            using (var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "Rar.jpeg.jpg"), new ReaderOptions()
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
            ArchiveFileRead("Rar.solid.rar");
        }

        [Fact]
        public void Rar5_Solid_ArchiveFileRead()
        {
            ArchiveFileRead("Rar5.solid.rar");
        }

        [Fact]
        public void Rar_Multi_ArchiveFileRead()
        {
            ArchiveFileRead("Rar.multi.part01.rar");
        }

        [Fact]
        public void Rar5_Multi_ArchiveFileRead()
        {
            ArchiveFileRead("Rar5.multi.part01.rar");
        }

        [Fact]
        public void Rar_IsFirstVolume_True()
        {
            DoRar_IsFirstVolume_True("Rar.multi.part01.rar");
        }

        [Fact]
        public void Rar5_IsFirstVolume_True()
        {
            DoRar_IsFirstVolume_True("Rar5.multi.part01.rar");
        }

        private void DoRar_IsFirstVolume_True(string firstFilename)
        {
            using (var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, firstFilename)))
            {
                Assert.True(archive.IsMultipartVolume());
                Assert.True(archive.IsFirstVolume());
            }
        }

        [Fact]
        public void Rar_IsFirstVolume_False()
        {
            DoRar_IsFirstVolume_False("Rar.multi.part03.rar");
        }

        [Fact]
        public void Rar5_IsFirstVolume_False()
        {
            DoRar_IsFirstVolume_False("Rar5.multi.part03.rar");
        }

        private void DoRar_IsFirstVolume_False(string notFirstFilename)
        {
            using (var archive = RarArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, notFirstFilename)))
            {
                Assert.True(archive.IsMultipartVolume());
                Assert.False(archive.IsFirstVolume());
            }
        }
    }
}
