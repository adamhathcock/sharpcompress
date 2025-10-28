using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Zip;

public class ZipArchiveDirectoryTests : TestBase
{
    [Fact]
    public void ZipArchive_AddDirectoryEntry_CreatesDirectoryEntry()
    {
        using var archive = ZipArchive.Create();

        archive.AddDirectoryEntry("test-dir", DateTime.Now);

        var entries = archive.Entries.ToList();
        Assert.Single(entries);
        Assert.Equal("test-dir", entries[0].Key);
        Assert.True(entries[0].IsDirectory);
    }

    [Fact]
    public void ZipArchive_AddDirectoryEntry_MultipleDirectories()
    {
        using var archive = ZipArchive.Create();

        archive.AddDirectoryEntry("dir1", DateTime.Now);
        archive.AddDirectoryEntry("dir2", DateTime.Now);
        archive.AddDirectoryEntry("dir1/subdir", DateTime.Now);

        var entries = archive.Entries.OrderBy(e => e.Key).ToList();
        Assert.Equal(3, entries.Count);
        Assert.True(entries.All(e => e.IsDirectory));
    }

    [Fact]
    public void ZipArchive_AddDirectoryEntry_MixedWithFiles()
    {
        using var archive = ZipArchive.Create();

        archive.AddDirectoryEntry("dir1", DateTime.Now);

        using var contentStream = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes("test content")
        );
        archive.AddEntry("dir1/file.txt", contentStream, false, contentStream.Length, DateTime.Now);

        archive.AddDirectoryEntry("dir2", DateTime.Now);

        var entries = archive.Entries.OrderBy(e => e.Key).ToList();
        Assert.Equal(3, entries.Count);
        Assert.True(entries[0].IsDirectory);
        Assert.False(entries[1].IsDirectory);
        Assert.True(entries[2].IsDirectory);
    }

    [Fact]
    public void ZipArchive_AddDirectoryEntry_SaveAndReload()
    {
        var scratchPath = Path.Combine(SCRATCH_FILES_PATH, "zip-directory-test.zip");

        using (var archive = ZipArchive.Create())
        {
            archive.AddDirectoryEntry("dir1", DateTime.Now);
            archive.AddDirectoryEntry("dir2", DateTime.Now);

            using var contentStream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("test content")
            );
            archive.AddEntry(
                "dir1/file.txt",
                contentStream,
                false,
                contentStream.Length,
                DateTime.Now
            );

            using (var fileStream = File.Create(scratchPath))
            {
                archive.SaveTo(fileStream, CompressionType.Deflate);
            }
        }

        using (var archive = ZipArchive.Open(scratchPath))
        {
            var entries = archive.Entries.OrderBy(e => e.Key).ToList();
            Assert.Equal(3, entries.Count);

            Assert.Equal("dir1/", entries[0].Key);
            Assert.True(entries[0].IsDirectory);

            Assert.Equal("dir1/file.txt", entries[1].Key);
            Assert.False(entries[1].IsDirectory);

            Assert.Equal("dir2/", entries[2].Key);
            Assert.True(entries[2].IsDirectory);
        }
    }

    [Fact]
    public void ZipArchive_AddDirectoryEntry_DuplicateKey_ThrowsException()
    {
        using var archive = ZipArchive.Create();

        archive.AddDirectoryEntry("test-dir", DateTime.Now);

        Assert.Throws<ArchiveException>(() => archive.AddDirectoryEntry("test-dir", DateTime.Now));
    }
}
