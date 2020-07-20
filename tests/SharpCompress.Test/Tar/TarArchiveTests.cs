using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Writers;
using Xunit;
using System.Text;
using SharpCompress.Readers;
using SharpCompress.Writers.Tar;
using SharpCompress.Readers.Tar;

namespace SharpCompress.Test.Tar
{
    public class TarArchiveTests : ArchiveTests
    {
        public TarArchiveTests()
        {
            UseExtensionInsteadOfNameToVerify = true;
        }

        [Fact]
        public void TarArchiveStreamRead()
        {
            ArchiveStreamRead("Tar.tar");
        }

        [Fact]
        public void TarArchivePathRead()
        {
            ArchiveFileRead("Tar.tar");
        }

        [Fact]
        public void Tar_FileName_Exactly_100_Characters()
        {
            string archive = "Tar_FileName_Exactly_100_Characters.tar";


            // create the 100 char filename
            string filename = "filename_with_exactly_100_characters_______________________________________________________________X";

            // Step 1: create a tar file containing a file with the test name
            using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, CompressionType.None))
            using (Stream inputStream = new MemoryStream())
            {
                StreamWriter sw = new StreamWriter(inputStream);
                sw.Write("dummy filecontent");
                sw.Flush();

                inputStream.Position = 0;
                writer.Write(filename, inputStream, null);
            }

