using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test;

public class OptionsUsabilityTests : TestBase
{
    [Fact]
    public void ReaderFactory_Stream_Default_Leaves_Stream_Open()
    {
        using var file = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"));
        using var testStream = new TestStream(file);

        using (var reader = ReaderFactory.OpenReader(testStream))
        {
            reader.MoveToNextEntry();
        }

        Assert.False(testStream.IsDisposed);
    }

    [Fact]
    public void ArchiveFactory_Stream_Default_Leaves_Stream_Open()
    {
        using var file = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"));
        using var testStream = new TestStream(file);

        using (var archive = ArchiveFactory.OpenArchive(testStream))
        {
            _ = archive.Entries;
        }

        Assert.False(testStream.IsDisposed);
    }

    [Fact]
    public async Task ReaderFactory_Stream_Default_Leaves_Stream_Open_Async()
    {
        using var file = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"));
        using var testStream = new TestStream(file);

        await using (
            var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(testStream))
        )
        {
            await reader.MoveToNextEntryAsync();
        }

        Assert.False(testStream.IsDisposed);
    }

    [Fact]
    public void WriterOptions_Invalid_CompressionLevels_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WriterOptions(CompressionType.Deflate, 10)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WriterOptions(CompressionType.ZStandard, 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WriterOptions(CompressionType.BZip2, 1)
        );
    }

    [Fact]
    public void ZipWriterOptions_Invalid_CompressionLevels_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ZipWriterOptions(CompressionType.Deflate, 10)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ZipWriterOptions(CompressionType.ZStandard, 23)
        );
    }

    [Fact]
    public void GZipWriterOptions_Invalid_Settings_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GZipWriterOptions(10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GZipWriterOptions { CompressionType = CompressionType.Deflate }
        );
    }

    [Fact]
    public void ZipWriterEntryOptions_Invalid_CompressionLevel_Throws()
    {
        using var destination = new MemoryStream();
        using var source = new MemoryStream(new byte[] { 1, 2, 3 });
        using var writer = new ZipWriter(
            destination,
            new ZipWriterOptions(CompressionType.Deflate)
        );

        var options = new ZipWriterEntryOptions { CompressionLevel = 11 };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            writer.Write("entry.bin", source, options)
        );
    }
}
