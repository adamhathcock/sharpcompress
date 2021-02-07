using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Writers;
using Xunit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        public async ValueTask TarArchiveStreamRead()
        {
            await ArchiveStreamReadAsync("Tar.tar");
        }

        [Fact]
        public async ValueTask TarArchivePathRead()
        {
            await ArchiveFileReadAsync("Tar.tar");
        }

        [Fact]
        public async Task Tar_FileName_Exactly_100_Characters()
        {
            string archive = "Tar_FileName_Exactly_100_Characters.tar";


            // create the 100 char filename
            string filename = "filename_with_exactly_100_characters_______________________________________________________________X";

            // Step 1: create a tar file containing a file with the test name
            await using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            await using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, CompressionType.None))
            await using (Stream inputStream = new MemoryStream())
            {
                StreamWriter sw = new StreamWriter(inputStream);
                await sw.WriteAsync("dummy filecontent");
                await sw.FlushAsync();

                inputStream.Position = 0;
                await writer.WriteAsync(filename, inputStream, null);
            }

            // Step 2: check if the written tar file can be read correctly
            string unmodified = Path.Combine(SCRATCH2_FILES_PATH, archive);
            await using (var archive2 = TarArchive.Open(unmodified))
            {
                Assert.Equal(1, await archive2.Entries.CountAsync());
                Assert.Contains(filename, await archive2.Entries.Select(entry => entry.Key).ToListAsync());

                await foreach (var entry in archive2.Entries)
                {
                    Assert.Equal("dummy filecontent", await new StreamReader(entry.OpenEntryStream()).ReadLineAsync());
                }
            }
        }

        [Fact]
        public async ValueTask Tar_NonUstarArchiveWithLongNameDoesNotSkipEntriesAfterTheLongOne()
        {
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "very long filename.tar");
            await using var archive = TarArchive.Open(unmodified);
            Assert.Equal(5, await archive.Entries.CountAsync());
            Assert.Contains("very long filename/", await archive.Entries.Select(entry => entry.Key).ToListAsync());
            Assert.Contains("very long filename/very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename very long filename.jpg", await archive.Entries.Select(entry => entry.Key).ToListAsync());
            Assert.Contains("z_file 1.txt", await archive.Entries.Select(entry => entry.Key).ToListAsync());
            Assert.Contains("z_file 2.txt", await archive.Entries.Select(entry => entry.Key).ToListAsync());
            Assert.Contains("z_file 3.txt", await archive.Entries.Select(entry => entry.Key).ToListAsync());
        }

        [Fact]
        public async ValueTask Tar_VeryLongFilepathReadback()
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
            await using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            await using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, CompressionType.None))
            await using (Stream inputStream = new MemoryStream())
            {
                StreamWriter sw = new StreamWriter(inputStream);
                await sw.WriteAsync("dummy filecontent");
                await sw.FlushAsync();

                inputStream.Position = 0;
                await writer.WriteAsync(longFilename, inputStream, null);
            }

            // Step 2: check if the written tar file can be read correctly
            string unmodified = Path.Combine(SCRATCH2_FILES_PATH, archive);
            await using (var archive2 = TarArchive.Open(unmodified))
            {
                Assert.Equal(1, await archive2.Entries.CountAsync());
                Assert.Contains(longFilename, await archive2.Entries.Select(entry => entry.Key).ToListAsync());

                await foreach (var entry in archive2.Entries)
                {
                    Assert.Equal("dummy filecontent", await new StreamReader(entry.OpenEntryStream()).ReadLineAsync());
                }
            }
        }

        [Fact]
        public async ValueTask Tar_UstarArchivePathReadLongName()
        {
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "ustar with long names.tar");
            await using var archive = TarArchive.Open(unmodified);
            Assert.Equal(6, await archive.Entries.CountAsync());
            Assert.Contains("Directory/", await archive.Entries.Select(entry => entry.Key).ToListAsync());
            Assert.Contains("Directory/Some file with veeeeeeeeeery loooooooooong name", await archive.Entries.Select(entry => entry.Key).ToListAsync());
            Assert.Contains("Directory/Directory with veeeeeeeeeery loooooooooong name/", await archive.Entries.Select(entry => entry.Key).ToListAsync());
            Assert.Contains("Directory/Directory with veeeeeeeeeery loooooooooong name/Some file with veeeeeeeeeery loooooooooong name", await archive.Entries.Select(entry => entry.Key).ToListAsync());
            Assert.Contains("Directory/Directory with veeeeeeeeeery loooooooooong name/Directory with veeeeeeeeeery loooooooooong name/", await archive.Entries.Select(entry => entry.Key).ToListAsync());
            Assert.Contains("Directory/Directory with veeeeeeeeeery loooooooooong name/Directory with veeeeeeeeeery loooooooooong name/Some file with veeeeeeeeeery loooooooooong name", await archive.Entries.Select(entry => entry.Key).ToListAsync());
        }

        [Fact]
        public async Task Tar_Create_New()
        {
            string scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.tar");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

            // var aropt = new Ar

            await using (var archive = TarArchive.Create())
            {
                await archive.AddAllFromDirectoryAsync(ORIGINAL_FILES_PATH);
                var twopt = new TarWriterOptions(CompressionType.None, true);
                twopt.ArchiveEncoding = new ArchiveEncoding()
                {
                    Default = Encoding.GetEncoding(866)
                };
                await archive.SaveToAsync(scratchPath, twopt);
            }
            await CompareArchivesByPathAsync(unmodified, scratchPath);
        }
        [Fact]
        public async Task Tar_Random_Write_Add()
        {
            string jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
            string scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.mod.tar");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.mod.tar");
            string modified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

            await using (var archive = TarArchive.Open(unmodified))
            {
                await archive.AddEntryAsync("jpg\\test.jpg", jpg);
                await archive.SaveToAsync(scratchPath, CompressionType.None);
            }
            await CompareArchivesByPathAsync(modified, scratchPath);
        }

        [Fact]
        public async Task Tar_Random_Write_Remove()
        {
            string scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.mod.tar");
            string modified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.mod.tar");
            string unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

            await using (var archive = TarArchive.Open(unmodified))
            {
                var entry = await archive.Entries.SingleAsync(x => x.Key.EndsWith("jpg"));
                await archive.RemoveEntryAsync(entry);
                await archive.SaveToAsync(scratchPath, CompressionType.None);
            }
            await CompareArchivesByPathAsync(modified, scratchPath);
        }

        [Fact]
        public async ValueTask Tar_Containing_Rar_Archive()
        {
            string archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.ContainsRar.tar");
            await using Stream stream = File.OpenRead(archiveFullPath);
            await using IArchive archive = await ArchiveFactory.OpenAsync(stream);
            Assert.True(archive.Type == ArchiveType.Tar);
        }

        [Fact]
        public async ValueTask Tar_Empty_Archive()
        {
            string archiveFullPath = Path.Combine(TEST_ARCHIVES_PATH, "Tar.Empty.tar");
            await using Stream stream = File.OpenRead(archiveFullPath);
            await using IArchive archive = await ArchiveFactory.OpenAsync(stream);
            Assert.True(archive.Type == ArchiveType.Tar);
        }
        [Theory]
        [InlineData(10)]
        [InlineData(128)]
        public async Task Tar_Japanese_Name(int length)
        {
            await using var mstm = new MemoryStream();
            var enc = new ArchiveEncoding()
                      {
                          Default = Encoding.UTF8
                      };
            var twopt = new TarWriterOptions(CompressionType.None, true);
            twopt.ArchiveEncoding = enc;
            var fname = new string((char)0x3042, length);
            await using (var tw = new TarWriter(mstm, twopt))
            await using (var input = new MemoryStream(new byte[32]))
            {
                await tw.WriteAsync(fname, input, null);
            }

            await using (var inputMemory = new MemoryStream(mstm.ToArray()))
            {
                var tropt = new ReaderOptions()
                            {
                                ArchiveEncoding = enc
                            };
                await using (var tr = await TarReader.OpenAsync(inputMemory, tropt))
                {
                    while (await tr.MoveToNextEntryAsync())
                    {
                        Assert.Equal(fname, tr.Entry.Key);
                    }
                }
            }
        }

        [Fact]
        public async Task Tar_Read_One_At_A_Time()
        {
            var archiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8, };
            var tarWriterOptions = new TarWriterOptions(CompressionType.None, true) { ArchiveEncoding = archiveEncoding, };
            var testBytes = Encoding.UTF8.GetBytes("This is a test.");

            await using var memoryStream = new MemoryStream();
            await using (var tarWriter = new TarWriter(memoryStream, tarWriterOptions))
            await using (var testFileStream = new MemoryStream(testBytes))
            {
                await tarWriter.WriteAsync("test1.txt", testFileStream);
                testFileStream.Position = 0;
                await tarWriter.WriteAsync("test2.txt", testFileStream);
            }

            memoryStream.Position = 0;

            var numberOfEntries = 0;

            await using (var archiveFactory = TarArchive.Open(memoryStream))
            {
                await foreach (var entry in archiveFactory.Entries)
                {
                    ++numberOfEntries;

                    await using var tarEntryStream = entry.OpenEntryStream();
                    await using var testFileStream = new MemoryStream();
                    await tarEntryStream.CopyToAsync(testFileStream);
                    Assert.Equal(testBytes.Length, testFileStream.Length);
                }
            }

            Assert.Equal(2, numberOfEntries);
        }
    }
}
