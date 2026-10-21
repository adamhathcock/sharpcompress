using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Providers;
using SharpCompress.Providers.Default;
using SharpCompress.Readers;
using SharpCompress.Services;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using Xunit;

namespace SharpCompress.Test;

public class ServiceCompositionTests : TestBase
{
    private static ServiceComposition CreateComposition(params IFactory[] factories) =>
        new(new FactoryCatalog(() => factories, () => TarWrapper.Wrappers));

    [Fact]
    public void Composition_RemainsAnInternalBuildTimeDetail()
    {
        Type[] serviceTypes =
        [
            typeof(ServiceComposition),
            typeof(LibraryDefaults),
            typeof(FactoryCatalog),
            typeof(ArchiveService),
            typeof(ReaderService),
            typeof(WriterService),
            typeof(ArchiveExtractor),
            typeof(ArchiveFileWriter),
        ];
        Assert.All(
            serviceTypes,
            type =>
            {
                Assert.False(type.IsVisible);
                Assert.Equal("SharpCompress.Services", type.Namespace);
            }
        );
        Assert.DoesNotContain(
            typeof(ArchiveFactory).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is "Pure.DI" or "Pure.DI.Abstractions"
        );
        Assert.IsType<TarWrapper[]>(TarWrapper.Wrappers);
    }

    [Fact]
    public async Task Composition_ReusesServiceRootsDuringConcurrentAccess()
    {
        var composition = CreateComposition(new ZipFactory());
        var other = CreateComposition(new ZipFactory());
        var roots = await Task.WhenAll(
            Enumerable
                .Range(0, 10)
                .Select(_ =>
                    Task.Run(() =>
                        (
                            composition.Archives,
                            composition.Readers,
                            composition.Writers,
                            composition.Extractor,
                            composition.FileWriter
                        )
                    )
                )
        );
        Assert.All(
            roots,
            root =>
            {
                Assert.Same(composition.Archives, root.Archives);
                Assert.Same(composition.Readers, root.Readers);
                Assert.Same(composition.Writers, root.Writers);
                Assert.Same(composition.Extractor, root.Extractor);
                Assert.Same(composition.FileWriter, root.FileWriter);
            }
        );
        Assert.NotSame(composition.Archives, other.Archives);
        Assert.NotSame(composition.Readers, other.Readers);
        Assert.NotSame(composition.Writers, other.Writers);
        Assert.NotSame(composition.Extractor, other.Extractor);
        Assert.NotSame(composition.FileWriter, other.FileWriter);
    }

