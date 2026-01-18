using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipMultiThreadTests : TestBase
{
    [Fact]
    public void Zip_Archive_Concurrent_Extraction_From_FileInfo()
    {
        // Test concurrent extraction of multiple entries from a Zip archive opened from FileInfo
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Zip.none.zip");
        var fileInfo = new FileInfo(testArchive);

        using var archive = ZipArchive.OpenArchive(fileInfo);
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
    public async Task Zip_Archive_Concurrent_Extraction_From_FileInfo_Async()
    {
        // Test concurrent async extraction of multiple entries from a Zip archive opened from FileInfo
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Zip.none.zip");
        var fileInfo = new FileInfo(testArchive);

        using var archive = ZipArchive.OpenArchive(fileInfo);
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

    [Fact]
    public void Zip_Archive_Concurrent_Extraction_From_Path()
    {
        // Test concurrent extraction when opening from path (should use FileInfo internally)
        var testArchive = Path.Combine(TEST_ARCHIVES_PATH, "Zip.none.zip");

        using var archive = ZipArchive.OpenArchive(testArchive);
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
}
