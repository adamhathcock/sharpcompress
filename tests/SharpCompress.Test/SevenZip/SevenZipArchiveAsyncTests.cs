using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;
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
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            new AsyncOnlyStream(stream)
        );

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
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            new AsyncOnlyStream(stream)
        );

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
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            new AsyncOnlyStream(stream)
        );

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
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            new AsyncOnlyStream(stream)
        );

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
        await using var archive = await ArchiveFactory.OpenAsyncArchive(
            new AsyncOnlyStream(stream)
        );

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
    public async Task SevenZipArchive_Solid_ExtractAllEntries_Contiguous_Async()
    {
        // This test verifies that solid archives iterate entries as contiguous streams
        // rather than recreating the decompression stream for each entry
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.7z");
        await using var archive = await SevenZipArchive.OpenAsyncArchive(testArchive);
        Assert.True(((SevenZipArchive)archive).IsSolid);

        await using var reader = await archive.ExtractAllEntriesAsync();
        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                await reader.WriteEntryToDirectoryAsync(SCRATCH_FILES_PATH);
            }
        }

        VerifyFiles();
    }

    [Fact]
    public async Task SevenZipArchive_Solid_VerifyStreamReuse()
    {
        // This test verifies that the folder stream is reused within each folder
        // and not recreated for each entry in solid archives
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.7z");
        await using var archive = await SevenZipArchive.OpenAsyncArchive(testArchive);
        Assert.True(((SevenZipArchive)archive).IsSolid);

        await using var reader = await archive.ExtractAllEntriesAsync();

        var sevenZipReader = Assert.IsType<SevenZipArchive.SevenZipReader>(reader);
        sevenZipReader.DiagnosticsEnabled = true;

        Stream? currentFolderStreamInstance = null;
        object? currentFolder = null;
        var entryCount = 0;
        var entriesInCurrentFolder = 0;
        var streamRecreationsWithinFolder = 0;

        while (await reader.MoveToNextEntryAsync())
        {
            if (!reader.Entry.IsDirectory)
            {
                // Extract the entry to trigger GetEntryStream
                using var entryStream = await reader.OpenEntryStreamAsync();
                var buffer = new byte[4096];
                while (entryStream.Read(buffer, 0, buffer.Length) > 0)
                {
                    // Read the stream to completion
                }

                entryCount++;

                var folderStream = sevenZipReader.DiagnosticsCurrentFolderStream;
                var folder = sevenZipReader.DiagnosticsCurrentFolder;

                Assert.NotNull(folderStream); // Folder stream should exist

                // Check if we're in a new folder
                if (currentFolder == null || !ReferenceEquals(currentFolder, folder))
                {
                    // Starting a new folder
                    currentFolder = folder;
                    currentFolderStreamInstance = folderStream;
                    entriesInCurrentFolder = 1;
                }
                else
                {
                    // Same folder - verify stream wasn't recreated
                    entriesInCurrentFolder++;

                    if (!ReferenceEquals(currentFolderStreamInstance, folderStream))
                    {
                        // Stream was recreated within the same folder - this is the bug we're testing for!
                        streamRecreationsWithinFolder++;
                    }

                    currentFolderStreamInstance = folderStream;
                }
            }
        }

        // Verify we actually tested multiple entries
        Assert.True(entryCount > 1, "Test should have multiple entries to verify stream reuse");

        // The critical check: within a single folder, the stream should NEVER be recreated
        Assert.Equal(0, streamRecreationsWithinFolder); // Folder stream should remain the same for all entries in the same folder
    }
}
