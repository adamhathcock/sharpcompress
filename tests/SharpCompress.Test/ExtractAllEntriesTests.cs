using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test;

/// <summary>
/// Tests for the ExtractAllEntries method behavior on both solid and non-solid
/// archives, including progress reporting and current usage restrictions.
/// </summary>
public class ExtractAllEntriesTests : TestBase
{
    [Fact]
    public void ExtractAllEntries_WithProgressReporting_NonSolidArchive()
    {
        var archivePath = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip");

        using var archive = ArchiveFactory.Open(archivePath);
        Assert.Throws<SharpCompressException>(() =>
        {
            using var reader = archive.ExtractAllEntries();
        });
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
}
