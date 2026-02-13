using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Compressors;
using SharpCompress.IO;
using SharpCompress.Providers;
using SharpCompress.Providers.Default;
using SharpCompress.Providers.System;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test;

public class CompressionProviderTests
{
    private sealed class TrackingCompressionProvider : ICompressionProvider
    {
        private readonly ICompressionProvider _inner;

        public TrackingCompressionProvider(ICompressionProvider inner)
        {
            _inner = inner;
        }

        public int CompressionCalls { get; private set; }

        public int DecompressionCalls { get; private set; }

        public int AsyncCompressionCalls { get; private set; }

        public int AsyncDecompressionCalls { get; private set; }

        public CompressionType CompressionType => _inner.CompressionType;

        public bool SupportsCompression => _inner.SupportsCompression;

        public bool SupportsDecompression => _inner.SupportsDecompression;

        public Stream CreateCompressStream(Stream destination, int compressionLevel)
        {
            CompressionCalls++;
            return _inner.CreateCompressStream(destination, compressionLevel);
        }

        public Stream CreateCompressStream(
            Stream destination,
            int compressionLevel,
            CompressionContext context
        )
        {
            CompressionCalls++;
            return _inner.CreateCompressStream(destination, compressionLevel, context);
        }

        public Stream CreateDecompressStream(Stream source)
        {
            DecompressionCalls++;
            return _inner.CreateDecompressStream(source);
        }

        public Stream CreateDecompressStream(Stream source, CompressionContext context)
        {
            DecompressionCalls++;
            return _inner.CreateDecompressStream(source, context);
        }

        public ValueTask<Stream> CreateCompressStreamAsync(
            Stream destination,
            int compressionLevel,
            CancellationToken cancellationToken = default
        )
        {
            AsyncCompressionCalls++;
            return _inner.CreateCompressStreamAsync(
                destination,
                compressionLevel,
                cancellationToken
            );
        }

        public ValueTask<Stream> CreateCompressStreamAsync(
            Stream destination,
            int compressionLevel,
            CompressionContext context,
            CancellationToken cancellationToken = default
        )
        {
            AsyncCompressionCalls++;
            return _inner.CreateCompressStreamAsync(
                destination,
                compressionLevel,
                context,
                cancellationToken
            );
        }

        public ValueTask<Stream> CreateDecompressStreamAsync(
            Stream source,
            CancellationToken cancellationToken = default
        )
        {
            AsyncDecompressionCalls++;
            return _inner.CreateDecompressStreamAsync(source, cancellationToken);
        }

        public ValueTask<Stream> CreateDecompressStreamAsync(
            Stream source,
            CompressionContext context,
            CancellationToken cancellationToken = default
        )
        {
            AsyncDecompressionCalls++;
            return _inner.CreateDecompressStreamAsync(source, context, cancellationToken);
        }
    }

    private sealed class TrackingLzmaHooksProvider : ICompressionProviderHooks
    {
        public int PreCalls { get; private set; }
        public int PropertiesCalls { get; private set; }
        public int PostCalls { get; private set; }

        public CompressionType CompressionType => CompressionType.LZMA;

        public bool SupportsCompression => true;

        public bool SupportsDecompression => false;

        public Stream CreateCompressStream(Stream destination, int compressionLevel)
        {
            CompressionContext context = new() { CanSeek = destination.CanSeek };
            return CreateCompressStream(destination, compressionLevel, context);
        }

        public Stream CreateCompressStream(
            Stream destination,
            int compressionLevel,
            CompressionContext context
        ) => SharpCompressStream.CreateNonDisposing(destination);

        public Stream CreateDecompressStream(Stream source) => throw new NotSupportedException();

        public Stream CreateDecompressStream(Stream source, CompressionContext context) =>
            throw new NotSupportedException();

        public ValueTask<Stream> CreateCompressStreamAsync(
            Stream destination,
            int compressionLevel,
            CancellationToken cancellationToken = default
        )
        {
            CompressionContext context = new() { CanSeek = destination.CanSeek };
            return CreateCompressStreamAsync(
                destination,
                compressionLevel,
                context,
                cancellationToken
            );
        }

        public ValueTask<Stream> CreateCompressStreamAsync(
            Stream destination,
            int compressionLevel,
            CompressionContext context,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<Stream>(SharpCompressStream.CreateNonDisposing(destination));
        }

