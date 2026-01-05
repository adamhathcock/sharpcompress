using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
    private static byte[] CreateTestData(int size, byte fillValue)
    {
        var data = new byte[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = fillValue;
        }
        return data;
    }

    [Fact]
    public void Zip_Write_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        using var archiveStream = new MemoryStream();
        var options = new ZipWriterOptions(CompressionType.Deflate) { Progress = progress };

        using (var writer = new ZipWriter(archiveStream, options))
        {
            var testData = CreateTestData(10000, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("test.txt", p.EntryPath));
        Assert.All(progress.Reports, p => Assert.Equal(10000, p.TotalBytes));

        var lastReport = progress.Reports[progress.Reports.Count - 1];
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
            var testData = CreateTestData(10000, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("test.txt", p.EntryPath));
        Assert.All(progress.Reports, p => Assert.Equal(10000, p.TotalBytes));

        var lastReport = progress.Reports[progress.Reports.Count - 1];
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
            var testData = CreateTestData(10000, (byte)'A');
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

        var lastReport = progress.Reports[progress.Reports.Count - 1];
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
            var testData = CreateTestData(10000, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        // Now open as archive and extract entry with progress as parameter
        archiveStream.Position = 0;

        using var archive = ZipArchive.Open(archiveStream);
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                using var extractedStream = new MemoryStream();
                entry.WriteTo(extractedStream, progress);
            }
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("test.txt", p.EntryPath));

        var lastReport = progress.Reports[progress.Reports.Count - 1];
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
            var testData = CreateTestData(10000, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("test.txt", sourceStream, DateTime.Now);
        }

        // Now open as archive and extract entry async with progress as parameter
        archiveStream.Position = 0;

        using var archive = ZipArchive.Open(archiveStream);
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                using var extractedStream = new MemoryStream();
                await entry.WriteToAsync(extractedStream, progress, CancellationToken.None);
            }
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("test.txt", p.EntryPath));

        var lastReport = progress.Reports[progress.Reports.Count - 1];
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
            var testData = CreateTestData(100, (byte)'A');
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
            var testData = CreateTestData(100, (byte)'A');
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
            var testData = CreateTestData(100, (byte)'A');
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
    public void Tar_Read_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        // Create a tar archive first
        using var archiveStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                archiveStream,
                new TarWriterOptions(CompressionType.None, true)
            )
        )
        {
            var testData = CreateTestData(10000, (byte)'B');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("data.bin", sourceStream, DateTime.Now);
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
        Assert.All(progress.Reports, p => Assert.Equal("data.bin", p.EntryPath));

        var lastReport = progress.Reports[progress.Reports.Count - 1];
        Assert.Equal(10000, lastReport.BytesTransferred);
    }

    [Fact]
    public void TarArchive_Entry_WriteTo_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        // Create a tar archive first
        using var archiveStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                archiveStream,
                new TarWriterOptions(CompressionType.None, true)
            )
        )
        {
            var testData = CreateTestData(10000, (byte)'C');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("file.dat", sourceStream, DateTime.Now);
        }

        // Now open as archive and extract entry with progress as parameter
        archiveStream.Position = 0;

        using var archive = SharpCompress.Archives.Tar.TarArchive.Open(archiveStream);
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                using var extractedStream = new MemoryStream();
                entry.WriteTo(extractedStream, progress);
            }
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("file.dat", p.EntryPath));

        var lastReport = progress.Reports[progress.Reports.Count - 1];
        Assert.Equal(10000, lastReport.BytesTransferred);
    }

    [Fact]
    public async Task TarArchive_Entry_WriteToAsync_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        // Create a tar archive first
        using var archiveStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                archiveStream,
                new TarWriterOptions(CompressionType.None, true)
            )
        )
        {
            var testData = CreateTestData(10000, (byte)'D');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("async.dat", sourceStream, DateTime.Now);
        }

        // Now open as archive and extract entry async with progress as parameter
        archiveStream.Position = 0;

        using var archive = SharpCompress.Archives.Tar.TarArchive.Open(archiveStream);
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                using var extractedStream = new MemoryStream();
                await entry.WriteToAsync(extractedStream, progress, CancellationToken.None);
            }
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("async.dat", p.EntryPath));

        var lastReport = progress.Reports[progress.Reports.Count - 1];
        Assert.Equal(10000, lastReport.BytesTransferred);
    }

    [Fact]
    public void Zip_Read_MultipleEntries_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        // Create a zip archive with multiple entries
        using var archiveStream = new MemoryStream();
        using (
            var writer = new ZipWriter(archiveStream, new ZipWriterOptions(CompressionType.Deflate))
        )
        {
            var testData1 = CreateTestData(5000, (byte)'A');
            using var sourceStream1 = new MemoryStream(testData1);
            writer.Write("file1.txt", sourceStream1, DateTime.Now);

            var testData2 = CreateTestData(8000, (byte)'B');
            using var sourceStream2 = new MemoryStream(testData2);
            writer.Write("file2.txt", sourceStream2, DateTime.Now);
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

        // Should have reports for both files
        var file1Reports = progress.Reports.Where(p => p.EntryPath == "file1.txt").ToList();
        var file2Reports = progress.Reports.Where(p => p.EntryPath == "file2.txt").ToList();

        Assert.NotEmpty(file1Reports);
        Assert.NotEmpty(file2Reports);

        // Verify final bytes for each file
        Assert.Equal(5000, file1Reports[file1Reports.Count - 1].BytesTransferred);
        Assert.Equal(8000, file2Reports[file2Reports.Count - 1].BytesTransferred);
    }

    [Fact]
    public void ZipArchive_MultipleEntries_WriteTo_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        // Create a zip archive with multiple entries
        using var archiveStream = new MemoryStream();
        using (
            var writer = new ZipWriter(archiveStream, new ZipWriterOptions(CompressionType.Deflate))
        )
        {
            var testData1 = CreateTestData(5000, (byte)'A');
            using var sourceStream1 = new MemoryStream(testData1);
            writer.Write("entry1.txt", sourceStream1, DateTime.Now);

            var testData2 = CreateTestData(7000, (byte)'B');
            using var sourceStream2 = new MemoryStream(testData2);
            writer.Write("entry2.txt", sourceStream2, DateTime.Now);
        }

        // Now open as archive and extract entries with progress as parameter
        archiveStream.Position = 0;

        using var archive = ZipArchive.Open(archiveStream);
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                using var extractedStream = new MemoryStream();
                entry.WriteTo(extractedStream, progress);
            }
        }

        Assert.NotEmpty(progress.Reports);

        // Should have reports for both files
        var entry1Reports = progress.Reports.Where(p => p.EntryPath == "entry1.txt").ToList();
        var entry2Reports = progress.Reports.Where(p => p.EntryPath == "entry2.txt").ToList();

        Assert.NotEmpty(entry1Reports);
        Assert.NotEmpty(entry2Reports);

        // Verify final bytes for each entry
        Assert.Equal(5000, entry1Reports[entry1Reports.Count - 1].BytesTransferred);
        Assert.Equal(7000, entry2Reports[entry2Reports.Count - 1].BytesTransferred);
    }

    [Fact]
    public async Task Zip_ReadAsync_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        // Create a zip archive
        using var archiveStream = new MemoryStream();
        using (
            var writer = new ZipWriter(archiveStream, new ZipWriterOptions(CompressionType.Deflate))
        )
        {
            var testData = CreateTestData(10000, (byte)'E');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("async_read.txt", sourceStream, DateTime.Now);
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
                    await reader.WriteEntryToAsync(extractedStream);
                }
            }
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("async_read.txt", p.EntryPath));

        var lastReport = progress.Reports[progress.Reports.Count - 1];
        Assert.Equal(10000, lastReport.BytesTransferred);
    }

    [Fact]
    public void GZip_Write_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        using var archiveStream = new MemoryStream();
        var options = new SharpCompress.Writers.GZip.GZipWriterOptions { Progress = progress };

        using (var writer = new SharpCompress.Writers.GZip.GZipWriter(archiveStream, options))
        {
            var testData = CreateTestData(10000, (byte)'G');
            using var sourceStream = new MemoryStream(testData);
            writer.Write("gzip_test.txt", sourceStream, DateTime.Now);
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("gzip_test.txt", p.EntryPath));
        Assert.All(progress.Reports, p => Assert.Equal(10000, p.TotalBytes));

        var lastReport = progress.Reports[progress.Reports.Count - 1];
        Assert.Equal(10000, lastReport.BytesTransferred);
        Assert.Equal(100.0, lastReport.PercentComplete);
    }

    [Fact]
    public async Task Tar_WriteAsync_ReportsProgress()
    {
        var progress = new TestProgress<ProgressReport>();

        using var archiveStream = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.None, true) { Progress = progress };

        using (var writer = new TarWriter(archiveStream, options))
        {
            var testData = CreateTestData(10000, (byte)'A');
            using var sourceStream = new MemoryStream(testData);
            await writer.WriteAsync("test.txt", sourceStream, DateTime.Now);
        }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, p => Assert.Equal("test.txt", p.EntryPath));

        var lastReport = progress.Reports[progress.Reports.Count - 1];
        Assert.Equal(10000, lastReport.BytesTransferred);
    }
}
