using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipArchiveAsyncTests : ArchiveTests
{
    [Fact]
    public async Task SevenZipArchive_LZMA_AsyncStreamExtraction()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.LZMA.7z");
#if NETFRAMEWORK
        using var stream = File.OpenRead(testArchive);
#else
        await using var stream = File.OpenRead(testArchive);
#endif
        await using var archive = await ArchiveFactory.OpenAsyncArchive(stream);

        await foreach (var entry in archive.EntriesAsync.Where(entry => !entry.IsDirectory))
        {
            var targetPath = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

#if NETFRAMEWORK
            using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#else
            await using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#endif
#if NETFRAMEWORK
            using var targetStream = File.Create(targetPath);
#else
            await using var targetStream = File.Create(targetPath);
#endif
#if NETFRAMEWORK
            await sourceStream.CopyToAsync(targetStream, 81920, CancellationToken.None);
#else
            await sourceStream.CopyToAsync(targetStream, CancellationToken.None);
#endif
        }

        VerifyFiles();
    }

    //[Fact]
    public async Task SevenZipArchive_LZMA2_AsyncStreamExtraction()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.LZMA2.7z");
#if NETFRAMEWORK
        using var stream = File.OpenRead(testArchive);
#else
        await using var stream = File.OpenRead(testArchive);
#endif
        await using var archive = await ArchiveFactory.OpenAsyncArchive(stream);

        await foreach (var entry in archive.EntriesAsync.Where(entry => !entry.IsDirectory))
        {
            var targetPath = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

#if NETFRAMEWORK
            using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#else
            await using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#endif
#if NETFRAMEWORK
            using var targetStream = File.Create(targetPath);
#else
            await using var targetStream = File.Create(targetPath);
#endif
#if NETFRAMEWORK
            await sourceStream.CopyToAsync(targetStream, 81920, CancellationToken.None);
#else
            await sourceStream.CopyToAsync(targetStream, CancellationToken.None);
#endif
        }

        VerifyFiles();
    }

    [Fact]
    public async Task SevenZipArchive_Solid_AsyncStreamExtraction()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.7z");
#if NETFRAMEWORK
        using var stream = File.OpenRead(testArchive);
#else
        await using var stream = File.OpenRead(testArchive);
#endif
        await using var archive = await ArchiveFactory.OpenAsyncArchive(stream);

        await foreach (var entry in archive.EntriesAsync.Where(entry => !entry.IsDirectory))
        {
            var targetPath = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

#if NETFRAMEWORK
            using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#else
            await using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#endif
#if NETFRAMEWORK
            using var targetStream = File.Create(targetPath);
#else
            await using var targetStream = File.Create(targetPath);
#endif
#if NETFRAMEWORK
            await sourceStream.CopyToAsync(targetStream, 81920, CancellationToken.None);
#else
            await sourceStream.CopyToAsync(targetStream, CancellationToken.None);
#endif
        }

        VerifyFiles();
    }

    [Fact]
    public async Task SevenZipArchive_BZip2_AsyncStreamExtraction()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.BZip2.7z");
#if NETFRAMEWORK
        using var stream = File.OpenRead(testArchive);
#else
        await using var stream = File.OpenRead(testArchive);
#endif
        await using var archive = await ArchiveFactory.OpenAsyncArchive(stream);

        await foreach (var entry in archive.EntriesAsync.Where(entry => !entry.IsDirectory))
        {
            var targetPath = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

#if NETFRAMEWORK
            using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#else
            await using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#endif
#if NETFRAMEWORK
            using var targetStream = File.Create(targetPath);
#else
            await using var targetStream = File.Create(targetPath);
#endif
#if NETFRAMEWORK
            await sourceStream.CopyToAsync(targetStream, 81920, CancellationToken.None);
#else
            await sourceStream.CopyToAsync(targetStream, CancellationToken.None);
#endif
        }

        VerifyFiles();
    }

    [Fact]
    public async Task SevenZipArchive_PPMd_AsyncStreamExtraction()
    {
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.PPMd.7z");
#if NETFRAMEWORK
        using var stream = File.OpenRead(testArchive);
#else
        await using var stream = File.OpenRead(testArchive);
#endif
        await using var archive = await ArchiveFactory.OpenAsyncArchive(stream);

        await foreach (var entry in archive.EntriesAsync.Where(entry => !entry.IsDirectory))
        {
            var targetPath = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

#if NETFRAMEWORK
            using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#else
            await using var sourceStream = await entry.OpenEntryStreamAsync(CancellationToken.None);
#endif
#if NETFRAMEWORK
            using var targetStream = File.Create(targetPath);
#else
            await using var targetStream = File.Create(targetPath);
#endif
#if NETFRAMEWORK
            await sourceStream.CopyToAsync(targetStream, 81920, CancellationToken.None);
#else
            await sourceStream.CopyToAsync(targetStream, CancellationToken.None);
#endif
        }

        VerifyFiles();
    }
}