        public ValueTask<Stream> CreateDecompressStreamAsync(
            Stream source,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public ValueTask<Stream> CreateDecompressStreamAsync(
            Stream source,
            CompressionContext context,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public byte[]? GetPreCompressionData(CompressionContext context)
        {
            PreCalls++;
            return [];
        }

        public byte[]? GetCompressionProperties(Stream stream, CompressionContext context)
        {
            PropertiesCalls++;
            return [];
        }

        public byte[]? GetPostCompressionData(Stream stream, CompressionContext context)
        {
            PostCalls++;
            return [1, 2, 3];
        }
    }

    private sealed class ContextRequiredGZipProvider : CompressionProviderBase
    {
        private readonly GZipCompressionProvider _inner = new();

        public override CompressionType CompressionType => CompressionType.GZip;

        public override bool SupportsCompression => true;

        public override bool SupportsDecompression => true;

        public override Stream CreateCompressStream(Stream destination, int compressionLevel) =>
            _inner.CreateCompressStream(destination, compressionLevel);

        public override Stream CreateDecompressStream(Stream source) =>
            throw new InvalidOperationException("Context is required for GZip decompression.");

        public override Stream CreateDecompressStream(Stream source, CompressionContext context)
        {
            context.ReaderOptions.Should().NotBeNull();
            return _inner.CreateDecompressStream(source, context);
        }
    }

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
        var modifiedProvider = modified.GetProvider(CompressionType.Deflate);
        originalProvider.Should().NotBeSameAs(modifiedProvider);
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
    public void GZipProvider_Decompress_WithReaderOptionsContext_UsesArchiveEncoding()
    {
        var provider = new GZipCompressionProvider();
        var data = Encoding.UTF8.GetBytes("gzip filename encoding");
        var expectedFileName = "café.txt";
        var archiveEncoding = new ArchiveEncoding { Default = Encoding.GetEncoding("iso-8859-1") };

        using var compressedStream = CreateGZipWithFileName(
            data,
            expectedFileName,
            archiveEncoding.Default
        );

        compressedStream.Position = 0;
        var readerOptions = new ReaderOptions { ArchiveEncoding = archiveEncoding };
        var context = CompressionContext.FromStream(compressedStream) with
        {
            ReaderOptions = readerOptions,
        };

        using var decompressStream = provider.CreateDecompressStream(compressedStream, context);
        using var resultStream = new MemoryStream();
        decompressStream.CopyTo(resultStream);

        resultStream.ToArray().Should().Equal(data);
        decompressStream.Should().BeOfType<SharpCompress.Compressors.Deflate.GZipStream>();
        var gzipStream = (SharpCompress.Compressors.Deflate.GZipStream)decompressStream;
        gzipStream.FileName.Should().Be(expectedFileName);
    }

    [Fact]
    public void GZipProvider_Decompress_WithNullReaderOptions_FallsBackToUtf8()
    {
        var provider = new GZipCompressionProvider();
        var data = Encoding.UTF8.GetBytes("gzip filename encoding");
        var expectedFileName = "café.txt";

        using var compressedStream = CreateGZipWithFileName(data, expectedFileName, Encoding.UTF8);

        compressedStream.Position = 0;
        var context = CompressionContext.FromStream(compressedStream);
        // ReaderOptions is null by default

        using var decompressStream = provider.CreateDecompressStream(compressedStream, context);
        using var resultStream = new MemoryStream();
        decompressStream.CopyTo(resultStream);

        resultStream.ToArray().Should().Equal(data);
        var gzipStream = (SharpCompress.Compressors.Deflate.GZipStream)decompressStream;
        gzipStream.FileName.Should().Be(expectedFileName);
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
        var options = new TarWriterOptions(CompressionType.GZip, true) { Providers = registry };

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
        var readOptions = new ReaderOptions { Providers = registry };

        using var reader = TarReader.OpenReader(archiveStream, readOptions);
        reader.MoveToNextEntry().Should().BeTrue();
        using var entryStream = reader.OpenEntryStream();
        using var resultStream = new MemoryStream();
        entryStream.CopyTo(resultStream);

        var result = Encoding.UTF8.GetString(resultStream.ToArray());
        result.Should().Be("Test content for reading");
    }

    [Fact]
    public void TarReader_OpenReader_WithContextRequiredGZipProvider_Succeeds()
    {
        using var archiveStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                archiveStream,
                new TarWriterOptions(CompressionType.GZip, true)
            )
        )
        {
            var data = Encoding.UTF8.GetBytes("Test content for context-required provider");
            writer.Write("test.txt", new MemoryStream(data), DateTime.Now);
        }