    [Fact]
    public async Task Compositions_UseIndependentFactoriesConcurrently()
    {
        var zip = CreateComposition(new ZipFactory());
        var empty = CreateComposition();
        Assert.NotSame(zip.Readers, empty.Readers);
        Assert.NotSame(zip.Extractor, empty.Extractor);
        var path = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip");
        await Task.WhenAll(
            Enumerable
                .Range(0, 10)
                .Select(async _ =>
                {
                    using var stream = File.OpenRead(path);
                    Assert.Equal(
                        ArchiveType.Zip,
                        zip.Archives.DetectArchive(stream)?.ContainerType
                    );
                    Assert.Null(empty.Archives.DetectArchive(stream));
                    Assert.Equal(0, stream.Position);
                    Assert.NotNull(
                        await zip.Archives.InspectArchiveAsync(stream, CancellationToken.None)
                    );
                    Assert.Null(
                        await empty.Archives.InspectArchiveAsync(stream, CancellationToken.None)
                    );
                    Assert.Equal(0, stream.Position);
                    using var archive = zip.Archives.OpenArchive(stream);
                    Assert.NotEmpty(archive.Entries);
                    Assert.Throws<ArchiveOperationException>(() =>
                        empty.Archives.OpenArchive(stream)
                    );
                })
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Services_PreserveProvidersAndStreamOwnership(bool useAsync)
    {
        var services = CreateComposition(new ZipFactory());
        var provider = new TrackingDeflateProvider();
        var providers = CompressionProviderRegistry.Default.With(provider);
        var writerOptions = new WriterOptions(CompressionType.Deflate)
        {
            Providers = providers,
            LeaveStreamOpen = true,
        };
        var readerOptions = new ReaderOptions { Providers = providers, LeaveStreamOpen = true };
        using var stream = new MemoryStream();
        using var payload = new MemoryStream(Encoding.UTF8.GetBytes("instance services"));
        if (useAsync)
        {
            await using var writer = await services.Writers.OpenAsyncWriter(
                stream,
                ArchiveType.Zip,
                writerOptions,
                CancellationToken.None
            );
            await writer.WriteAsync("payload.txt", payload, null, CancellationToken.None);
        }
        else
        {
            using var writer = services.Writers.OpenWriter(stream, ArchiveType.Zip, writerOptions);
            writer.Write("payload.txt", payload, null);
        }
        Assert.True(provider.CompressionCalls > 0);
        stream.Position = 0;
        using var output = new MemoryStream();
        if (useAsync)
        {
            await using var archive = await services.Archives.OpenAsyncArchive(
                stream,
                readerOptions,
                CancellationToken.None
            );
            await foreach (var entry in archive.EntriesAsync)
            {
                await services.Extractor.WriteToAsync(
                    entry,
                    output,
                    null,
                    cancellationToken: CancellationToken.None
                );
            }
        }
        else
        {
            using var archive = services.Archives.OpenArchive(stream, readerOptions);
            services.Extractor.WriteTo(Assert.Single(archive.Entries), output, null);
        }
        Assert.True(provider.DecompressionCalls > 0);
        Assert.Equal("instance services", Encoding.UTF8.GetString(output.ToArray()));
        Assert.True(stream.CanRead);
        Assert.True(output.CanWrite);
        Assert.True(readerOptions.LeaveStreamOpen);
        Assert.Same(providers, readerOptions.Providers);
        Assert.Same(providers, writerOptions.Providers);

        stream.Position = 0;
        using var forwardOnly = new ForwardOnlyStream(stream);
        if (useAsync)
        {
            await using var reader = await services.Readers.OpenAsyncReader(
                forwardOnly,
                readerOptions,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
        }
        else
        {
            using var reader = services.Readers.OpenReader(forwardOnly, readerOptions);
            Assert.True(reader.MoveToNextEntry());
        }
        Assert.True(forwardOnly.CanRead);
    }

    [Fact]
    public async Task Services_PreserveAsyncCancellation()
    {
        var services = CreateComposition(new ZipFactory());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            services.Archives.DetectArchiveAsync(stream, cancellation.Token).AsTask()
        );
        Assert.Equal(0, stream.Position);
    }

    private sealed class TrackingDeflateProvider : CompressionProviderBase
    {
        private readonly DeflateCompressionProvider inner = new();
        public int CompressionCalls { get; private set; }
        public int DecompressionCalls { get; private set; }
        public override CompressionType CompressionType => CompressionType.Deflate;
        public override bool SupportsCompression => true;
        public override bool SupportsDecompression => true;

        public override Stream CreateCompressStream(Stream destination, int compressionLevel)
        {
            CompressionCalls++;
            return inner.CreateCompressStream(destination, compressionLevel);
        }

        public override Stream CreateDecompressStream(Stream source)
        {
            DecompressionCalls++;
            return inner.CreateDecompressStream(source);
        }
    }
}

// Legacy registration mutates process-wide state, so this collection cannot run alongside others.
[CollectionDefinition("Factory registration", DisableParallelization = true)]
public class FactoryRegistrationCollection;

[Collection("Factory registration")]
public class FactoryRegistrationTests
{
    [Fact]
    public async Task DefaultComposition_ObservesLateRegistration()
    {
        _ = LibraryDefaults.Composition.Archives;
        var factory = new TestFactory();
        Factory.RegisterFactory(factory);
        try
        {
            Assert.Contains(factory, Factory.Factories);
            using var stream = new MemoryStream([0x49, 0x44, 0x49]);
            Assert.Same(factory, ArchiveFactory.FindFactory<IFactory>(stream));
            Assert.Same(
                factory,
                await ArchiveFactory.FindFactoryAsync<IFactory>(stream, CancellationToken.None)
            );
        }
        finally
        {
            // Restore the legacy collection without adding a new public unregister API.
            Assert.IsAssignableFrom<ICollection<Factory>>(Factory.Factories).Remove(factory);
        }
    }

    private sealed class TestFactory : Factory
    {
        public override string Name => "Instance DI test";

        public override IEnumerable<string> GetSupportedExtensions() => [];

        public override bool IsArchive(Stream stream, ReaderOptions readerOptions) =>
            stream.ReadByte() == 0x49 && stream.ReadByte() == 0x44 && stream.ReadByte() == 0x49;

        public override ValueTask<bool> IsArchiveAsync(
            Stream stream,
            ReaderOptions readerOptions,
            CancellationToken cancellationToken = default
        ) => new(IsArchive(stream, readerOptions));
    }
}
