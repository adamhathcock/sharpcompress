using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.SevenZip;

#if !NETFRAMEWORK
public class SevenZipArchiveAsyncTests : ArchiveTests
{
    [Fact]
    public async Task SevenZipArchive_LZMA_AsyncStreamExtraction()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.LZMA.7z");
        using var stream = File.OpenRead(testArchive);
        using var archive = ArchiveFactory.Open(stream);

        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            var targetPath = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
            await using var targetStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(targetStream, CancellationToken.None);
        }

        VerifyFiles();
    }

    [Fact]
    public async Task SevenZipArchive_LZMA2_AsyncStreamExtraction()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.LZMA2.7z");
        using var stream = File.OpenRead(testArchive);
        using var archive = ArchiveFactory.Open(stream);

        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            var targetPath = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
            await using var targetStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(targetStream, CancellationToken.None);
        }

        VerifyFiles();
    }

    [Fact]
    public async Task SevenZipArchive_Solid_AsyncStreamExtraction()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.7z");
        using var stream = File.OpenRead(testArchive);
        using var archive = ArchiveFactory.Open(stream);

        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            var targetPath = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
            await using var targetStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(targetStream, CancellationToken.None);
        }

        VerifyFiles();
    }

    [Fact]
    public async Task SevenZipArchive_BZip2_AsyncStreamExtraction()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.BZip2.7z");
        using var stream = File.OpenRead(testArchive);
        using var archive = ArchiveFactory.Open(stream);

        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            var targetPath = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
            await using var targetStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(targetStream, CancellationToken.None);
        }

        VerifyFiles();
    }

    [Fact]
    public async Task SevenZipArchive_PPMd_AsyncStreamExtraction()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.PPMd.7z");
        using var stream = File.OpenRead(testArchive);
        using var archive = ArchiveFactory.Open(stream);

        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            var targetPath = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
            await using var targetStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(targetStream, CancellationToken.None);
        }

        VerifyFiles();
    }
}
#endif