        archiveStream.Position = 0;
        var registry = CompressionProviderRegistry.Default.With(new ContextRequiredGZipProvider());
        var readOptions = new ReaderOptions { Providers = registry };

        using var reader = TarReader.OpenReader(archiveStream, readOptions);
        reader.MoveToNextEntry().Should().BeTrue();
        using var entryStream = reader.OpenEntryStream();
        using var resultStream = new MemoryStream();
        entryStream.CopyTo(resultStream);

        var result = Encoding.UTF8.GetString(resultStream.ToArray());
        result.Should().Be("Test content for context-required provider");
    }

    [Fact]
    public void WriterOptions_WithProviders_CanBeCloned()
    {
        var customProvider = new DeflateCompressionProvider();
        var registry = CompressionProviderRegistry.Default.With(customProvider);

        var original = new WriterOptions(CompressionType.GZip)
        {
            Providers = registry,
            LeaveStreamOpen = false,
        };

        // Clone using 'with' expression
        var clone = original with
        {
            LeaveStreamOpen = true,
        };

        clone.CompressionType.Should().Be(original.CompressionType);
        clone.CompressionLevel.Should().Be(original.CompressionLevel);
        clone.Providers.Should().BeSameAs(original.Providers);
        clone.LeaveStreamOpen.Should().BeTrue();
    }

    [Fact]
    public void ReaderOptions_WithProviders_CanBeCloned()
    {
        var customProvider = new DeflateCompressionProvider();
        var registry = CompressionProviderRegistry.Default.With(customProvider);

        var original = new ReaderOptions { Providers = registry, LeaveStreamOpen = false };

        // Clone using 'with' expression
        var clone = original with
        {
            LeaveStreamOpen = true,
        };

        clone.Providers.Should().BeSameAs(original.Providers);
        clone.LeaveStreamOpen.Should().BeTrue();
    }

