using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipArchiveAsyncTests : ArchiveTests
{
    public ZipArchiveAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async ValueTask Zip_ZipX_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.zipx");

    [Fact]
    public async ValueTask Zip_BZip2_Streamed_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.bzip2.dd.zip");

    [Fact]
    public async ValueTask Zip_BZip2_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.bzip2.zip");

    [Fact]
    public async ValueTask Zip_Deflate_Streamed2_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.deflate.dd-.zip");

    [Fact]
    public async ValueTask Zip_Deflate_Streamed_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.deflate.dd.zip");

    [Fact]
    public async ValueTask Zip_Deflate_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.deflate.zip");

    [Fact]
    public async ValueTask Zip_Deflate64_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.deflate64.zip");

    [Fact]
    public async ValueTask Zip_LZMA_Streamed_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.lzma.dd.zip");

    [Fact]
    public async ValueTask Zip_LZMA_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.lzma.zip");

    [Fact]
    public async ValueTask Zip_PPMd_Streamed_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.ppmd.dd.zip");

    [Fact]
    public async ValueTask Zip_PPMd_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.ppmd.zip");

    [Fact]
    public async ValueTask Zip_None_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.none.zip");

    [Fact]
    public async ValueTask Zip_Zip64_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.zip64.zip");

    [Fact]
    public async ValueTask Zip_Shrink_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.shrink.zip");
    }

    [Fact]
    public async ValueTask Zip_Implode_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.implode.zip");
    }

    [Fact]
    public async ValueTask Zip_Reduce1_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.reduce1.zip");
    }

    [Fact]
    public async ValueTask Zip_Reduce2_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.reduce2.zip");
    }

    [Fact]
    public async ValueTask Zip_Reduce3_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.reduce3.zip");
    }

    [Fact]
    public async ValueTask Zip_Reduce4_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.reduce4.zip");
    }

    [Fact]
    public async ValueTask Zip_Random_Write_Remove_Async()
    {
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Zip.deflate.mod.zip");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");
        var modified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.mod.zip");

        await using (var archive = ZipArchive.OpenAsyncArchive(unmodified))
        {
            var entry = await archive.EntriesAsync.SingleAsync(x =>
                x.Key.NotNull().EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
            );
            await archive.RemoveEntryAsync(entry);

            WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate);
            writerOptions.ArchiveEncoding.Default = Encoding.GetEncoding(866);

            await archive.SaveToAsync(scratchPath, writerOptions);
        }
        CompareArchivesByPath(modified, scratchPath, Encoding.GetEncoding(866));
    }

    [Fact]
    public async ValueTask Zip_Random_Write_Add_Async()
    {
        var jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Zip.deflate.mod.zip");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.mod.zip");
        var modified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");

        await using (var archive = ZipArchive.OpenAsyncArchive(unmodified))
        {
            await archive.AddEntryAsync("jpg\\test.jpg", jpg);

            WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate);
            writerOptions.ArchiveEncoding.Default = Encoding.GetEncoding(866);

            await archive.SaveToAsync(scratchPath, writerOptions);
        }
        CompareArchivesByPath(modified, scratchPath, Encoding.GetEncoding(866));
    }

    [Fact]
    public async ValueTask Zip_Create_New_Async()
    {
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Zip.deflate.noEmptyDirs.zip");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");

        await using (var archive = (ZipArchive)ZipArchive.CreateAsyncArchive())
        {
            archive.DeflateCompressionLevel = CompressionLevel.BestSpeed;
            archive.AddAllFromDirectory(ORIGINAL_FILES_PATH);

            WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate);
            writerOptions.ArchiveEncoding.Default = Encoding.UTF8;

            await archive.SaveToAsync(scratchPath, writerOptions);
        }
        CompareArchivesByPath(unmodified, scratchPath);
    }

    [Fact]
    public async ValueTask Zip_Deflate_Entry_Stream_Async()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip")))
        {
            IAsyncArchive archive = ZipArchive.OpenAsyncArchive(new AsyncOnlyStream(stream));
            try
            {
                await foreach (var entry in archive.EntriesAsync.Where(entry => !entry.IsDirectory))
                {
                    await entry.WriteToDirectoryAsync(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
            finally
            {
                await archive.DisposeAsync();
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async ValueTask Zip_Deflate_Archive_WriteToDirectoryAsync()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip")))
        {
            IAsyncArchive archive = ZipArchive.OpenAsyncArchive(new AsyncOnlyStream(stream));
            try
            {
                await archive.WriteToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
            finally
            {
                await archive.DisposeAsync();
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async ValueTask Zip_Deflate_Archive_WriteToDirectoryAsync_WithProgress()
    {
        var progressReports = new System.Collections.Generic.List<ProgressReport>();
        var progress = new Progress<ProgressReport>(report => progressReports.Add(report));

#if NETFRAMEWORK
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip")))
#else
        await using (
            Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"))
        )
#endif
        {
            await using IAsyncArchive archive = ZipArchive.OpenAsyncArchive(
                new AsyncOnlyStream(stream)
            );
            await archive.WriteToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true },
                progress
            );
        }

        await Task.Delay(1000);
        VerifyFiles();
        Assert.True(progressReports.Count > 0, "Progress reports should be generated");
    }
}
