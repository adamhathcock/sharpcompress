#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;
using SysZip = System.IO.Compression.ZipArchive;
using SysZipMode = System.IO.Compression.ZipArchiveMode;

namespace SharpCompress.Test.Security;

public class ExtractionPathTraversalTests : TestBase
{
    [Theory]
    [InlineData("ReaderAll")]
    [InlineData("ReaderEntry")]
    [InlineData("Archive")]
    [InlineData("ArchiveEntry")]
    [InlineData("AsyncReaderAll")]
    [InlineData("AsyncReaderEntry")]
    [InlineData("AsyncArchive")]
    [InlineData("AsyncArchiveEntry")]
    public async Task DirectoryTraversalToExistingOutsideDirectory_ShouldThrow(string api)
    {
        var extractDir = Path.Combine(SCRATCH_FILES_PATH, "extract");
        Directory.CreateDirectory(extractDir);
        var escapedDirectory = Path.GetFullPath(Path.Combine(extractDir, "../../escaped_existing"));
        Directory.CreateDirectory(escapedDirectory);
        var archivePath = Path.Combine(SCRATCH2_FILES_PATH, $"{api}.zip");
        BuildZip(archivePath, "../../escaped_existing/");

        var exception = await RecordExtractionExceptionAsync(api, archivePath, extractDir);

        var extractionException = Assert.IsType<ExtractionException>(exception);
        Assert.Contains("outside of the destination", extractionException.Message);
    }

    [Theory]
    [InlineData("ReaderAll")]
    [InlineData("ReaderEntry")]
    [InlineData("Archive")]
    [InlineData("ArchiveEntry")]
    [InlineData("AsyncReaderAll")]
    [InlineData("AsyncReaderEntry")]
    [InlineData("AsyncArchive")]
    [InlineData("AsyncArchiveEntry")]
    public async Task FileTraversalToSiblingDirectory_ShouldThrow(string api)
    {
        var extractDir = Path.Combine(SCRATCH_FILES_PATH, "extract");
        Directory.CreateDirectory(extractDir);
        var siblingDirectory = Path.Combine(SCRATCH_FILES_PATH, "extract2");
        Directory.CreateDirectory(siblingDirectory);
        var archivePath = Path.Combine(SCRATCH2_FILES_PATH, $"{api}.zip");
        BuildZip(archivePath, "../extract2/evil.txt");

        var exception = await RecordExtractionExceptionAsync(api, archivePath, extractDir);

        var extractionException = Assert.IsType<ExtractionException>(exception);
        Assert.Contains("outside of the destination", extractionException.Message);
        Assert.False(File.Exists(Path.Combine(siblingDirectory, "evil.txt")));
    }

    private static void BuildZip(string path, string entryName)
    {
        using var fs = File.Create(path);
        using var zip = new SysZip(fs, SysZipMode.Create);
        var entry = zip.CreateEntry(entryName);

        if (entryName.EndsWith('/'))
        {
            return;
        }

        using var writer = new StreamWriter(entry.Open());
        writer.Write("evil");
    }

    private static async Task<Exception?> RecordExtractionExceptionAsync(
        string api,
        string archivePath,
        string extractDir
    )
    {
        var options = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };

        return api switch
        {
            "ReaderAll" => RecordException(() =>
                ExtractWithReaderAll(archivePath, extractDir, options)
            ),
            "ReaderEntry" => RecordException(() =>
                ExtractWithReaderEntry(archivePath, extractDir, options)
            ),
            "Archive" => RecordException(() =>
                ExtractWithArchive(archivePath, extractDir, options)
            ),
            "ArchiveEntry" => RecordException(() =>
                ExtractWithArchiveEntry(archivePath, extractDir, options)
            ),
            "AsyncReaderAll" => await RecordExceptionAsync(() =>
                ExtractWithAsyncReaderAllAsync(archivePath, extractDir, options)
            ),
            "AsyncReaderEntry" => await RecordExceptionAsync(() =>
                ExtractWithAsyncReaderEntryAsync(archivePath, extractDir, options)
            ),
            "AsyncArchive" => await RecordExceptionAsync(() =>
                ExtractWithAsyncArchiveAsync(archivePath, extractDir, options)
            ),
            "AsyncArchiveEntry" => await RecordExceptionAsync(() =>
                ExtractWithAsyncArchiveEntryAsync(archivePath, extractDir, options)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(api), api, null),
        };
    }

    private static Exception? RecordException(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<Exception?> RecordExceptionAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void ExtractWithReaderAll(
        string archivePath,
        string extractDir,
        ExtractionOptions options
    )
    {
        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream);
        reader.WriteAllToDirectory(extractDir, options);
    }

    private static void ExtractWithReaderEntry(
        string archivePath,
        string extractDir,
        ExtractionOptions options
    )
    {
        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream);
        Assert.True(reader.MoveToNextEntry());
        reader.WriteEntryToDirectory(extractDir, options);
    }

    private static void ExtractWithArchive(
        string archivePath,
        string extractDir,
        ExtractionOptions options
    )
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        archive.WriteToDirectory(extractDir, options);
    }

    private static void ExtractWithArchiveEntry(
        string archivePath,
        string extractDir,
        ExtractionOptions options
    )
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        archive.Entries.Single().WriteToDirectory(extractDir, options);
    }

    private static async Task ExtractWithAsyncReaderAllAsync(
        string archivePath,
        string extractDir,
        ExtractionOptions options
    )
    {
        using var stream = File.OpenRead(archivePath);
        await using var reader = await ReaderFactory.OpenAsyncReader(stream);
        await reader.WriteAllToDirectoryAsync(extractDir, options);
    }

    private static async Task ExtractWithAsyncReaderEntryAsync(
        string archivePath,
        string extractDir,
        ExtractionOptions options
    )
    {
        using var stream = File.OpenRead(archivePath);
        await using var reader = await ReaderFactory.OpenAsyncReader(stream);
        Assert.True(await reader.MoveToNextEntryAsync());
        await reader.WriteEntryToDirectoryAsync(extractDir, options);
    }

    private static async Task ExtractWithAsyncArchiveAsync(
        string archivePath,
        string extractDir,
        ExtractionOptions options
    )
    {
        await using var archive = await ArchiveFactory.OpenAsyncArchive(archivePath);
        await archive.WriteToDirectoryAsync(extractDir, options);
    }

    private static async Task ExtractWithAsyncArchiveEntryAsync(
        string archivePath,
        string extractDir,
        ExtractionOptions options
    )
    {
        await using var archive = await ArchiveFactory.OpenAsyncArchive(archivePath);

        await foreach (var entry in archive.EntriesAsync)
        {
            await entry.WriteToDirectoryAsync(extractDir, options);
            return;
        }

        throw new InvalidOperationException("Archive did not contain an entry.");
    }
}
#endif