    [Fact]
    public void TarArchive_OpenArchive_UsesCustomGZipProvider()
    {
        using var archiveStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                archiveStream,
                new TarWriterOptions(CompressionType.GZip, true)
            )
        )
        {
            var data = Encoding.UTF8.GetBytes("tar archive provider usage");
            writer.Write("test.txt", new MemoryStream(data), DateTime.Now);
        }

        var trackingProvider = new TrackingCompressionProvider(new GZipCompressionProvider());
        var registry = CompressionProviderRegistry.Default.With(trackingProvider);
        var readOptions = new ReaderOptions { Providers = registry };

        archiveStream.Position = 0;
        using var archive = TarArchive.OpenArchive(archiveStream, readOptions);
        var entry = archive.Entries.First(x => !x.IsDirectory);
        using var entryStream = entry.OpenEntryStream();
        using var resultStream = new MemoryStream();
        entryStream.CopyTo(resultStream);

        trackingProvider.DecompressionCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TarArchive_OpenAsyncArchive_UsesCustomGZipProvider()
    {
        using var archiveStream = new MemoryStream();
        using (
            var writer = new TarWriter(
                archiveStream,
                new TarWriterOptions(CompressionType.GZip, true)
            )
        )
        {
            var data = Encoding.UTF8.GetBytes("tar async archive provider usage");
            writer.Write("test.txt", new MemoryStream(data), DateTime.Now);
        }

        var trackingProvider = new TrackingCompressionProvider(new GZipCompressionProvider());
        var registry = CompressionProviderRegistry.Default.With(trackingProvider);
        var readOptions = new ReaderOptions { Providers = registry };

        archiveStream.Position = 0;
        await using var archive = await TarArchive.OpenAsyncArchive(archiveStream, readOptions);
        await foreach (var entry in archive.EntriesAsync)
        {
            if (entry.IsDirectory)
            {
                continue;
            }

            using var entryStream = await entry.OpenEntryStreamAsync();
            using var resultStream = new MemoryStream();
            await entryStream.CopyToAsync(resultStream);
            break;
        }

        trackingProvider.AsyncDecompressionCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ZipReader_OpenEntryStreamAsync_UsesCustomDeflateProvider()
    {
        using var zipStream = new MemoryStream();
        using (
            var writer = WriterFactory.OpenWriter(
                zipStream,
                ArchiveType.Zip,
                new WriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }
            )
        )
        {
            var data = Encoding.UTF8.GetBytes("zip async provider usage");
            writer.Write("test.txt", new MemoryStream(data));
        }

        var trackingProvider = new TrackingCompressionProvider(new DeflateCompressionProvider());
        var registry = CompressionProviderRegistry.Default.With(trackingProvider);
        var options = new ReaderOptions { Providers = registry };

        zipStream.Position = 0;
        await using var reader = await ReaderFactory.OpenAsyncReader(zipStream, options);
        (await reader.MoveToNextEntryAsync()).Should().BeTrue();
        using var entryStream = await reader.OpenEntryStreamAsync();
        using var resultStream = new MemoryStream();
        await entryStream.CopyToAsync(resultStream);

        trackingProvider.AsyncDecompressionCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public void LzwReader_OpenReader_UsesCustomLzwProvider()
    {
        var archivePath = Path.Combine(TestBase.TEST_ARCHIVES_PATH, "Tar.tar.Z");
        var trackingProvider = new TrackingCompressionProvider(new LzwCompressionProvider());
        var registry = CompressionProviderRegistry.Default.With(trackingProvider);
        var options = new ReaderOptions { Providers = registry };

        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream, options);
        reader.MoveToNextEntry().Should().BeTrue();
        reader.WriteEntryTo(Stream.Null);

        trackingProvider.DecompressionCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ZipWriter_LzmaProviderHook_WritesPostCompressionData()
    {
        var trackingProvider = new TrackingLzmaHooksProvider();
        var registry = CompressionProviderRegistry.Default.With(trackingProvider);
        using var zipStream = new MemoryStream();

        using (
            var writer = WriterFactory.OpenWriter(
                zipStream,
                ArchiveType.Zip,
                new WriterOptions(CompressionType.LZMA)
                {
                    LeaveStreamOpen = true,
                    Providers = registry,
                }
            )
        )
        {
            var data = Encoding.UTF8.GetBytes("hook provider");
            writer.Write("test.txt", new MemoryStream(data));
        }

        trackingProvider.PreCalls.Should().BeGreaterThan(0);
        trackingProvider.PropertiesCalls.Should().BeGreaterThan(0);
        trackingProvider.PostCalls.Should().BeGreaterThan(0);
    }

    #region System.IO.Compression Tests

    private static MemoryStream CreateGZipWithFileName(
        byte[] data,
        string fileName,
        Encoding encoding
    )
    {
        var compressedStream = new MemoryStream();
        using (
            var compressStream = new SharpCompress.Compressors.Deflate.GZipStream(
                SharpCompressStream.CreateNonDisposing(compressedStream),
                CompressionMode.Compress,
                SharpCompress.Compressors.Deflate.CompressionLevel.Default,
                encoding
            )
        )
        {
            compressStream.FileName = fileName;
            compressStream.Write(data, 0, data.Length);
        }

        compressedStream.Position = 0;
        return compressedStream;
    }

    [Fact]
    public void SystemGZipProvider_RoundTrip_Works()
    {
        var provider = new SystemGZipCompressionProvider();
        var original = Encoding.UTF8.GetBytes(
            "Hello, World! This is a test of System.IO.Compression.GZipStream."
        );

        using var compressedStream = new MemoryStream();
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
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(compressedStream);
        using (var compressStream = provider.CreateCompressStream(nonDisposingStream, level))
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
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(compressedStream);
        using (var compressStream = systemProvider.CreateCompressStream(nonDisposingStream, 6))
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
            Providers = registry,
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
    public void SystemDeflateProvider_Compress_InternalProvider_Decompress_CrossCompatibility()
    {
        // Compress with System.IO.Compression Deflate
        var systemProvider = new SystemDeflateCompressionProvider();
        var original = Encoding.UTF8.GetBytes(
            "Cross-compatibility test between System.IO.Compression and internal Deflate."
        );

        using var compressedStream = new MemoryStream();
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(compressedStream);
        using (var compressStream = systemProvider.CreateCompressStream(nonDisposingStream, 6))
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
