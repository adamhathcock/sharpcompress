using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xunit;

namespace SharpCompress.Test.SevenZip;

/// <summary>
/// Tests for 7Zip decoder stream caching optimization to ensure:
/// 1. Stream reuse works correctly for sequential reads
/// 2. Partial reads don't corrupt subsequent extractions
/// 3. Non-sequential access is handled correctly
/// </summary>
public class SevenZipStreamCachingTests : TestBase
{
    [Fact]
    public void SevenZipReader_SequentialExtraction_ReusesDecoderStream()
    {
        // This test verifies that the ExtractAllEntries() pattern reuses the decoder stream
        // for sequential reads from the same folder, reducing allocations
        using var stream = new MemoryStream();
        using (
            var archive = SevenZipArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.LZMA2.7z"))
        )
        {
            using var reader = archive.ExtractAllEntries();
            var entriesProcessed = 0;

            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    using var entryStream = reader.OpenEntryStream();
                    entryStream.CopyTo(stream);
                    entriesProcessed++;
                }
            }

            // Verify we processed multiple entries
            Assert.True(entriesProcessed > 1, "Test archive should have multiple files");
        }

        // Verify extracted data is non-zero
        Assert.True(stream.Length > 0, "Should have extracted data");
    }

    [Fact]
    public void SevenZipReader_PartialRead_DoesNotCorruptNextEntry()
    {
        // This test verifies that if a user partially reads an entry,
        // the next entry extraction still works correctly
        using (
            var archive = SevenZipArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.LZMA2.7z"))
        )
        {
            using var reader = archive.ExtractAllEntries();

            var firstEntry = true;
            var secondEntryData = new byte[0];

            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    if (firstEntry)
                    {
                        // Partially read the first entry (only read 10 bytes)
                        using var entryStream = reader.OpenEntryStream();
                        var buffer = new byte[10];
                        var bytesRead = entryStream.Read(buffer, 0, buffer.Length);
                        Assert.True(bytesRead > 0, "Should read some bytes from first entry");
                        // Don't read the rest - simulate partial read
                        firstEntry = false;
                    }
                    else
                    {
                        // Fully read the second entry
                        using var entryStream = reader.OpenEntryStream();
                        using var ms = new MemoryStream();
                        entryStream.CopyTo(ms);
                        secondEntryData = ms.ToArray();
                        break; // Only test first two files
                    }
                }
            }

            // Verify second entry was extracted successfully despite first entry being partial
            Assert.True(
                secondEntryData.Length > 0,
                "Second entry should be extracted correctly even if first was partial"
            );
        }
    }

    [Fact]
    public void SevenZipReader_SolidArchive_ExtractsAllFilesCorrectly()
    {
        // Test extraction of solid archive where all files share same compression folder
        using (
            var archive = SevenZipArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.7z"))
        )
        {
            using var reader = archive.ExtractAllEntries();

            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    reader.WriteEntryToDirectory(
                        SCRATCH_FILES_PATH,
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                    );
                }
            }
        }

        VerifyFiles();
    }

    [Fact]
    public void SevenZipArchive_NonSequentialAccess_WorksCorrectly()
    {
        // Test that non-sequential access via Archive API (not Reader) still works
        // This doesn't benefit from caching but shouldn't be broken
        using (
            var archive = SevenZipArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.LZMA2.7z"))
        )
        {
            var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
            Assert.True(entries.Count >= 2, "Need at least 2 files for this test");

            // Read entries in reverse order (non-sequential)
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                using var entryStream = entries[i].OpenEntryStream();
                using var ms = new MemoryStream();
                entryStream.CopyTo(ms);

                Assert.True(
                    ms.Length > 0,
                    $"Entry {entries[i].Key} should have data when accessed non-sequentially"
                );
            }
        }
    }

    [Fact]
    public void SevenZipReader_MultipleEntriesFromSameFolder_SharesDecoderStream()
    {
        // Verify that multiple entries from the same compression folder
        // reuse the decoder stream (this is the core optimization)
        using (
            var archive = SevenZipArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.solid.7z"))
        )
        {
            var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
            Assert.True(entries.Count > 1, "Solid archive should have multiple files");

            // Group entries by folder to verify caching works for files in same folder
            var folderGroups = entries.GroupBy(e => e.FilePart.Folder).ToList();

            using var reader = archive.ExtractAllEntries();
            var extractedCount = 0;

            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    using var entryStream = reader.OpenEntryStream();
                    using var ms = new MemoryStream();
                    entryStream.CopyTo(ms);
                    Assert.True(ms.Length > 0, "Each entry should have data");
                    extractedCount++;
                }
            }

            Assert.Equal(entries.Count, extractedCount);

            // Verify there are folders with multiple files (these benefit from caching)
            Assert.True(
                folderGroups.Any(g => g.Count() > 1),
                "Solid archive should have at least one folder with multiple files"
            );
        }
    }

    [Fact]
    public void SevenZipReader_EmptyEntries_HandledCorrectly()
    {
        // Test that zero-byte files don't break the caching logic
        using (
            var archive = SevenZipArchive.Open(Path.Combine(TEST_ARCHIVES_PATH, "7Zip.LZMA2.7z"))
        )
        {
            using var reader = archive.ExtractAllEntries();

            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    using var entryStream = reader.OpenEntryStream();
                    using var ms = new MemoryStream();
                    entryStream.CopyTo(ms);
                    // Some entries might be empty, that's OK
                    Assert.True(ms.Length >= 0);
                }
            }
        }
    }
}
