using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
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

public class CompressionProgressTests : TestBase
{
    [Fact]
    public void Zip_Write_ReportsProgress()
    {
        var progress = new TestProgress<CompressionProgress>();

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
        Assert.Equal(10000, lastReport.BytesRead);
        Assert.Equal(100.0, lastReport.PercentComplete);
    }

    [Fact]
    public void Tar_Write_ReportsProgress()
    {
        var progress = new TestProgress<CompressionProgress>();

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
        Assert.Equal(10000, lastReport.BytesRead);
        Assert.Equal(100.0, lastReport.PercentComplete);
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
    public void CompressionProgress_PercentComplete_WithUnknownTotalBytes_ReturnsNull()
    {
        var progress = new CompressionProgress("test.txt", 100, null);
        Assert.Null(progress.PercentComplete);
    }

    [Fact]
    public void CompressionProgress_PercentComplete_WithZeroTotalBytes_ReturnsNull()
    {
        var progress = new CompressionProgress("test.txt", 0, 0);
        Assert.Null(progress.PercentComplete);
    }

    [Fact]
    public void CompressionProgress_Properties_AreSetCorrectly()
    {
        var progress = new CompressionProgress("path/to/file.txt", 500, 1000);

        Assert.Equal("path/to/file.txt", progress.EntryPath);
        Assert.Equal(500, progress.BytesRead);
        Assert.Equal(1000, progress.TotalBytes);
        Assert.Equal(50.0, progress.PercentComplete);
    }

    [Fact]
    public async Task Tar_WriteAsync_ReportsProgress()
    {
        var progress = new TestProgress<CompressionProgress>();

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
        Assert.Equal(10000, lastReport.BytesRead);
    }
}
