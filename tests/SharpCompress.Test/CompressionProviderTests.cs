using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Providers;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test;

public class CompressionProviderTests
{
    [Fact]
    public void CompressionProviderRegistry_Default_ReturnsInternalProviders()
    {
        var registry = CompressionProviderRegistry.Default;

        registry.GetProvider(CompressionType.Deflate).Should().NotBeNull();
        registry.GetProvider(CompressionType.GZip).Should().NotBeNull();
        registry.GetProvider(CompressionType.BZip2).Should().NotBeNull();
        registry.GetProvider(CompressionType.ZStandard).Should().NotBeNull();
        registry.GetProvider(CompressionType.LZip).Should().NotBeNull();
        registry.GetProvider(CompressionType.Xz).Should().NotBeNull();
        registry.GetProvider(CompressionType.Lzw).Should().NotBeNull();
    }

    [Fact]
    public void CompressionProviderRegistry_With_ReplacesProvider()
    {
        var customProvider = new DeflateCompressionProvider();
        var registry = CompressionProviderRegistry.Default.With(customProvider);

        // Should return the new provider
        var retrieved = registry.GetProvider(CompressionType.Deflate);
        retrieved.Should().BeSameAs(customProvider);
    }

    [Fact]
    public void CompressionProviderRegistry_With_DoesNotModifyOriginal()
    {
        var original = CompressionProviderRegistry.Default;
        var customProvider = new DeflateCompressionProvider();
        var modified = original.With(customProvider);

        // Original should still have the default provider
        var originalProvider = original.GetProvider(CompressionType.Deflate);
        originalProvider.Should().NotBeSameAs(customProvider);
    }

    [Fact]
    public void DeflateProvider_RoundTrip_Works()
    {
        var provider = new DeflateCompressionProvider();
        var original = Encoding.UTF8.GetBytes("Hello, World! This is a test of compression.");

        using var compressedStream = new MemoryStream();
        // Wrap in NonDisposingStream so the compression stream doesn't close it
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(compressedStream);
        using (var compressStream = provider.CreateCompressStream(nonDisposingStream, 6))
        {
            compressStream.Write(original, 0, original.Length);
        }

        compressedStream.Position = 0;
        using var decompressStream = provider.CreateDecompressStream(compressedStream);
        using var resultStream = new MemoryStream();
        decompressStream.CopyTo(resultStream);

        var result = resultStream.ToArray();
        result.Should().Equal(original);
    }

    [Fact]
    public void GZipProvider_RoundTrip_Works()
    {
        var provider = new GZipCompressionProvider();
        var original = Encoding.UTF8.GetBytes("Hello, World! This is a test of compression.");

        using var compressedStream = new MemoryStream();
        // Wrap in NonDisposingStream so the compression stream doesn't close it
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(compressedStream);
        using (var compressStream = provider.CreateCompressStream(nonDisposingStream, 6))
        {
            compressStream.Write(original, 0, original.Length);
        }

        compressedStream.Position = 0;
        using var decompressStream = provider.CreateDecompressStream(compressedStream);
        using var resultStream = new MemoryStream();
        decompressStream.CopyTo(resultStream);

        var result = resultStream.ToArray();
        result.Should().Equal(original);
    }

    [Fact]
    public void BZip2Provider_SupportsCompressionAndDecompression()
    {
        var provider = new BZip2CompressionProvider();

        // Verify the provider reports correct capabilities
        provider.CompressionType.Should().Be(CompressionType.BZip2);
        provider.SupportsCompression.Should().BeTrue();
        provider.SupportsDecompression.Should().BeTrue();
    }

    [Fact]
    public void TarWriter_WithCustomProvider_UsesProvider()
    {
        var customProvider = new GZipCompressionProvider();
        var registry = CompressionProviderRegistry.Default.With(customProvider);

        using var stream = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.GZip, true)
        {
            CompressionProviders = registry,
        };

        using (var writer = new TarWriter(stream, options))
        {
            var data = Encoding.UTF8.GetBytes("Test content");
            writer.Write("test.txt", new MemoryStream(data), DateTime.Now);
        }

