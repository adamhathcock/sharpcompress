using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;
using Xunit;

namespace SharpCompress.Test;

public class AsyncTests : TestBase
{
    [Fact]
    public async Task Reader_Async_Extract_All()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz");
        using var stream = File.OpenRead(testArchive);
        using var reader = ReaderFactory.Open(stream);

        await reader.WriteAllToDirectoryAsync(
            SCRATCH_FILES_PATH,
            new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
        );

        // Just verify some files were extracted
        var extractedFiles = Directory.GetFiles(SCRATCH_FILES_PATH, "*", SearchOption.AllDirectories);
        Assert.True(extractedFiles.Length > 0, "No files were extracted");
    }

    [Fact]
    public async Task Reader_Async_Extract_Single_Entry()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz");
        using var stream = File.OpenRead(testArchive);
        using var reader = ReaderFactory.Open(stream);

        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                var outputPath = Path.Combine(SCRATCH_FILES_PATH, reader.Entry.Key!);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                using var outputStream = File.Create(outputPath);
                await reader.WriteEntryToAsync(outputStream);
                break; // Just test one entry
            }
        }
    }

    [Fact]
    public async Task Archive_Entry_Async_Open_Stream()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz");
        using var archive = ArchiveFactory.Open(testArchive);

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory).Take(1))
        {
            using var entryStream = await entry.OpenEntryStreamAsync();
            Assert.NotNull(entryStream);
            Assert.True(entryStream.CanRead);

            // Read some data to verify it works
            var buffer = new byte[1024];
            var read = await entryStream.ReadAsync(buffer, 0, buffer.Length);
            Assert.True(read > 0);
        }
    }

    [Fact]
    public async Task Writer_Async_Write_Single_File()
    {
        var outputPath = Path.Combine(SCRATCH_FILES_PATH, "async_test.zip");
        using (var stream = File.Create(outputPath))
        using (var writer = WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate))
        {
            var testFile = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz");
            using var fileStream = File.OpenRead(testFile);
            await writer.WriteAsync("test_entry.bin", fileStream, new DateTime(2023, 1, 1));
        }

        // Verify the archive was created and contains the entry
        Assert.True(File.Exists(outputPath));
        using var archive = ZipArchive.Open(outputPath);
        Assert.Single(archive.Entries.Where(e => !e.IsDirectory));
    }

    [Fact]
    public async Task Async_With_Cancellation_Token()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10000); // 10 seconds should be plenty

        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz");
        using var stream = File.OpenRead(testArchive);
        using var reader = ReaderFactory.Open(stream);

        await reader.WriteAllToDirectoryAsync(
            SCRATCH_FILES_PATH,
            new ExtractionOptions { ExtractFullPath = true, Overwrite = true },
            cts.Token
        );

        // Just verify some files were extracted
        var extractedFiles = Directory.GetFiles(SCRATCH_FILES_PATH, "*", SearchOption.AllDirectories);
        Assert.True(extractedFiles.Length > 0, "No files were extracted");
    }

    [Fact]
    public async Task Stream_Extensions_Async()
    {
        var testFile = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar.gz");
        using var inputStream = File.OpenRead(testFile);
        var outputPath = Path.Combine(SCRATCH_FILES_PATH, "async_copy.bin");
        using var outputStream = File.Create(outputPath);

        // Test the async extension method
        var buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await outputStream.WriteAsync(buffer, 0, bytesRead);
        }

        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }
}
