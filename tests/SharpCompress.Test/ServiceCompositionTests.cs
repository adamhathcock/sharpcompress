using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Detection;
using SharpCompress.Factories;
using SharpCompress.Providers;
using SharpCompress.Providers.Default;
using SharpCompress.Readers;
using SharpCompress.Services;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using Xunit;
using ArchiveDetector = SharpCompress.Services.ArchiveDetector;
using ArchiveExtractor = SharpCompress.Services.ArchiveExtractor;
using ArchiveInspector = SharpCompress.Services.ArchiveInspector;

#pragma warning disable CS0618 // Intentionally exercise obsolete ArchiveFactory compatibility entry points.

namespace SharpCompress.Test;

public class ServiceCompositionTests : TestBase
{
    private static ServiceComposition CreateComposition(params IFactory[] factories) =>
        new(new FactoryCatalog(() => factories, () => TarWrapper.Wrappers));

    private static object[] GetRoots(ServiceComposition composition) =>
        [
            composition.Archives,
            composition.Detector,
            composition.Inspector,
            composition.Readers,
            composition.Writers,
            composition.Extractor,
            composition.ReaderExtractor,
            composition.EntryExtractor,
            composition.WriterFiles,
            composition.ArchiveFiles,
        ];

    [Fact]
    public void Defaults_ExposeReusableRootsWithoutExposingComposition()
    {
        Assert.Null(typeof(LibraryDefaults).GetProperty("Composition"));
        var roots = typeof(ServiceComposition).GetProperties();
        Assert.Equal(roots.Length, typeof(LibraryDefaults).GetProperties().Length);
        foreach (var root in roots)
        {
            var property = Assert.IsAssignableFrom<PropertyInfo>(
                typeof(LibraryDefaults).GetProperty(root.Name)
            );
            Assert.Equal(root.PropertyType, property.PropertyType);
            Assert.Same(property.GetValue(null), property.GetValue(null));
        }
    }

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
            typeof(ExtractionStreamHelper),
            typeof(ArchiveDetector),
            typeof(ArchiveInspector),
            typeof(ReaderExtractor),
            typeof(ArchiveEntryExtractor),
            typeof(WriterFileOperations),
            typeof(WritableArchiveFileOperations),
            typeof(IFactoryCatalog),
            typeof(IArchiveService),
            typeof(IReaderService),
            typeof(IWriterService),
            typeof(IArchiveExtractor),
            typeof(IArchiveDetector),
            typeof(IArchiveInspector),
            typeof(IReaderExtractor),
            typeof(IArchiveEntryExtractor),
            typeof(IWriterFileOperations),
            typeof(IWritableArchiveFileOperations),
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
    public void ServiceContracts_IncludeGeneratedSyncMethods()
    {
        (Type Contract, Type Implementation)[] services =
        [
            (typeof(IFactoryCatalog), typeof(FactoryCatalog)),
            (typeof(IArchiveService), typeof(ArchiveService)),
            (typeof(IReaderService), typeof(ReaderService)),
            (typeof(IWriterService), typeof(WriterService)),
            (typeof(IArchiveExtractor), typeof(ArchiveExtractor)),
            (typeof(IArchiveDetector), typeof(ArchiveDetector)),
            (typeof(IArchiveInspector), typeof(ArchiveInspector)),
            (typeof(IReaderExtractor), typeof(ReaderExtractor)),
            (typeof(IArchiveEntryExtractor), typeof(ArchiveEntryExtractor)),
            (typeof(IWriterFileOperations), typeof(WriterFileOperations)),
            (typeof(IWritableArchiveFileOperations), typeof(WritableArchiveFileOperations)),
        ];
        foreach (var (contract, implementation) in services)
        {
            Assert.True(contract.IsAssignableFrom(implementation));
            foreach (
                var method in implementation.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                )
            )
            {
                Assert.Contains(
                    contract.GetMethods(),
                    candidate => candidate.ToString() == method.ToString()
                );
            }
        }
        Assert.All(
            typeof(ServiceComposition).GetProperties(),
            root => Assert.Contains(services, service => service.Contract == root.PropertyType)
        );
    }

    [Fact]
    public void Composition_AcceptsInterfaceOnlyCatalog()
    {
        IFactoryCatalog catalog = new TestFactoryCatalog();
        var composition = new ServiceComposition(catalog);
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"));
        Assert.Equal(ArchiveType.Zip, composition.Detector.DetectArchive(stream)?.ContainerType);
        Assert.Equal(0, stream.Position);
    }

    private sealed class TestFactoryCatalog : IFactoryCatalog
    {
        public IEnumerable<IFactory> Factories { get; } = [new ZipFactory()];
        public IEnumerable<TarWrapper> Wrappers => TarWrapper.Wrappers;
    }

    [Fact]
    public async Task Composition_ReusesServiceRootsDuringConcurrentAccess()
    {
        var composition = CreateComposition(new ZipFactory());
        var other = CreateComposition(new ZipFactory());
        var roots = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => Task.Run(() => GetRoots(composition)))
        );
        var expected = GetRoots(composition);
        var separate = GetRoots(other);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.All(roots, root => Assert.Same(expected[i], root[i]));
            Assert.NotSame(expected[i], separate[i]);
        }
    }

    [Fact]
    public async Task Inspector_KeepsMetadataStateLocalToEachOperation()
    {
        var composition = CreateComposition(new ZipFactory(), new AceFactory());
        var expected = new[] { "Zip.deflate.zip", "Ace.store.ace" }
            .Select(name => Path.Combine(TEST_ARCHIVES_PATH, name))
            .Select(path =>
                (
                    Path: path,
                    Information: Assert.IsType<ArchiveInformation>(
                        composition.Inspector.InspectArchive(path)
                    )
                )
            )
            .ToArray();
        await Task.WhenAll(
            Enumerable
                .Range(0, 20)
                .Select(index =>
                    Task.Run(async () =>
                    {
                        var sample = expected[index % expected.Length];
                        using var stream = File.OpenRead(sample.Path);
                        var information = Assert.IsType<ArchiveInformation>(
                            await composition.Inspector.InspectArchiveAsync(
                                stream,
                                CancellationToken.None
                            )
                        );
                        Assert.Equal(sample.Information.EntryCount, information.EntryCount);
                        Assert.Equal(
                            sample.Information.UncompressedPayloadSize,
                            information.UncompressedPayloadSize
                        );
                        Assert.Equal(
                            sample.Information.CompressedPayloadSize,
                            information.CompressedPayloadSize
                        );
                        Assert.Equal(0, stream.Position);
                    })
                )
        );
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
                        zip.Detector.DetectArchive(stream)?.ContainerType
                    );
                    Assert.Null(empty.Detector.DetectArchive(stream));
                    Assert.Equal(0, stream.Position);
                    Assert.NotNull(
                        await zip.Inspector.InspectArchiveAsync(stream, CancellationToken.None)
                    );
                    Assert.Null(
                        await empty.Inspector.InspectArchiveAsync(stream, CancellationToken.None)
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
                await services.EntryExtractor.WriteToAsync(
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
            services.EntryExtractor.WriteTo(Assert.Single(archive.Entries), output, null);
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
            services.Detector.DetectArchiveAsync(stream, cancellation.Token).AsTask()
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
        _ = LibraryDefaults.Archives;
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
