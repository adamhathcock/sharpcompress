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
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarArchiveAsyncTests : ArchiveTests
{
    public TarArchiveAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async ValueTask TarArchiveStreamRead_Async() => await ArchiveStreamReadAsync("Tar.tar");

    [Fact]
    public async ValueTask Tar_FileName_Exactly_100_Characters_Async()
    {
        var archive = "Tar_FileName_Exactly_100_Characters.tar";

        // create the 100 char filename
        var filename =
            "filename_with_exactly_100_characters_______________________________________________________________X";

        // Step 1: create a tar file containing a file with the test name
        using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
        {
            using (
                var writer = WriterFactory.OpenAsyncWriter(
                    new AsyncOnlyStream(stream),
                    ArchiveType.Tar,
                    new WriterOptions(CompressionType.None) { LeaveStreamOpen = false }
                )
            )
            using (Stream inputStream = new MemoryStream())
            {
                var sw = new StreamWriter(inputStream);
                await sw.WriteAsync("dummy filecontent");
                await sw.FlushAsync();

                inputStream.Position = 0;
                await writer.WriteAsync(filename, inputStream, null);
            }
        }

        // Step 2: check if the written tar file can be read correctly
        var unmodified = Path.Combine(SCRATCH2_FILES_PATH, archive);
        await using (
            var archive2 = await TarArchive.OpenAsyncArchive(
                new AsyncOnlyStream(File.OpenRead(unmodified)),
                new ReaderOptions() { LeaveStreamOpen = false }
            )
        )
        {
            Assert.Equal(1, await archive2.EntriesAsync.CountAsync());
            Assert.Contains(
                filename,
                await archive2.EntriesAsync.Select(entry => entry.Key).ToListAsync()
            );

            await foreach (var entry in archive2.EntriesAsync)
            {
                using (var sr = new StreamReader(await entry.OpenEntryStreamAsync()))
                {
                    Assert.Equal("dummy filecontent", await sr.ReadLineAsync());
                }
            }
        }
    }

    [Fact]
    public async ValueTask Tar_VeryLongFilepathReadback_Async()
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
        using (
            var writer = WriterFactory.OpenAsyncWriter(
                new AsyncOnlyStream(stream),
                ArchiveType.Tar,
                new WriterOptions(CompressionType.None) { LeaveStreamOpen = false }
            )
        )
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
        await using (
            var archive2 = await TarArchive.OpenAsyncArchive(
                new AsyncOnlyStream(File.OpenRead(unmodified)),
                new ReaderOptions() { LeaveStreamOpen = false }
            )
        )
        {
            Assert.Equal(1, await archive2.EntriesAsync.CountAsync());
            Assert.Contains(
                longFilename,
                await archive2.EntriesAsync.Select(entry => entry.Key).ToListAsync()
            );

            await foreach (var entry in archive2.EntriesAsync)
            {
                using (var sr = new StreamReader(await entry.OpenEntryStreamAsync()))
                {
                    Assert.Equal("dummy filecontent", await sr.ReadLineAsync());
                }
            }
        }
#if LEGACY_DOTNET
        //add a delay because old .net sucks on DisposeAsync
        await Task.Delay(TimeSpan.FromSeconds(1));
#endif
    }

    [Fact]
    public async ValueTask Tar_Create_New_Async()
    {
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.tar");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

        await using (var archive = await TarArchive.CreateAsyncArchive())
        {
            await archive.AddAllFromDirectoryAsync(ORIGINAL_FILES_PATH);
            var twopt = new TarWriterOptions(CompressionType.None, true)
            {
                ArchiveEncoding = new ArchiveEncoding { Default = Encoding.GetEncoding(866) },
            };
            await archive.SaveToAsync(scratchPath, twopt);
        }
        CompareArchivesByPath(unmodified, scratchPath);
    }

    [Fact]
    public async ValueTask Tar_Random_Write_Add_Async()
    {
        var jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.mod.tar");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.mod.tar");
        var modified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

        await using (var archive = await TarArchive.OpenAsyncArchive(unmodified))
        {
            await archive.AddEntryAsync("jpg\\test.jpg", jpg);
            await archive.SaveToAsync(
                scratchPath,
                new TarWriterOptions(CompressionType.None, true)
            );
        }
        CompareArchivesByPath(modified, scratchPath);
    }

    [Fact]
    public async ValueTask Tar_Random_Write_Remove_Async()
    {
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Tar.mod.tar");
        var modified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.mod.tar");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Tar.noEmptyDirs.tar");

        await using (var archive = await TarArchive.OpenAsyncArchive(unmodified))
        {
            var entry = await archive.EntriesAsync.SingleAsync(x =>
                x.Key.NotNull().EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
            );
            await archive.RemoveEntryAsync(entry);
            await archive.SaveToAsync(
                scratchPath,
                new TarWriterOptions(CompressionType.None, true)
            );
        }
        CompareArchivesByPath(modified, scratchPath);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(128)]
    public async ValueTask Tar_Japanese_Name_Async(int length)
    {
        using var mstm = new MemoryStream();
        var enc = new ArchiveEncoding { Default = Encoding.UTF8 };
        var twopt = new TarWriterOptions(CompressionType.None, true) { ArchiveEncoding = enc };
        var fname = new string((char)0x3042, length);
        using (var tw = new TarWriter(mstm, twopt))
        using (var input = new MemoryStream(new byte[32]))
        {
            await tw.WriteAsync(fname, input, null);
        }
        using (var inputMemory = new MemoryStream(mstm.ToArray()))
        {
            var tropt = new ReaderOptions { ArchiveEncoding = enc };
            await using (
                var tr = await ReaderFactory.OpenAsyncReader(
                    new AsyncOnlyStream(inputMemory),
                    tropt
                )
            )
            {
                while (await tr.MoveToNextEntryAsync())
                {
                    Assert.Equal(fname, tr.Entry.Key);
                }
            }
        }
    }

    [Fact]
    public async ValueTask Tar_Read_One_At_A_Time_Async()
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

        await using (
            var archiveFactory = await ArchiveFactory.OpenAsyncArchive(
                new AsyncOnlyStream(memoryStream)
            )
        )
        {
            await foreach (var entry in archiveFactory.EntriesAsync)
            {
                ++numberOfEntries;

#if LEGACY_DOTNET
                using var tarEntryStream = await entry.OpenEntryStreamAsync();
#else
                await using var tarEntryStream = await entry.OpenEntryStreamAsync();
#endif
                using var testFileStream = new MemoryStream();
                await tarEntryStream.CopyToAsync(testFileStream);
                Assert.Equal(testBytes.Length, testFileStream.Length);
            }
        }

        Assert.Equal(2, numberOfEntries);
    }
}