            // Step 2: check if the written tar file can be read correctly
            string unmodified = Path.Combine(SCRATCH2_FILES_PATH, archive);
            using (var archive2 = TarArchive.Open(unmodified))
            {
                Assert.Equal(1, archive2.Entries.Count);
                Assert.Contains(filename, archive2.Entries.Select(entry => entry.Key));

                foreach (var entry in archive2.Entries)
                {
                    Assert.Equal("dummy filecontent", new StreamReader(entry.OpenEntryStream()).ReadLine());
                }
            }
        }

        [Fact]
        public void Tar_NonUstarArchiveWithLongNameDoesNotSkipEntriesAfterTheLongOne()
        {
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "very long filename.tar");
            using (var archive = TarArchive.Open(unmodified))
            {
                Assert.Equal(5, archive.Entries.Count);
                Assert.Contains("very long filename/", archive.Entries.Select(entry => entry.Key));
                Assert.Contains("very long filename/very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename.jpg", archive.Entries.Select(entry => entry.Key));
                Assert.Contains("z_file 1.txt", archive.Entries.Select(entry => entry.Key));
                Assert.Contains("z_file 2.txt", archive.Entries.Select(entry => entry.Key));
                Assert.Contains("z_file 3.txt", archive.Entries.Select(entry => entry.Key));
            }
        }

        [Fact]
        public void Tar_VeryLongFilepathReadback()
        {
            string archive = "Tar_VeryLongFilepathReadback.tar";


            // create a very long filename
            string longFilename = "";
            for (int i = 0; i < 600; i = longFilename.Length)
            {
                longFilename += i.ToString("D10") + "-";
            }

            longFilename += ".txt";

            // Step 1: create a tar file containing a file with a long name
            using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, CompressionType.None))
            using (Stream inputStream = new MemoryStream())
            {
                StreamWriter sw = new StreamWriter(inputStream);
                sw.Write("dummy filecontent");
                sw.Flush();

                inputStream.Position = 0;
                writer.Write(longFilename, inputStream, null);
            }

            // Step 2: check if the written tar file can be read correctly
            string unmodified = Path.Combine(SCRATCH2_FILES_PATH, archive);
            using (var archive2 = TarArchive.Open(unmodified))
            {
                Assert.Equal(1, archive2.Entries.Count);
                Assert.Contains(longFilename, archive2.Entries.Select(entry => entry.Key));

                foreach (var entry in archive2.Entries)
                {
                    Assert.Equal("dummy filecontent", new StreamReader(entry.OpenEntryStream()).ReadLine());
                }
            }
        }

        [Fact]
        public void Tar_UstarArchivePathReadLongName()
        {
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "ustar with long names.tar");
            using (var archive = TarArchive.Open(unmodified))
            {
                Assert.Equal(6, archive.Entries.Count);
                Assert.Contains("Directory/", archive.Entries.Select(entry => entry.Key));
                Assert.Contains("Directory/Some file with veeeeeeeeeery loooooooooong name", archive.Entries.Select(entry => entry.Key));
                Assert.Contains("Directory/Directory with veeeeeeeeeery loooooooooong name/", archive.Entries.Select(entry => entry.Key));
                Assert.Contains("Directory/Directory with veeeeeeeeeery loooooooooong name/Some file with veeeeeeeeeery loooooooooong name", archive.Entries.Select(entry => entry.Key));
                Assert.Contains("Directory/Directory with veeeeeeeeeery loooooooooong name/Directory with veeeeeeeeeery loooooooooong name/", archive.Entries.Select(entry => entry.Key));
                Assert.Contains("Directory/Directory with veeeeeeeeeery loooooooooong name/Directory with veeeeeeeeeery loooooooooong name/Some file with veeeeeeeeeery loooooooooong name", archive.Entries.Select(entry => entry.Key));
            }
        }

        [Fact]
        public void Tar_Create_New()
        {
            string scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.tar");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

            // var aropt = new Ar

            using (var archive = TarArchive.Create())
            {
                archive.AddAllFromDirectory(ORIGINAL_FILES_PATH);
                var twopt = new TarWriterOptions(CompressionType.None, true);
                twopt.ArchiveEncoding = new ArchiveEncoding()
                {
                    Default = Encoding.GetEncoding(866)
                };
                archive.SaveTo(scratchPath, twopt);
            }
            CompareArchivesByPath(unmodified, scratchPath);
        }
        [Fact]
        public void Tar_Random_Write_Add()
        {
            string jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
            string scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.mod.tar");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.mod.tar");
            string modified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

            using (var archive = TarArchive.Open(unmodified))
            {
                archive.AddEntry("jpg\\test.jpg", jpg);
                archive.SaveTo(scratchPath, CompressionType.None);
            }
            CompareArchivesByPath(modified, scratchPath);
        }

        [Fact]
        public void Tar_Random_Write_Remove()
        {
            string scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.mod.tar");
            string modified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.mod.tar");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

            using (var archive = TarArchive.Open(unmodified))
            {
                var entry = archive.Entries.Single(x => x.Key.EndsWith("jpg"));
                archive.RemoveEntry(entry);
                archive.SaveTo(scratchPath, CompressionType.None);
            }
            CompareArchivesByPath(modified, scratchPath);
        }

        [Fact]
        public void Tar_Containing_Rar_Archive()
        {
            string archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.ContainsRar.tar");
            using (Stream stream = File.OpenRead(archiveFullPath))
            using (IArchive archive = ArchiveFactory.Open(stream))
            {
                Assert.True(archive.Type == ArchiveType.Tar);
            }
        }

        [Fact]
        public void Tar_Empty_Archive()
        {
            string archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.Empty.tar");
            using (Stream stream = File.OpenRead(archiveFullPath))
            using (IArchive archive = ArchiveFactory.Open(stream))
            {
                Assert.True(archive.Type == ArchiveType.Tar);
            }
        }
        [Theory]
        [InlineData(10)]
        [InlineData(128)]
        public void Tar_Japanese_Name(int length)
        {
            using (var mstm = new MemoryStream())
            {
                var enc = new ArchiveEncoding()
                {
                    Default = Encoding.UTF8
                };
                var twopt = new TarWriterOptions(CompressionType.None, true);
                twopt.ArchiveEncoding = enc;
                var fname = new string((char)0x3042, length);
                using (var tw = new TarWriter(mstm, twopt))
                using (var input = new MemoryStream(new byte[32]))
                {
                    tw.Write(fname, input, null);
                }
                using (var inputMemory = new MemoryStream(mstm.ToArray()))
                {
                    var tropt = new ReaderOptions()
                    {
                        ArchiveEncoding = enc
                    };
                    using (var tr = TarReader.Open(inputMemory, tropt))
                    {
                        while (tr.MoveToNextEntry())
                        {
                            Assert.Equal(fname, tr.Entry.Key);
                        }
                    }
                }
            }
        }

        [Fact]
        public void Tar_Read_One_At_A_Time()
        {
            var archiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8, };
            var tarWriterOptions = new TarWriterOptions(CompressionType.None, true) { ArchiveEncoding = archiveEncoding, };
            var testBytes = Encoding.UTF8.GetBytes("This is a test.");

            using (var memoryStream = new MemoryStream())
            {
                using (var tarWriter = new TarWriter(memoryStream, tarWriterOptions))
                using (var testFileStream = new MemoryStream(testBytes))
                {
                    tarWriter.Write("test1.txt", testFileStream);
                    testFileStream.Position = 0;
                    tarWriter.Write("test2.txt", testFileStream);
                }

                memoryStream.Position = 0;

                var numberOfEntries = 0;

                using (var archiveFactory = TarArchive.Open(memoryStream))
                {
                    foreach (var entry in archiveFactory.Entries)
                    {
                        ++numberOfEntries;

                        using (var tarEntryStream = entry.OpenEntryStream())
                        using (var testFileStream = new MemoryStream())
                        {
                            tarEntryStream.CopyTo(testFileStream);
                            Assert.Equal(testBytes.Length, testFileStream.Length);
                        }
                    }
                }

                Assert.Equal(2, numberOfEntries);
            }
        }
    }
}
