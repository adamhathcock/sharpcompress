using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Common;
using SharpCompress.Reader;
using SharpCompress.Reader.Rar;

namespace SharpCompress.Test
{
    [TestClass]
    public class RarReaderTests : ReaderTests
    {
        [TestMethod]
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
                    reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                }
            }
            VerifyFiles();
        }

        [TestMethod]
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

           using (var reader = RarReader.Open(testArchives.Select(s => Path.Combine(SCRATCH2_FILES_PATH, s))
               .Select(p => File.OpenRead(p)), Options.None))
           {
              while (reader.MoveToNextEntry())
              {
                 reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
              }
           }
           VerifyFiles();

           foreach (var file in testArchives.Select(s => Path.Combine(SCRATCH2_FILES_PATH, s)))
           {
              File.Delete(file);
           }
        }

        [TestMethod]
        public void Rar_None_Reader()
        {
            Read("Rar.none.rar", CompressionType.Rar);
        }

        [TestMethod]
        public void Rar_Reader()
        {
            Read("Rar.rar", CompressionType.Rar);
        }

        [TestMethod]
        public void Rar_Encrypted_Reader()
        {
            Read("Rar.Encrypted.rar", CompressionType.Rar);
        }

        [TestMethod]
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
                        Assert.AreEqual(reader.Entry.CompressionType, CompressionType.Rar);
                        using (var entryStream = reader.OpenEntryStream())
                        {
                            string file = Path.GetFileName(reader.Entry.FilePath);
                            string folder = Path.GetDirectoryName(reader.Entry.FilePath);
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

        [TestMethod]
        public void Rar_Reader_Audio_program()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Audio_program.rar")))
            using (var reader = RarReader.Open(stream, Options.LookForHeader))
            {
                while (reader.MoveToNextEntry())
                {
                    Assert.AreEqual(reader.Entry.CompressionType, CompressionType.Rar);
                    reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                }
            }
            CompareFilesByPath(Path.Combine(SCRATCH_FILES_PATH, "test.dat"),
                Path.Combine(MISC_TEST_FILES_PATH, "test.dat"));
        }

        [TestMethod]
        public void Rar_Jpg_Reader()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "RarJpeg.jpg")))
            using (var reader = RarReader.Open(stream, Options.LookForHeader))
            {
                while (reader.MoveToNextEntry())
                {
                    Assert.AreEqual(reader.Entry.CompressionType, CompressionType.Rar);
                    reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                }
            }
            VerifyFiles();
        }

        [TestMethod]
        public void Rar_Solid_Reader()
        {
            Read("Rar.solid.rar", CompressionType.Rar);
        }

        [TestMethod]
        public void Rar_Solid_Skip_Reader()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.solid.rar")))
            using (var reader = RarReader.Open(stream, Options.LookForHeader))
            {
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.FilePath.Contains("jpg"))
                    {
                        Assert.AreEqual(reader.Entry.CompressionType, CompressionType.Rar);
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                }
            }
        }

        [TestMethod]
        public void Rar_Reader_Skip()
        {
            ResetScratch();
            using (var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Rar.rar")))
            using (var reader = RarReader.Open(stream, Options.LookForHeader))
            {
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.FilePath.Contains("jpg"))
                    {
                        Assert.AreEqual(reader.Entry.CompressionType, CompressionType.Rar);
                        reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                }
            }
        }
    }
}
