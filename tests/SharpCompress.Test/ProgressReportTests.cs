using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test;

/// <summary>
/// A synchronous progress implementation for testing.
/// Unlike Progress&lt;T&gt;, this captures reports immediately without SynchronizationContext.
/// </summary>
internal sealed class TestProgress<T> : IProgress<T>
{
    private readonly List<T> _reports = new();

    public IReadOnlyList<T> Reports => _reports;

    public void Report(T value) => _reports.Add(value);
}

public class ProgressReportTests : TestBase
{
    [Fact]
    public void Zip_Write_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        using var archiveStream = new MemoryStream();
        var options = new ZipWriterOptions(CompressionType.Deflate) { Progress = progress };

        using (var writer = new ZipWriter(archiveStream, options))
        {
            var testData = new byte[10000];
            Array.Fill(testData, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("test.txt", p.EntryPath));
        Assert.All(progress.Reports, p => Assert.Equal(10000, p.TotalBytes));

        var lastReport = progress.Reports[^1];
        Assert.Equal(10000, lastReport.BytesTransferred);
        Assert.Equal(100.0, lastReport.PercentComplete);
    }

    [Fact]
    public void Tar_Write_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        using var archiveStream = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true) { Progress = progress };

        using (var writer = new TarWriter(archiveStream, options))
        {
            var testData = new byte[10000];
            Array.Fill(testData, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("test.txt", p.EntryPath));
        Assert.All(progress.Reports, p => Assert.Equal(10000, p.TotalBytes));

        var lastReport = progress.Reports[^1];
        Assert.Equal(10000, lastReport.BytesTransferred);
        Assert.Equal(100.0, lastReport.PercentComplete);
    }

    [Fact]
    public void Zip_Read_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        // First create a zip archive
        using var archiveStream = new MemoryStream();
        using (
            var writer = new ZipWriter(archiveStream, new ZipWriterOptions(CompressionType.Deflate))
        )
        {
            var testData = new byte[10000];
            Array.Fill(testData, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        // Now read it with progress reporting
        archiveStream.Position = 0;
        var readerOptions = new ReaderOptions { Progress = progress };

        using (var reader = ReaderFactory.Open(archiveStream, readerOptions))
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    using var extractedStream = new MemoryStream();
                    reader.WriteEntryTo(extractedStream);
                }
            }
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("test.txt", p.EntryPath));

        var lastReport = progress.Reports[^1];
        Assert.Equal(10000, lastReport.BytesTransferred);
    }

    [Fact]
    public void ZipArchive_Entry_WriteTo_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        // First create a zip archive
        using var archiveStream = new MemoryStream();
        using (
            var writer = new ZipWriter(archiveStream, new ZipWriterOptions(CompressionType.Deflate))
        )
        {
            var testData = new byte[10000];
            Array.Fill(testData, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        // Now open as archive and extract entry with progress
        archiveStream.Position = 0;
        var readerOptions = new ReaderOptions { Progress = progress };

        using var archive = ZipArchive.Open(archiveStream, readerOptions);
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                using var extractedStream = new MemoryStream();
                entry.WriteTo(extractedStream);
            }
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("test.txt", p.EntryPath));

        var lastReport = progress.Reports[^1];
        Assert.Equal(10000, lastReport.BytesTransferred);
    }

    [Fact]
    public async Task ZipArchive_Entry_WriteToAsync_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        // First create a zip archive
        using var archiveStream = new MemoryStream();
        using (
            var writer = new ZipWriter(archiveStream, new ZipWriterOptions(CompressionType.Deflate))
        )
        {
            var testData = new byte[10000];
            Array.Fill(testData, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        // Now open as archive and extract entry async with progress
        archiveStream.Position = 0;
        var readerOptions = new ReaderOptions { Progress = progress };

        using var archive = ZipArchive.Open(archiveStream, readerOptions);
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                using var extractedStream = new MemoryStream();
                await entry.WriteToAsync(extractedStream);
            }
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("test.txt", p.EntryPath));

        var lastReport = progress.Reports[^1];
        Assert.Equal(10000, lastReport.BytesTransferred);
    }

    [Fact]
    public void WriterOptions_WithoutProgress_DoesNotThrow()
    {
        using var archiveStream = new MemoryStream();
        var options = new ZipWriterOptions(CompressionType.Deflate);
        Assert.Null(options.Progress);

        using (var writer = new ZipWriter(archiveStream, options))
        {
            var testData = new byte[100];
            Array.Fill(testData, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        Assert.True(archiveStream.Length > 0);
    }

    [Fact]
    public void ReaderOptions_WithoutProgress_DoesNotThrow()
    {
        // First create a zip archive
        using var archiveStream = new MemoryStream();
        using (
            var writer = new ZipWriter(archiveStream, new ZipWriterOptions(CompressionType.Deflate))
        )
        {
            var testData = new byte[100];
            Array.Fill(testData, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        // Read without progress
        archiveStream.Position = 0;
        var readerOptions = new ReaderOptions();
        Assert.Null(readerOptions.Progress);

        using (var reader = ReaderFactory.Open(archiveStream, readerOptions))
        {
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory)
                {
                    using var extractedStream = new MemoryStream();
                    reader.WriteEntryTo(extractedStream);
                }
            }
        }
    }

    [Fact]
    public void ZipArchive_WithoutProgress_DoesNotThrow()
    {
        // First create a zip archive
        using var archiveStream = new MemoryStream();
        using (
            var writer = new ZipWriter(archiveStream, new ZipWriterOptions(CompressionType.Deflate))
        )
        {
            var testData = new byte[100];
            Array.Fill(testData, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        // Open archive and extract without progress
        archiveStream.Position = 0;

        using var archive = ZipArchive.Open(archiveStream);
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                using var extractedStream = new MemoryStream();
                entry.WriteTo(extractedStream);
            }
        }
    }

    [Fact]
    public void ProgressReport_PercentComplete_WithUnknownTotalBytes_ReturnsNull()
    {
        var progress = new ProgressReport("test.txt", 100, null);
        Assert.Null(progress.PercentComplete);
    }

    [Fact]
    public void ProgressReport_PercentComplete_WithZeroTotalBytes_ReturnsNull()
    {
        var progress = new ProgressReport("test.txt", 0, 0);
        Assert.Null(progress.PercentComplete);
    }

    [Fact]
    public void ProgressReport_Properties_AreSetCorrectly()
    {
        var progress = new ProgressReport("path/to/file.txt", 500, 1000);

        Assert.Equal("path/to/file.txt", progress.EntryPath);
        Assert.Equal(500, progress.BytesTransferred);
        Assert.Equal(1000, progress.TotalBytes);
        Assert.Equal(50.0, progress.PercentComplete);
    }

    [Fact]
    public async Task Tar_WriteAsync_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        using var archiveStream = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true) { Progress = progress };

        using (var writer = new TarWriter(archiveStream, options))
        {
            var testData = new byte[10000];
            Array.Fill(testData, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            await writer.WriteAsync("test.txt", sourceStream, DateTime.Now);
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("test.txt", p.EntryPath));

        var lastReport = progress.Reports[^1];
        Assert.Equal(10000, lastReport.BytesTransferred);
    }
}
