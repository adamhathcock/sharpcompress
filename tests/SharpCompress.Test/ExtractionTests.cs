using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test;

public class ExtractionTests : TestBase
{
    [Fact]
    public void Extraction_ShouldHandleCaseInsensitivePathsOnWindows()
    {
        // This test simulates the issue where Path.GetFullPath returns paths with different casing
        // than the actual directory on disk (e.g., "system32" vs "System32" on Windows).
        // On Windows, file paths are case-insensitive, so the extraction should succeed.
        // On Unix-like systems, file paths are case-sensitive, so this test validates the
        // platform-specific behavior.

        var testArchive = Path.Combine(SCRATCH2_FILES_PATH, "test-extraction.zip");
        var extractPath = SCRATCH_FILES_PATH;

        // Create a simple test archive with a single file
        using (var stream = File.Create(testArchive))
        {
            using var writer = (ZipWriter)
                WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate);

            // Create a test file to add to the archive
            var testFilePath = Path.Combine(SCRATCH2_FILES_PATH, "testfile.txt");
            File.WriteAllText(testFilePath, "Test content");

            writer.Write("testfile.txt", testFilePath);
        }

        // Extract the archive - this should succeed regardless of path casing
        using (var stream = File.OpenRead(testArchive))
        {
            using var reader = ReaderFactory.Open(stream);

            // This should not throw an exception even if Path.GetFullPath returns
            // a path with different casing than the actual directory
            var exception = Record.Exception(() =>
                reader.WriteAllToDirectory(
                    extractPath,
                    new ExtractionOptions { ExtractFullPath = false, Overwrite = true }
                )
            );

            Assert.Null(exception);
        }

        // Verify the file was extracted successfully
        var extractedFile = Path.Combine(extractPath, "testfile.txt");
        Assert.True(File.Exists(extractedFile));
        Assert.Equal("Test content", File.ReadAllText(extractedFile));
    }

    [Fact]
    public void Extraction_ShouldPreventPathTraversalAttacks()
    {
        // This test ensures that the security check still works to prevent
        // path traversal attacks (e.g., using "../" to escape the destination directory)

        var testArchive = Path.Combine(SCRATCH2_FILES_PATH, "test-traversal.zip");
        var extractPath = SCRATCH_FILES_PATH;

        // Create a test archive with a path traversal attempt
        using (var stream = File.Create(testArchive))
        {
            using var writer = (ZipWriter)
                WriterFactory.Open(stream, ArchiveType.Zip, CompressionType.Deflate);

            var testFilePath = Path.Combine(SCRATCH2_FILES_PATH, "testfile2.txt");
            File.WriteAllText(testFilePath, "Test content");

            // Try to write with a path that attempts to escape the destination directory
            writer.Write("../../evil.txt", testFilePath);
        }

        // Extract the archive - this should throw an exception for path traversal
        using (var stream = File.OpenRead(testArchive))
        {
            using var reader = ReaderFactory.Open(stream);

            var exception = Assert.Throws<ExtractionException>(() =>
                reader.WriteAllToDirectory(
                    extractPath,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                )
            );

            Assert.Contains("outside of the destination", exception.Message);
        }
    }
}
