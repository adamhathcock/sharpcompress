using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarArchiveAsyncTests : ArchiveTests
{
    public TarArchiveAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async Task TarArchiveStreamRead_Async() => await ArchiveStreamReadAsync("Tar.tar");

    [Fact]
    public async Task Tar_FileName_Exactly_100_Characters_Async()
    {
        var archive = "Tar_FileName_Exactly_100_Characters.tar";

        // create the 100 char filename
        var filename =
            "filename_with_exactly_100_characters_______________________________________________________________X";

        // Step 1: create a tar file containing a file with the test name
        using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
        using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, CompressionType.None))
        using (Stream inputStream = new MemoryStream())
        {
            var sw = new StreamWriter(inputStream);
            await sw.WriteAsync("dummy filecontent");
            await sw.FlushAsync();

            inputStream.Position = 0;
            await writer.WriteAsync(filename, inputStream, null);
        }

        // Step 2: check if the written tar file can be read correctly
        var unmodified = Path.Combine(SCRATCH2_FILES_PATH, archive);
        using (var archive2 = TarArchive.Open(unmodified))
        {
            Assert.Equal(1, archive2.Entries.Count);
            Assert.Contains(filename, archive2.Entries.Select(entry => entry.Key));

            foreach (var entry in archive2.Entries)
            {
                Assert.Equal(
                    "dummy filecontent",
                    await new StreamReader(entry.OpenEntryStream()).ReadLineAsync()
                );
            }
        }
    }

    [Fact]
    public async Task Tar_VeryLongFilepathReadback_Async()
    {
        var archive = "Tar_VeryLongFilepathReadback.tar";

        // create a very long filename
        var longFilename = "";
        for (var i = 0; i < 600; i = longFilename.Length)
        {
            longFilename += i.ToString("D10") + "-";
        }

        longFilename += ".txt";

        // Step 1: create a tar file containing a file with a long name
        using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
        using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, CompressionType.None))
        using (Stream inputStream = new MemoryStream())
        {
            var sw = new StreamWriter(inputStream);
            await sw.WriteAsync("dummy filecontent");
            await sw.FlushAsync();

            inputStream.Position = 0;
            await writer.WriteAsync(longFilename, inputStream, null);
        }

        // Step 2: check if the written tar file can be read correctly
        var unmodified = Path.Combine(SCRATCH2_FILES_PATH, archive);
        using (var archive2 = TarArchive.Open(unmodified))
        {
            Assert.Equal(1, archive2.Entries.Count);
            Assert.Contains(longFilename, archive2.Entries.Select(entry => entry.Key));

            foreach (var entry in archive2.Entries)
            {
                Assert.Equal(
                    "dummy filecontent",
                    await new StreamReader(entry.OpenEntryStream()).ReadLineAsync()
                );
            }
        }
    }

    [Fact]
    public async Task Tar_Create_New_Async()
    {
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.tar");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

        using (var archive = TarArchive.Create())
        {
            archive.AddAllFromDirectory(ORIGINAL_FILES_PATH);
            var twopt = new TarWriterOptions(CompressionType.None, true);
            twopt.ArchiveEncoding = new ArchiveEncoding { Default = Encoding.GetEncoding(866) };
            await archive.SaveToAsync(scratchPath, twopt);
        }
        CompareArchivesByPath(unmodified, scratchPath);
    }

    [Fact]
    public async Task Tar_Random_Write_Add_Async()
    {
        var jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.mod.tar");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.mod.tar");
        var modified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

        using (var archive = TarArchive.Open(unmodified))
        {
            archive.AddEntry("jpg\\test.jpg", jpg);
            await archive.SaveToAsync(scratchPath, new WriterOptions(CompressionType.None));
        }
        CompareArchivesByPath(modified, scratchPath);
    }

    [Fact]
    public async Task Tar_Random_Write_Remove_Async()
    {
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.mod.tar");
        var modified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.mod.tar");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

        using (var archive = TarArchive.Open(unmodified))
        {
            var entry = archive.Entries.Single(x =>
                x.Key.NotNull().EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
            );
            archive.RemoveEntry(entry);
            await archive.SaveToAsync(scratchPath, new WriterOptions(CompressionType.None));
        }
        CompareArchivesByPath(modified, scratchPath);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(128)]
    public async Task Tar_Japanese_Name_Async(int length)
    {
        using var mstm = new MemoryStream();
        var enc = new ArchiveEncoding { Default = Encoding.UTF8 };
        var twopt = new TarWriterOptions(CompressionType.None, true);
        twopt.ArchiveEncoding = enc;
        var fname = new string((char)0x3042, length);
        using (var tw = new TarWriter(mstm, twopt))
        using (var input = new MemoryStream(new byte[32]))
        {
            await tw.WriteAsync(fname, input, null);
        }
        using (var inputMemory = new MemoryStream(mstm.ToArray()))
        {
            var tropt = new ReaderOptions { ArchiveEncoding = enc };
            using (var tr = TarReader.Open(inputMemory, tropt))
            {
                while (tr.MoveToNextEntry())
                {
                    Assert.Equal(fname, tr.Entry.Key);
                }
            }
        }
    }

    [Fact]
    public async Task Tar_Read_One_At_A_Time_Async()
    {
        var archiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 };
        var tarWriterOptions = new TarWriterOptions(CompressionType.None, true)
        {
            ArchiveEncoding = archiveEncoding,
        };
        var testBytes = Encoding.UTF8.GetBytes("This is a test.");

        using var memoryStream = new MemoryStream();
        using (var tarWriter = new TarWriter(memoryStream, tarWriterOptions))
        using (var testFileStream = new MemoryStream(testBytes))
        {
            await tarWriter.WriteAsync("test1.txt", testFileStream, null);
            testFileStream.Position = 0;
            await tarWriter.WriteAsync("test2.txt", testFileStream, null);
        }

        memoryStream.Position = 0;

        var numberOfEntries = 0;

        using (var archiveFactory = TarArchive.Open(memoryStream))
        {
            foreach (var entry in archiveFactory.Entries)
            {
                ++numberOfEntries;

                using var tarEntryStream = entry.OpenEntryStream();
                using var testFileStream = new MemoryStream();
                await tarEntryStream.CopyToAsync(testFileStream);
                Assert.Equal(testBytes.Length, testFileStream.Length);
            }
        }

        Assert.Equal(2, numberOfEntries);
    }
}