        // Should have written compressed data
        stream.Position = 0;
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TarWriter_WithoutCustomProvider_UsesDefault()
    {
        using var stream = new MemoryStream();
        var options = new TarWriterOptions(CompressionType.GZip, true);

        using (var writer = new TarWriter(stream, options))
        {
            var data = Encoding.UTF8.GetBytes("Test content");
            writer.Write("test.txt", new MemoryStream(data), DateTime.Now);
        }

        stream.Position = 0;
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TarReader_WithCustomProvider_UsesProvider()
    {
        // First, create a tar.gz file
        using var archiveStream = new MemoryStream();
        var writeOptions = new TarWriterOptions(CompressionType.GZip, true);
        using (var writer = new TarWriter(archiveStream, writeOptions))
        {
            var data = Encoding.UTF8.GetBytes("Test content for reading");
            writer.Write("test.txt", new MemoryStream(data), DateTime.Now);
        }

        // Now read it back with a custom provider
        archiveStream.Position = 0;
        var customProvider = new GZipCompressionProvider();
        var registry = CompressionProviderRegistry.Default.With(customProvider);
        var readOptions = new ReaderOptions { CompressionProviders = registry };

        using var reader = TarReader.OpenReader(archiveStream, readOptions);
        reader.MoveToNextEntry().Should().BeTrue();
        using var entryStream = reader.OpenEntryStream();
        using var resultStream = new MemoryStream();
        entryStream.CopyTo(resultStream);

        var result = Encoding.UTF8.GetString(resultStream.ToArray());
        result.Should().Be("Test content for reading");
    }

    [Fact]
    public void WriterOptions_WithProviders_CanBeCloned()
    {
        var customProvider = new DeflateCompressionProvider();
        var registry = CompressionProviderRegistry.Default.With(customProvider);

        var original = new WriterOptions(CompressionType.GZip)
        {
            CompressionProviders = registry,
            LeaveStreamOpen = false,
        };

        // Clone using 'with' expression
        var clone = original with
        {
            LeaveStreamOpen = true,
        };

        clone.CompressionType.Should().Be(original.CompressionType);
        clone.CompressionLevel.Should().Be(original.CompressionLevel);
        clone.CompressionProviders.Should().BeSameAs(original.CompressionProviders);
        clone.LeaveStreamOpen.Should().BeTrue();
    }

    [Fact]
    public void ReaderOptions_WithProviders_CanBeCloned()
    {
        var customProvider = new DeflateCompressionProvider();
        var registry = CompressionProviderRegistry.Default.With(customProvider);

        var original = new ReaderOptions
        {
            CompressionProviders = registry,
            LeaveStreamOpen = false,
        };

        // Clone using 'with' expression
        var clone = original with
        {
            LeaveStreamOpen = true,
        };

        clone.CompressionProviders.Should().BeSameAs(original.CompressionProviders);
        clone.LeaveStreamOpen.Should().BeTrue();
    }

    #region System.IO.Compression Tests

    [Fact]
    public void SystemGZipProvider_RoundTrip_Works()
    {
        var provider = new SystemGZipCompressionProvider();
        var original = Encoding.UTF8.GetBytes(
            "Hello, World! This is a test of System.IO.Compression.GZipStream."
        );

        using var compressedStream = new MemoryStream();
        // System.IO.Compression streams leave the underlying stream open by default with leaveOpen: true
        using (var compressStream = provider.CreateCompressStream(compressedStream, 6))
        {
            compressStream.Write(original, 0, original.Length);
        }

        compressedStream.Position = 0;
        using var decompressStream = provider.CreateDecompressStream(compressedStream);
        using var resultStream = new MemoryStream();
        decompressStream.CopyTo(resultStream);

        var result = resultStream.ToArray();
        result.Should().Equal(original);
    }

    [Theory]
    [InlineData(0)] // No compression
    [InlineData(3)] // Fast
    [InlineData(6)] // Default
    [InlineData(9)] // Best compression
    public void SystemGZipProvider_DifferentCompressionLevels_Work(int level)
    {
        var provider = new SystemGZipCompressionProvider();
        var original = Encoding.UTF8.GetBytes(
            "Test data for compression level testing with System.IO.Compression."
        );

        using var compressedStream = new MemoryStream();
        using (var compressStream = provider.CreateCompressStream(compressedStream, level))
        {
            compressStream.Write(original, 0, original.Length);
        }

        compressedStream.Position = 0;
        using var decompressStream = provider.CreateDecompressStream(compressedStream);
        using var resultStream = new MemoryStream();
        decompressStream.CopyTo(resultStream);

        var result = resultStream.ToArray();
        result.Should().Equal(original);
    }

    [Fact]
    public void SystemGZipProvider_Compress_InternalProvider_Decompress_CrossCompatibility()
    {
        // Compress with System.IO.Compression
        var systemProvider = new SystemGZipCompressionProvider();
        var original = Encoding.UTF8.GetBytes(
            "Cross-compatibility test between System.IO.Compression and internal GZip."
        );

        using var compressedStream = new MemoryStream();
        using (var compressStream = systemProvider.CreateCompressStream(compressedStream, 6))
        {
            compressStream.Write(original, 0, original.Length);
        }

        // Decompress with internal provider
        compressedStream.Position = 0;
        var internalProvider = new GZipCompressionProvider();
        using var decompressStream = internalProvider.CreateDecompressStream(compressedStream);
        using var resultStream = new MemoryStream();
        decompressStream.CopyTo(resultStream);

        var result = resultStream.ToArray();
        result.Should().Equal(original);
    }

    [Fact]
    public void InternalProvider_Compress_SystemGZipProvider_Decompress_CrossCompatibility()
    {
        // Compress with internal provider
        var internalProvider = new GZipCompressionProvider();
        var original = Encoding.UTF8.GetBytes(
            "Cross-compatibility test between internal GZip and System.IO.Compression."
        );

        using var compressedStream = new MemoryStream();
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(compressedStream);
        using (var compressStream = internalProvider.CreateCompressStream(nonDisposingStream, 6))
        {
            compressStream.Write(original, 0, original.Length);
        }

        // Decompress with System.IO.Compression
        compressedStream.Position = 0;
        var systemProvider = new SystemGZipCompressionProvider();
        using var decompressStream = systemProvider.CreateDecompressStream(compressedStream);
        using var resultStream = new MemoryStream();
        decompressStream.CopyTo(resultStream);

        var result = resultStream.ToArray();
        result.Should().Equal(original);
    }

    [Fact]
    public void TarWriter_WithSystemGZipProvider_CreatesReadableArchive()
    {
        // Create tar.gz using System.IO.Compression provider
        var systemProvider = new SystemGZipCompressionProvider();
        var registry = CompressionProviderRegistry.Default.With(systemProvider);

        using var archiveStream = new MemoryStream();
        var writeOptions = new TarWriterOptions(CompressionType.GZip, true)
        {
            CompressionProviders = registry,
        };

        using (var writer = new TarWriter(archiveStream, writeOptions))
        {
            var data = Encoding.UTF8.GetBytes("Content written with System.IO.Compression");
            writer.Write("test.txt", new MemoryStream(data), DateTime.Now);
        }

        // Read back using internal provider (should be compatible)
        archiveStream.Position = 0;
        var readOptions = new ReaderOptions();
        using var reader = TarReader.OpenReader(archiveStream, readOptions);
        reader.MoveToNextEntry().Should().BeTrue();
        using var entryStream = reader.OpenEntryStream();
        using var resultStream = new MemoryStream();
        entryStream.CopyTo(resultStream);

        var result = Encoding.UTF8.GetString(resultStream.ToArray());
        result.Should().Be("Content written with System.IO.Compression");
    }

    [Fact]
    public void SystemGZipProvider_SupportsCompressionAndDecompression()
    {
        var provider = new SystemGZipCompressionProvider();

        // Verify the provider reports correct capabilities
        provider.CompressionType.Should().Be(CompressionType.GZip);
        provider.SupportsCompression.Should().BeTrue();
        provider.SupportsDecompression.Should().BeTrue();
    }

    [Fact]
    public void SystemDeflateProvider_RoundTrip_Works()
    {
        var provider = new SystemDeflateCompressionProvider();
        var original = Encoding.UTF8.GetBytes(
            "Hello, World! This is a test of System.IO.Compression.DeflateStream."
        );

        using var compressedStream = new MemoryStream();
        using (var compressStream = provider.CreateCompressStream(compressedStream, 6))
        {
            compressStream.Write(original, 0, original.Length);
        }

        compressedStream.Position = 0;
        using var decompressStream = provider.CreateDecompressStream(compressedStream);
        using var resultStream = new MemoryStream();
        decompressStream.CopyTo(resultStream);

        var result = resultStream.ToArray();
        result.Should().Equal(original);
    }

    [Fact]
    public void SystemDeflateProvider_Compress_InternalProvider_Decompress_CrossCompatibility()
    {
        // Compress with System.IO.Compression Deflate
        var systemProvider = new SystemDeflateCompressionProvider();
        var original = Encoding.UTF8.GetBytes(
            "Cross-compatibility test between System.IO.Compression and internal Deflate."
        );

        using var compressedStream = new MemoryStream();
        using (var compressStream = systemProvider.CreateCompressStream(compressedStream, 6))
        {
            compressStream.Write(original, 0, original.Length);
        }

        // Decompress with internal provider
        compressedStream.Position = 0;
        var internalProvider = new DeflateCompressionProvider();
        using var decompressStream = internalProvider.CreateDecompressStream(compressedStream);
        using var resultStream = new MemoryStream();
        decompressStream.CopyTo(resultStream);

        var result = resultStream.ToArray();
        result.Should().Equal(original);
    }

    #endregion
}
