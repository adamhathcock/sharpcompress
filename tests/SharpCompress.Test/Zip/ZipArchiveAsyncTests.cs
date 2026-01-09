using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipArchiveAsyncTests : ArchiveTests
{
    public ZipArchiveAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async Task Zip_ZipX_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.zipx");

    [Fact]
    public async Task Zip_BZip2_Streamed_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.bzip2.dd.zip");

    [Fact]
    public async Task Zip_BZip2_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.bzip2.zip");

    [Fact]
    public async Task Zip_Deflate_Streamed2_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.deflate.dd-.zip");

    [Fact]
    public async Task Zip_Deflate_Streamed_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.deflate.dd.zip");

    [Fact]
    public async Task Zip_Deflate_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.deflate.zip");

    [Fact]
    public async Task Zip_Deflate64_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.deflate64.zip");

    [Fact]
    public async Task Zip_LZMA_Streamed_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.lzma.dd.zip");

    [Fact]
    public async Task Zip_LZMA_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.lzma.zip");

    [Fact]
    public async Task Zip_PPMd_Streamed_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.ppmd.dd.zip");

    [Fact]
    public async Task Zip_PPMd_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.ppmd.zip");

    [Fact]
    public async Task Zip_None_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.none.zip");

    [Fact]
    public async Task Zip_Zip64_ArchiveStreamRead_Async() =>
        await ArchiveStreamReadAsync("Zip.zip64.zip");

    [Fact]
    public async Task Zip_Shrink_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.shrink.zip");
    }

    [Fact]
    public async Task Zip_Implode_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.implode.zip");
    }

    [Fact]
    public async Task Zip_Reduce1_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.reduce1.zip");
    }

    [Fact]
    public async Task Zip_Reduce2_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.reduce2.zip");
    }

    [Fact]
    public async Task Zip_Reduce3_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.reduce3.zip");
    }

    [Fact]
    public async Task Zip_Reduce4_ArchiveStreamRead_Async()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
        await ArchiveStreamReadAsync("Zip.reduce4.zip");
    }

    [Fact]
    public async Task Zip_Random_Write_Remove_Async()
    {
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Zip.deflate.mod.zip");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");
        var modified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.mod.zip");

        using (var archive = ZipArchive.Open(unmodified))
        {
            var entry = archive.Entries.Single(x =>
                x.Key.NotNull().EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
            );
            archive.RemoveEntry(entry);

            WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate);
            writerOptions.ArchiveEncoding.Default = Encoding.GetEncoding(866);

            await archive.SaveToAsync(scratchPath, writerOptions);
        }
        CompareArchivesByPath(modified, scratchPath, Encoding.GetEncoding(866));
    }

    [Fact]
    public async Task Zip_Random_Write_Add_Async()
    {
        var jpg = Path.Combine(ORIGINAL_FILES_PATH, "jpg", "test.jpg");
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Zip.deflate.mod.zip");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.mod.zip");
        var modified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");

        using (var archive = ZipArchive.Open(unmodified))
        {
            archive.AddEntry("jpg\\test.jpg", jpg);

            WriterOptions writerOptions = new ZipWriterOptions(CompressionType.Deflate);
            writerOptions.ArchiveEncoding.Default = Encoding.GetEncoding(866);

            await archive.SaveToAsync(scratchPath, writerOptions);
        }
        CompareArchivesByPath(modified, scratchPath, Encoding.GetEncoding(866));
    }

    [Fact]
    public async Task Zip_Create_New_Async()
    {
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "Zip.deflate.noEmptyDirs.zip");
        var unmodified = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.noEmptyDirs.zip");

        using (var archive = ZipArchive.Create())
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
    public async Task Zip_Deflate_Entry_Stream_Async()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip")))
        using (var archive = ZipArchive.Open(stream))
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                await entry.WriteToDirectoryAsync(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Zip_Deflate_Archive_WriteToDirectoryAsync()
    {
        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip")))
        using (var archive = ZipArchive.Open(stream))
        {
            await archive.WriteToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
        }
        VerifyFiles();
    }

    [Fact]
    public async Task Zip_Deflate_Archive_WriteToDirectoryAsync_WithProgress()
    {
        var progressReports = new System.Collections.Generic.List<ProgressReport>();
        var progress = new Progress<ProgressReport>(report => progressReports.Add(report));

        using (Stream stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip")))
        using (var archive = ZipArchive.Open(stream))
        {
            await archive.WriteToDirectoryAsync(
                SCRATCH_FILES_PATH,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true },
                progress
            );
        }

        VerifyFiles();
        Assert.True(progressReports.Count > 0, "Progress reports should be generated");
    }
}
