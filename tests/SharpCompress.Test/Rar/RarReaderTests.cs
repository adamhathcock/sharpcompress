using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using Xunit;

namespace SharpCompress.Test.Rar
{
    public class RarReaderTests : ReaderTests
    {
        [Fact]
        public void Rar_Multi_Reader()
        {
            var testArchives = new string[] { "Rar.multi.part01.rar",
                "Rar.multi.part02.rar",
                "Rar.multi.part03.rar",
                "Rar.multi.part04.rar",
                "Rar.multi.part05.rar",
                "Rar.multi.part06.rar"};


            ResetScratch();
            using (var reader = RarReader.Open(testArchives.Select(s => Path.Combine(TEST_ARCHIVES_PATH, s))
                .Select(p => File.OpenRead(p))))
            {
                while (reader.MoveToNextEntry())
                {
                    reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void Rar_Multi_Reader_Encrypted()
        {
            var testArchives = new string[] { "EncryptedParts.part01.rar",
                "EncryptedParts.part02.rar",
                "EncryptedParts.part03.rar",
                "EncryptedParts.part04.rar",
                "EncryptedParts.part05.rar",
                "EncryptedParts.part06.rar"};


            Assert.Throws<InvalidFormatException>(() =>
                                                  {
                                                      ResetScratch();
                                                      using (var reader = RarReader.Open(testArchives.Select(s => Path.Combine(TEST_ARCHIVES_PATH, s))
                                                                                                     .Select(p => File.OpenRead(p)),
                                                                                         new ReaderOptions()
                                                                                         {
                                                                                             Password = "test"
                                                                                         }))
                                                      {
                                                          while (reader.MoveToNextEntry())
                                                          {
                                                              reader.WriteEntryToDirectory(SCRATCH_FILES_PATH,
                                                                                           new ExtractionOptions()
                                                                                           {
                                                                                               ExtractFullPath = true,
                                                                                               Overwrite = true
                                                                                           });
                                                          }
                                                      }
                                                      VerifyFiles();
                                                  });
        }

        [Fact]
        public void Rar_Multi_Reader_Delete_Files()
        {
            var testArchives = new string[] { "Rar.multi.part01.rar",
                "Rar.multi.part02.rar",
                "Rar.multi.part03.rar",
                "Rar.multi.part04.rar",
                "Rar.multi.part05.rar",
                "Rar.multi.part06.rar"};


            ResetScratch();

            foreach (var file in testArchives)
            {
                File.Copy(Path.Combine(TEST_ARCHIVES_PATH, file), Path.Combine(SCRATCH2_FILES_PATH, file));
            }
            var streams = testArchives.Select(s => Path.Combine(SCRATCH2_FILES_PATH, s)).Select(File.OpenRead).ToList();
            using (var reader = RarReader.Open(streams))
            {
                while (reader.MoveToNextEntry())
                {
                    reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            foreach (var stream in streams)
            {
                stream.Dispose();
            }
            VerifyFiles();

            foreach (var file in testArchives.Select(s => Path.Combine(SCRATCH2_FILES_PATH, s)))
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void Rar_None_Reader()
        {
            Read("Rar.none.rar", CompressionType.Rar);
        }

        [Fact]
        public void Rar_Reader()
        {
            Read("Rar.rar", CompressionType.Rar);
        }

        [Fact]
        public void Rar_EncryptedFileAndHeader_Reader()
        {
            ReadRar("Rar.encrypted_filesAndHeader.rar", "test");

        }

        [Fact]
        public void Rar_EncryptedFileOnly_Reader()
        {
            ReadRar("Rar.encrypted_filesOnly.rar", "test");

        }

        [Fact]
        public void Rar_Encrypted_Reader()
        {
            ReadRar("Encrypted.rar", "test");
        }

        private void ReadRar(string testArchive, string password)
        {
            ResetScratch();
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, testArchive)))
            using (var reader = RarReader.Open(stream, new ReaderOptions()
                                                       {
                                                           Password = password
                                                       }))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
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
        public void Rar_Entry_Stream()
        {
            ResetScratch();
            using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.rar")))
            using (var reader = RarReader.Open(stream))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                        using (var entryStream = reader.OpenEntryStream())
                        {
                            string file = Path.GetFileName(reader.Entry.Key);
                            string folder = Path.GetDirectoryName(reader.Entry.Key);
                            string destdir = Path.Combine(SCRATCH_FILES_PATH, folder);
                            if (!Directory.Exists(destdir))
                            {
                                Directory.CreateDirectory(destdir);
                            }
                            string destinationFileName = Path.Combine(destdir, file);

                            using (FileStream fs = File.OpenWrite(destinationFileName))
                            {
                                entryStream.TransferTo(fs);
                            }
                        }
                    }
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void Rar_Reader_Audio_program()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Audio_program.rar")))
            using (var reader = RarReader.Open(stream, new ReaderOptions()
                                                       {
                                                           LookForHeader = true
                                                       }))
            {
                while (reader.MoveToNextEntry())
                {
                    Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                    reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            CompareFilesByPath(Path.Combine(SCRATCH_FILES_PATH, "test.dat"),
                Path.Combine(MISC_TEST_FILES_PATH, "test.dat"));
        }

        [Fact]
        public void Rar_Jpg_Reader()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "RarJpeg.jpg")))
            using (var reader = RarReader.Open(stream, new ReaderOptions()
                                                        {
                                                            LookForHeader = true
                                                        }))
            {
                while (reader.MoveToNextEntry())
                {
                    Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                    reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            VerifyFiles();
        }

        [Fact]
        public void Rar_Solid_Reader()
        {
            Read("Rar.solid.rar", CompressionType.Rar);
        }

        [Fact]
        public void Rar_Solid_Skip_Reader()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.solid.rar")))
            using (var reader = RarReader.Open(stream, new ReaderOptions()
                                                        {
                                                            LookForHeader = true
                                                        }))
            {
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.Key.Contains("jpg"))
                    {
                        Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            }
        }

        [Fact]
        public void Rar_Reader_Skip()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.rar")))
            using (var reader = RarReader.Open(stream, new ReaderOptions()
                                                            {
                                                                LookForHeader = true
                                                            }))
            {
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.Key.Contains("jpg"))
                    {
                        Assert.Equal(CompressionType.Rar, reader.Entry.CompressionType);
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            }
        }
    }
}
