using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test.Tar;

public class TarWriterDirectoryTests : TestBase
{
    [Fact]
    public void TarWriter_WriteDirectory_CreatesDirectoryEntry()
    {
        using var memoryStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                memoryStream,
                new TarWriterOptions(CompressionType.None, true)
            )
        )
        {
            writer.WriteDirectory("test-dir", DateTime.Now);
        }

        memoryStream.Position = 0;
        using var archive = TarArchive.Open(memoryStream);
        var entries = archive.Entries.ToList();

        Assert.Single(entries);
        Assert.Equal("test-dir/", entries[0].Key);
        Assert.True(entries[0].IsDirectory);
    }

    [Fact]
    public void TarWriter_WriteDirectory_WithTrailingSlash()
    {
        using var memoryStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                memoryStream,
                new TarWriterOptions(CompressionType.None, true)
            )
        )
        {
            writer.WriteDirectory("test-dir/", DateTime.Now);
        }

        memoryStream.Position = 0;
        using var archive = TarArchive.Open(memoryStream);
        var entries = archive.Entries.ToList();

        Assert.Single(entries);
        Assert.Equal("test-dir/", entries[0].Key);
        Assert.True(entries[0].IsDirectory);
    }

    [Fact]
    public void TarWriter_WriteDirectory_WithBackslash()
    {
        using var memoryStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                memoryStream,
                new TarWriterOptions(CompressionType.None, true)
            )
        )
        {
            writer.WriteDirectory("test-dir\\subdir", DateTime.Now);
        }

        memoryStream.Position = 0;
        using var archive = TarArchive.Open(memoryStream);
        var entries = archive.Entries.ToList();

        Assert.Single(entries);
        Assert.Equal("test-dir/subdir/", entries[0].Key);
        Assert.True(entries[0].IsDirectory);
    }

    [Fact]
    public void TarWriter_WriteDirectory_EmptyString_IsSkipped()
    {
        using var memoryStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                memoryStream,
                new TarWriterOptions(CompressionType.None, true)
            )
        )
        {
            writer.WriteDirectory("", DateTime.Now);
        }

        memoryStream.Position = 0;
        using var archive = TarArchive.Open(memoryStream);

        Assert.Empty(archive.Entries);
    }

    [Fact]
    public void TarWriter_WriteDirectory_MultipleDirectories()
    {
        using var memoryStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                memoryStream,
                new TarWriterOptions(CompressionType.None, true)
            )
        )
        {
            writer.WriteDirectory("dir1", DateTime.Now);
            writer.WriteDirectory("dir2", DateTime.Now);
            writer.WriteDirectory("dir1/subdir", DateTime.Now);
        }

        memoryStream.Position = 0;
        using var archive = TarArchive.Open(memoryStream);
        var entries = archive.Entries.OrderBy(e => e.Key).ToList();

        Assert.Equal(3, entries.Count);
        Assert.Equal("dir1/", entries[0].Key);
        Assert.True(entries[0].IsDirectory);
        Assert.Equal("dir1/subdir/", entries[1].Key);
        Assert.True(entries[1].IsDirectory);
        Assert.Equal("dir2/", entries[2].Key);
        Assert.True(entries[2].IsDirectory);
    }

    [Fact]
    public void TarWriter_WriteDirectory_MixedWithFiles()
    {
        using var memoryStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                memoryStream,
                new TarWriterOptions(CompressionType.None, true)
            )
        )
        {
            writer.WriteDirectory("dir1", DateTime.Now);

            using var contentStream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("test content")
            );
            writer.Write("dir1/file.txt", contentStream, DateTime.Now);

            writer.WriteDirectory("dir2", DateTime.Now);
        }

        memoryStream.Position = 0;
        using var archive = TarArchive.Open(memoryStream);
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
