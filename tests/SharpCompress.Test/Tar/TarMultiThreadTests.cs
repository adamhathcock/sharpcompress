using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarMultiThreadTests : TestBase
{
    [Fact]
    public void Tar_Archive_Concurrent_Extraction_From_FileInfo()
    {
        // Test concurrent extraction of multiple entries from a Tar archive opened from FileInfo
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar");
        var fileInfo = new FileInfo(testArchive);

        using var archive = TarArchive.OpenArchive(fileInfo);
        var entries = archive.Entries.Where(e => !e.IsDirectory).Take(5).ToList();

        // Extract multiple entries concurrently
        var tasks = new List<Task>();
        var outputFiles = new List<string>();

        foreach (var entry in entries)
        {
            var outputFile = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            outputFiles.Add(outputFile);

            tasks.Add(
                Task.Run(() =>
                {
                    var dir = Path.GetDirectoryName(outputFile);
                    if (dir != null)
                    {
                        Directory.CreateDirectory(dir);
                    }
                    using var entryStream = entry.OpenEntryStream();
                    using var fileStream = File.Create(outputFile);
                    entryStream.CopyTo(fileStream);
                })
            );
        }

        Task.WaitAll(tasks.ToArray());

        // Verify all files were extracted
        Assert.Equal(entries.Count, outputFiles.Count);
        foreach (var outputFile in outputFiles)
        {
            Assert.True(File.Exists(outputFile), $"File {outputFile} should exist");
        }
    }

    [Fact]
    public async Task Tar_Archive_Concurrent_Extraction_From_FileInfo_Async()
    {
        // Test concurrent async extraction of multiple entries from a Tar archive opened from FileInfo
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Tar.tar");
        var fileInfo = new FileInfo(testArchive);

        using var archive = TarArchive.OpenArchive(fileInfo);
        var entries = archive.Entries.Where(e => !e.IsDirectory).Take(5).ToList();

        // Extract multiple entries concurrently
        var tasks = new List<Task>();
        var outputFiles = new List<string>();

        foreach (var entry in entries)
        {
            var outputFile = Path.Combine(SCRATCH_FILES_PATH, entry.Key!);
            outputFiles.Add(outputFile);

            tasks.Add(
                Task.Run(async () =>
                {
                    var dir = Path.GetDirectoryName(outputFile);
                    if (dir != null)
                    {
                        Directory.CreateDirectory(dir);
                    }
                    using var entryStream = await entry.OpenEntryStreamAsync();
                    using var fileStream = File.Create(outputFile);
                    await entryStream.CopyToAsync(fileStream);
                })
            );
        }

        await Task.WhenAll(tasks);

        // Verify all files were extracted
        Assert.Equal(entries.Count, outputFiles.Count);
        foreach (var outputFile in outputFiles)
        {
            Assert.True(File.Exists(outputFile), $"File {outputFile} should exist");
        }
    }
}
