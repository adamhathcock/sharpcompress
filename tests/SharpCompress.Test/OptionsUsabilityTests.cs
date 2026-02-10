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

    [Fact]
    public void WriterOptions_Factory_Methods_Create_Valid_Options()
    {
        // ForZip
        var zipOptions = WriterOptions.ForZip();
        Assert.Equal(CompressionType.Deflate, zipOptions.CompressionType);
        Assert.True(zipOptions.LeaveStreamOpen);

        // ForTar
        var tarOptions = WriterOptions.ForTar();
        Assert.Equal(CompressionType.None, tarOptions.CompressionType);

        // ForGZip
        var gzipOptions = WriterOptions.ForGZip();
        Assert.Equal(CompressionType.GZip, gzipOptions.CompressionType);
    }

    [Fact]
    public void WriterOptions_Fluent_Methods_Modify_Correctly()
    {
        var options = WriterOptions.ForZip().WithLeaveStreamOpen(false).WithCompressionLevel(9);

        Assert.Equal(CompressionType.Deflate, options.CompressionType);
        Assert.Equal(9, options.CompressionLevel);
        Assert.False(options.LeaveStreamOpen);
    }

    [Fact]
    public void WriterOptions_Factory_And_Fluent_Equivalent_To_Constructor()
    {
        // Factory + fluent approach
        var factoryApproach = WriterOptions
            .ForZip()
            .WithLeaveStreamOpen(false)
            .WithCompressionLevel(9);

        // Traditional constructor approach
        var constructorApproach = new WriterOptions(CompressionType.Deflate)
        {
            CompressionLevel = 9,
            LeaveStreamOpen = false,
        };

        Assert.Equal(factoryApproach.CompressionType, constructorApproach.CompressionType);
        Assert.Equal(factoryApproach.CompressionLevel, constructorApproach.CompressionLevel);
        Assert.Equal(factoryApproach.LeaveStreamOpen, constructorApproach.LeaveStreamOpen);
    }

    [Fact]
    public void ReaderOptions_Fluent_Methods_Modify_Correctly()
    {
        var options = new ReaderOptions()
            .WithLeaveStreamOpen(false)
            .WithPassword("secret")
            .WithLookForHeader(true)
            .WithBufferSize(65536);

        Assert.False(options.LeaveStreamOpen);
        Assert.Equal("secret", options.Password);
        Assert.True(options.LookForHeader);
        Assert.Equal(65536, options.BufferSize);
    }

    [Fact]
    public void ReaderOptions_Fluent_And_Initializer_Equivalent()
    {
        // Fluent approach
        var fluentApproach = new ReaderOptions()
            .WithLeaveStreamOpen(false)
            .WithPassword("secret")
            .WithLookForHeader(true)
            .WithOverwrite(false);

        // Object initializer approach
        var initializerApproach = new ReaderOptions
        {
            LeaveStreamOpen = false,
            Password = "secret",
            LookForHeader = true,
            Overwrite = false,
        };

        Assert.Equal(fluentApproach.LeaveStreamOpen, initializerApproach.LeaveStreamOpen);
        Assert.Equal(fluentApproach.Password, initializerApproach.Password);
        Assert.Equal(fluentApproach.LookForHeader, initializerApproach.LookForHeader);
        Assert.Equal(fluentApproach.Overwrite, initializerApproach.Overwrite);
    }

    [Fact]
    public void ReaderOptions_Presets_Have_Correct_Defaults()
    {
        var external = ReaderOptions.ForExternalStream;
        Assert.True(external.LeaveStreamOpen);

        var owned = ReaderOptions.ForOwnedFile;
        Assert.False(owned.LeaveStreamOpen);

        var safe = ReaderOptions.SafeExtract;
        Assert.False(safe.Overwrite);

        var flat = ReaderOptions.FlatExtract;
        Assert.False(flat.ExtractFullPath);
        Assert.True(flat.Overwrite);
    }

    [Fact]
    public void ReaderOptions_Factory_ForEncryptedArchive_Sets_Password()
    {
        var options = ReaderOptions.ForEncryptedArchive("myPassword");
        Assert.Equal("myPassword", options.Password);

        var noPassword = ReaderOptions.ForEncryptedArchive();
        Assert.Null(noPassword.Password);
    }

    [Fact]
    public void ReaderOptions_Factory_ForEncoding_Sets_Encoding()
    {
        var encoding = new ArchiveEncoding { Default = System.Text.Encoding.UTF8 };
        var options = ReaderOptions.ForEncoding(encoding);
        Assert.Equal(encoding, options.ArchiveEncoding);
    }

    [Fact]
    public void ReaderOptions_Factory_ForSelfExtractingArchive_Configures_Correctly()
    {
        var options = ReaderOptions.ForSelfExtractingArchive("password");
        Assert.True(options.LookForHeader);
        Assert.Equal("password", options.Password);
        Assert.Equal(1_048_576, options.RewindableBufferSize);

        var noPassword = ReaderOptions.ForSelfExtractingArchive();
        Assert.True(noPassword.LookForHeader);
        Assert.Null(noPassword.Password);
        Assert.Equal(1_048_576, noPassword.RewindableBufferSize);
    }
}
