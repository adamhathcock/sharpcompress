using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test;

/// <summary>
/// Tests for ExtractAllEntries method which should work for all archive types
/// regardless of whether they are SOLID or not.
/// </summary>
public class ExtractAllEntriesTests : TestBase
{
    [Theory]
    [InlineData("Zip.deflate.zip", false)]
    [InlineData("Zip.bzip2.zip", false)]
    [InlineData("Zip.lzma.zip", false)]
    [InlineData("Zip.ppmd.zip", false)]
    [InlineData("Rar.rar", false)]
    [InlineData("Rar.solid.rar", true)]
    [InlineData("7Zip.LZMA.7z", true)]
    [InlineData("7Zip.PPMd.7z", true)]
    [InlineData("Tar.tar", false)]
    public void ExtractAllEntries_WorksForAllArchiveTypes(string archiveName, bool expectedSolid)
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, archiveName);

        using var archive = ArchiveFactory.Open(archivePath);

        // Verify IsSolid matches expectation
        Assert.Equal(expectedSolid, archive.IsSolid);

        // This should not throw for any archive type
        using var reader = archive.ExtractAllEntries();

        var entryCount = 0;
        var filesExtracted = 0;
        while (reader.MoveToNextEntry())
        {
            entryCount++;
            if (!reader.Entry.IsDirectory)
            {
                filesExtracted++;
                // Extract to scratch path to verify it actually works
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
        }

        Assert.True(entryCount > 0, $"Archive {archiveName} should have at least one entry");
        Assert.True(
            filesExtracted > 0,
            $"Archive {archiveName} should have at least one file entry"
        );
    }

    [Fact]
    public void ExtractAllEntries_WithProgressReporting_NonSolidArchive()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip");

        using var archive = ArchiveFactory.Open(archivePath);

        // Calculate total size like user code does
        double totalSize = archive.Entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
        long completed = 0;
        var progressReports = 0;

        using var reader = archive.ExtractAllEntries();
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );

                completed += reader.Entry.Size;
                var progress = completed / totalSize;
                progressReports++;

                Assert.True(progress >= 0 && progress <= 1.0);
            }
        }

        Assert.True(progressReports > 0);
    }

    [Fact]
    public void ExtractAllEntries_WithProgressReporting_SolidArchive()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Rar.solid.rar");

        using var archive = ArchiveFactory.Open(archivePath);
        Assert.True(archive.IsSolid);

        // Calculate total size like user code does
        double totalSize = archive.Entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
        long completed = 0;
        var progressReports = 0;

        using var reader = archive.ExtractAllEntries();
        while (reader.MoveToNextEntry())
        {
            if (!reader.Entry.IsDirectory)
            {
                reader.WriteEntryToDirectory(
                    SCRATCH_FILES_PATH,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );

                completed += reader.Entry.Size;
                var progress = completed / totalSize;
                progressReports++;

                Assert.True(progress >= 0 && progress <= 1.0);
            }
        }

        Assert.True(progressReports > 0);
    }

    [Fact]
    public void ExtractAllEntries_UserScenario_UnknownArchiveType()
    {
        // Test the exact user scenario: they don't know if archive is SOLID or not
        // and want to use ExtractAllEntries with progress reporting
        var testArchives = new[]
        {
            "Zip.deflate.zip", // Non-SOLID
            "Rar.rar", // Non-SOLID
            "Rar.solid.rar", // SOLID
            "7Zip.LZMA.7z", // SOLID (all 7z are treated as SOLID)
        };

        foreach (var archiveName in testArchives)
        {
            var archivePath = Path.Combine(TEST_ARCHIVES_PATH, archiveName);

            // User code pattern
            var options = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };

            Directory.CreateDirectory(SCRATCH_FILES_PATH);

            using var archive = ArchiveFactory.Open(archivePath);
            double totalSize = archive.Entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
            long completed = 0;

            // This should work regardless of archive type
            using var reader = archive.ExtractAllEntries();
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    reader.WriteEntryToDirectory(SCRATCH_FILES_PATH, options);

                    completed += reader.Entry.Size;
                    var progress = completed / totalSize;
                    // In real code, user would invoke progress callback here
                    Assert.True(progress >= 0 && progress <= 1.0);
                }
            }

            // Clean up for next iteration
            if (Directory.Exists(SCRATCH_FILES_PATH))
            {
                Directory.Delete(SCRATCH_FILES_PATH, true);
                Directory.CreateDirectory(SCRATCH_FILES_PATH);
            }
        }
    }
}
