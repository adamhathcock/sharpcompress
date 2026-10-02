using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Factories;
using SharpCompress.Providers;
using SharpCompress.Providers.Default;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test;

public class SharpCompressClientTests : TestBase
{
    [Fact]
    public void FormatRegistry_CopiesCollectionsAndRetainsReplacementPriority()
    {
        IFactory[] factories = [new ZipFactory(), new TarFactory()];
        var registry = new FormatRegistry(factories);
        factories[0] = new GZipFactory();
        var replacement = new ZipFactory();
        var replaced = registry.With(replacement);
        var custom = new ReaderOnlyFactory();
        var extended = registry.With(custom, prepend: true);

        Assert.IsType<ZipFactory>(registry.Factories[0]);
        Assert.Same(replacement, replaced.Factories[0]);
        Assert.Same(registry.Factories[1], replaced.Factories[1]);
        Assert.Same(custom, extended.Factories[0]);
        Assert.Equal(2, registry.Factories.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<IFactory>)registry.Factories).Add(custom)
        );
        Assert.Throws<NotSupportedException>(() =>
            ((IList<TarWrapper>)registry.TarWrappers).Clear()
        );
        Assert.Empty(FormatRegistry.Empty.TarWrappers);
    }

    [Fact]
    public async Task Clients_RecognizeFormatsIndependentlyDuringConcurrentUsage()
    {
        var zipClient = new SharpCompressClient(
            new SharpCompressConfiguration(FormatRegistry.Empty.With(new ZipFactory()))
        );
        var emptyClient = new SharpCompressClient(
            new SharpCompressConfiguration(FormatRegistry.Empty)
        );
        var path = Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip");

        await Task.WhenAll(
            Enumerable
                .Range(0, 10)
                .Select(async _ =>
                {
                    using var zipStream = File.OpenRead(path);
                    using var emptyStream = File.OpenRead(path);
                    Assert.Equal(
                        ArchiveType.Zip,
                        (
                            await zipClient.Inspector.DetectArchiveAsync(
                                zipStream,
                                cancellationToken: CancellationToken.None
                            )
                        )?.ContainerType
                    );
                    Assert.Null(
                        await emptyClient.Inspector.DetectArchiveAsync(
                            emptyStream,
                            cancellationToken: CancellationToken.None
                        )
                    );
                    Assert.Equal(0, zipStream.Position);
                    Assert.Equal(0, emptyStream.Position);
                    using var archive = zipClient.OpenArchive(zipStream);
                    Assert.NotEmpty(archive.Entries);
                    Assert.Throws<ArchiveOperationException>(() =>
                        emptyClient.OpenArchive(emptyStream)
                    );
                })
        );
        Assert.Equal(ArchiveType.Zip, ArchiveFactory.DetectArchive(path)?.ContainerType);
        using var output = new MemoryStream();
        Assert.Throws<NotSupportedException>(() =>
            emptyClient.OpenWriter(
                output,
                ArchiveType.Zip,
                new ZipWriterOptions(CompressionType.Deflate)
            )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Client_RecognizesInterfaceOnlyReaderFactoryOnNonSeekableStream(bool useAsync)
    {
        var factory = new ReaderOnlyFactory();
        var client = new SharpCompressClient(
            new SharpCompressConfiguration(FormatRegistry.Empty.With(factory))
        );
        using var stream = new ForwardOnlyStream(
            File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"))
        );
        var options = new ReaderOptions { ExtensionHint = "custom", LeaveStreamOpen = true };
        if (useAsync)
        {
            await using var reader = await client.OpenAsyncReader(
                stream,
                options,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
        }
        else
        {
            using var reader = client.OpenReader(stream, options);
            Assert.True(reader.MoveToNextEntry());
        }
        Assert.True(stream.CanRead);
        Assert.Equal(1, factory.OpenCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Client_ResolvesProvidersWithoutMutatingOperationOptions(bool useAsync)
    {
        var provider = new TrackingDeflateProvider();
        var registry = CompressionProviderRegistry.Default.With(provider);
        var client = new SharpCompressClient(new SharpCompressConfiguration(providers: registry));
        var options = new ZipWriterOptions(CompressionType.Deflate);
        var readerOptions = new ReaderOptions { LeaveStreamOpen = true };
        using var stream = new MemoryStream();
        using var payload = new MemoryStream(Encoding.UTF8.GetBytes("client-local provider"));
        if (useAsync)
        {
            await using var writer = await client.OpenAsyncWriter(
                stream,
                ArchiveType.Zip,
                options,
                CancellationToken.None
            );
            await writer.WriteAsync("payload.txt", payload, null, CancellationToken.None);
        }
        else
        {
            using var writer = client.OpenWriter(stream, ArchiveType.Zip, options);
            writer.Write("payload.txt", payload, null);
        }
        Assert.True(provider.CompressionCalls > 0);
        Assert.Same(CompressionProviderRegistry.Default, options.Providers);
        stream.Position = 0;
        using var extracted = new MemoryStream();
        if (useAsync)
        {
            await using var reader = await client.OpenAsyncReader(
                stream,
                readerOptions,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            await client.Extractor.ExtractToStreamAsync(
                reader,
                extracted,
                cancellationToken: CancellationToken.None
            );
        }
        else
        {
            using var reader = client.OpenReader(stream, readerOptions);
            Assert.True(reader.MoveToNextEntry());
            client.Extractor.ExtractToStream(reader, extracted);
        }
        Assert.True(provider.DecompressionCalls > 0);
        Assert.Equal("client-local provider", Encoding.UTF8.GetString(extracted.ToArray()));
        Assert.Same(CompressionProviderRegistry.Default, readerOptions.Providers);
        Assert.True(stream.CanRead);
        Assert.True(extracted.CanWrite);

        var calls = provider.DecompressionCalls;
        stream.Position = 0;
        using var archive = client.OpenArchive(
            stream,
            readerOptions with
            {
                Providers = CompressionProviderRegistry.Default,
            }
        );
        using var entryStream = archive.Entries.Single().OpenEntryStream();
        entryStream.CopyTo(Stream.Null);
        Assert.Equal(calls, provider.DecompressionCalls);

        stream.Position = 0;
        using var configuredArchive = client.OpenArchive(stream, readerOptions);
        Assert.Same(registry, configuredArchive.ReaderOptions.Providers);
        using var configuredEntryStream = configuredArchive.Entries.Single().OpenEntryStream();
        configuredEntryStream.CopyTo(Stream.Null);
        Assert.True(provider.DecompressionCalls > calls);
    }

    [Theory]
    [InlineData("Zip.deflate.zip", false)]
    [InlineData("Zip.deflate.zip", true)]
    [InlineData("Tar.tar", false)]
    [InlineData("Tar.tar", true)]
    [InlineData("Rar.rar", false)]
    [InlineData("Rar.rar", true)]
    [InlineData("7Zip.nonsolid.7z", false)]
    [InlineData("7Zip.nonsolid.7z", true)]
    public async Task Extractor_ExtractsArchivesThroughInstanceServices(
        string archiveName,
        bool useAsync
    )
    {
        UseExtensionInsteadOfNameToVerify = true;
        var client = new SharpCompressClient();
        var path = Path.Combine(TEST_ARCHIVES_PATH, archiveName);
        if (useAsync)
        {
            await using var archive = await client.OpenAsyncArchive(
                path,
                cancellationToken: CancellationToken.None
            );
            await client.Extractor.ExtractToDirectoryAsync(
                archive,
                SCRATCH_FILES_PATH,
                cancellationToken: CancellationToken.None
            );
        }
        else
        {
            using var archive = client.OpenArchive(path);
            client.Extractor.ExtractToDirectory(archive, SCRATCH_FILES_PATH);
        }
        VerifyFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileWriter_WritesRelativePathsAndHonorsFilter(bool useAsync)
    {
        var client = new SharpCompressClient();
        var directory = CreateScratch2Directory("sources");
        Directory.CreateDirectory(Path.Combine(directory, "nested"));
        File.WriteAllText(Path.Combine(directory, "first.txt"), "first");
        File.WriteAllText(Path.Combine(directory, "skip.tmp"), "skip");
        File.WriteAllText(Path.Combine(directory, "nested", "second.txt"), "second");
        using var stream = new MemoryStream();
        if (useAsync)
        {
            await using var writer = await client.OpenAsyncWriter(
                stream,
                ArchiveType.Zip,
                new WriterOptions(CompressionType.Deflate),
                CancellationToken.None
            );
            await client.FileWriter.WriteDirectoryAsync(
                writer,
                directory + Path.DirectorySeparatorChar,
                filter: file => file.EndsWith(".txt", StringComparison.Ordinal),
                searchOption: SearchOption.AllDirectories,
                cancellationToken: CancellationToken.None
            );
        }
        else
        {
            using var writer = client.OpenWriter(
                stream,
                ArchiveType.Zip,
                new WriterOptions(CompressionType.Deflate)
            );
            client.FileWriter.WriteDirectory(
                writer,
                directory,
                filter: file => file.EndsWith(".txt", StringComparison.Ordinal),
                searchOption: SearchOption.AllDirectories
            );
        }
        stream.Position = 0;
        using var archive = client.OpenArchive(stream);
        Assert.Equal(
            new[] { "first.txt", "nested/second.txt" },
            archive.Entries.Select(entry => entry.Key).OrderBy(key => key, StringComparer.Ordinal)
        );
        client.Extractor.ExtractToDirectory(archive, SCRATCH_FILES_PATH);
        Assert.Equal("first", File.ReadAllText(GetScratchPath("first.txt")));
        Assert.Equal("second", File.ReadAllText(GetScratchPath("nested", "second.txt")));
        Assert.False(File.Exists(GetScratchPath("skip.tmp")));
    }

    [Fact]
    public async Task Client_AsyncWorkflowsRespectPreCanceledTokens()
    {
        var client = new SharpCompressClient();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var stream = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.OpenAsyncReader(stream, cancellationToken: cancellation.Token).AsTask()
        );
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client
                .Inspector.InspectArchiveAsync(stream, cancellationToken: cancellation.Token)
                .AsTask()
        );
        await using var archive = await client.OpenAsyncArchive(
            stream,
            cancellationToken: CancellationToken.None
        );
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client
                .Extractor.ExtractToDirectoryAsync(
                    archive,
                    SCRATCH_FILES_PATH,
                    cancellationToken: cancellation.Token
                )
                .AsTask()
        );
        using var output = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client
                .OpenAsyncWriter(
                    output,
                    ArchiveType.Zip,
                    new WriterOptions(CompressionType.Deflate),
                    cancellation.Token
                )
                .AsTask()
        );
        Assert.True(stream.CanRead);
        Assert.True(output.CanWrite);
        Assert.Empty(Directory.EnumerateFileSystemEntries(SCRATCH_FILES_PATH));
    }

    [Theory]
    [InlineData("Tar.tar.gz", false)]
    [InlineData("Tar.tar.gz", true)]
    [InlineData("Tar.tar.bz2", false)]
    [InlineData("Tar.tar.bz2", true)]
    public async Task Clients_UseTheirOwnTarWrapperSelection(string archiveName, bool useAsync)
    {
        var plainWrappers = TarWrapper.Wrappers.Where(wrapper =>
            wrapper.CompressionType == CompressionType.None
        );
        var plainClient = new SharpCompressClient(
            new SharpCompressConfiguration(new FormatRegistry([new TarFactory()], plainWrappers))
        );
        var defaultClient = new SharpCompressClient();
        var zipClient = new SharpCompressClient(
            new SharpCompressConfiguration(new FormatRegistry([new ZipFactory()]))
        );
        var path = Path.Combine(TEST_ARCHIVES_PATH, archiveName);
        using var stream = new ForwardOnlyStream(File.OpenRead(path));
        if (useAsync)
        {
            Assert.Null(
                await plainClient.Inspector.DetectArchiveAsync(
                    path,
                    cancellationToken: CancellationToken.None
                )
            );
            Assert.Null(
                await zipClient.Inspector.DetectArchiveAsync(
                    path,
                    cancellationToken: CancellationToken.None
                )
            );
            Assert.Equal(
                ArchiveType.Tar,
                (
                    await defaultClient.Inspector.DetectArchiveAsync(
                        path,
                        cancellationToken: CancellationToken.None
                    )
                )?.ContainerType
            );
            await Assert.ThrowsAsync<InvalidFormatException>(() =>
                plainClient
                    .OpenAsyncReader(stream, cancellationToken: CancellationToken.None)
                    .AsTask()
            );
            await using var reader = await defaultClient.OpenAsyncReader(
                path,
                cancellationToken: CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
        }
        else
        {
            Assert.Null(plainClient.Inspector.DetectArchive(path));
            Assert.Null(zipClient.Inspector.DetectArchive(path));
            Assert.Equal(
                ArchiveType.Tar,
                defaultClient.Inspector.DetectArchive(path)?.ContainerType
            );
            Assert.Throws<InvalidFormatException>(() => plainClient.OpenReader(stream));
            using var reader = defaultClient.OpenReader(path);
            Assert.True(reader.MoveToNextEntry());
        }
        Assert.True(stream.CanRead);
        Assert.Equal(
            plainWrappers.Max(wrapper => wrapper.MinimumRewindBufferSize),
            plainClient.Configuration.Formats.MaximumRewindBufferSize
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileWriter_AddsAndSavesWritableArchivesUsingClientProviders(bool useAsync)
    {
        var provider = new TrackingDeflateProvider();
        var client = new SharpCompressClient(
            new SharpCompressConfiguration(
                providers: CompressionProviderRegistry.Default.With(provider)
            )
        );
        var directory = CreateScratch2Directory("sources");
        File.WriteAllText(Path.Combine(directory, "payload.txt"), "writable archive");
        var output = new FileInfo(GetScratch2Path("saved.zip"));
        var options = new ZipWriterOptions(CompressionType.Deflate);
        using (var archive = client.CreateArchive<ZipWriterOptions>())
        {
            Assert.Same(client.Configuration.Providers, archive.ReaderOptions.Providers);
            if (useAsync)
            {
                await client.FileWriter.AddDirectoryAsync(
                    (IWritableAsyncArchive)archive,
                    directory,
                    cancellationToken: CancellationToken.None
                );
                await client.FileWriter.SaveToFileAsync(
                    (IWritableAsyncArchive<ZipWriterOptions>)archive,
                    output,
                    options,
                    CancellationToken.None
                );
            }
            else
            {
                client.FileWriter.AddDirectory(archive, directory);
                client.FileWriter.SaveToFile(archive, output, options);
            }
        }
        Assert.True(provider.CompressionCalls > 0);
        Assert.Same(CompressionProviderRegistry.Default, options.Providers);
        using var saved = client.OpenArchive(output);
        using var payload = saved.Entries.Single().OpenEntryStream();
        using var reader = new StreamReader(payload);
        Assert.Equal("writable archive", reader.ReadToEnd());
    }

    [Fact]
    public void Client_ExplicitWriterProvidersOverrideClientDefaults()
    {
        var provider = new TrackingDeflateProvider();
        var client = new SharpCompressClient(
            new SharpCompressConfiguration(
                providers: CompressionProviderRegistry.Default.With(provider)
            )
        );
        using var stream = new MemoryStream();
        var options = new ZipWriterOptions(CompressionType.Deflate)
        {
            Providers = CompressionProviderRegistry.Default,
            UseZip64 = true,
        };
        using (var writer = client.OpenWriter(stream, ArchiveType.Zip, options))
        using (var payload = new MemoryStream(Encoding.UTF8.GetBytes("explicit provider")))
        {
            writer.Write("payload.txt", payload, null);
        }
        Assert.Equal(0, provider.CompressionCalls);
        Assert.True(options.UseZip64);
        Assert.True(stream.CanWrite);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Client_ProviderDefaultsSurviveWriterOptionConversionAndCopies(
        bool useInterfaceConstructor,
        bool explicitOverride
    )
    {
        var provider = new TrackingDeflateProvider();
        var client = new SharpCompressClient(
            new SharpCompressConfiguration(
                providers: CompressionProviderRegistry.Default.With(provider)
            )
        );
        var source = new WriterOptions(CompressionType.Deflate);
        if (explicitOverride)
        {
            source.Providers = CompressionProviderRegistry.Default;
        }
        var converted = useInterfaceConstructor
            ? new ZipWriterOptions((IWriterOptions)source)
            : new ZipWriterOptions(source);
        var options = converted with { CompressionLevel = 9 };
        using var stream = new MemoryStream();
        using (var writer = client.OpenWriter(stream, ArchiveType.Zip, options))
        using (var payload = new MemoryStream(Encoding.UTF8.GetBytes("converted options")))
        {
            writer.Write("payload.txt", payload, null);
        }
        Assert.Equal(!explicitOverride, provider.CompressionCalls > 0);
        Assert.Same(CompressionProviderRegistry.Default, converted.Providers);
        Assert.Same(CompressionProviderRegistry.Default, options.Providers);
    }

    private sealed class ReaderOnlyFactory : IReaderFactory
    {
        private readonly ZipFactory inner = new();
        public int OpenCount;
        public string Name => "Custom ZIP reader";
        public ArchiveType? KnownArchiveType => null;

        public IEnumerable<string> GetSupportedExtensions() => ["custom"];

        public bool IsArchive(Stream stream, ReaderOptions options) =>
            inner.IsArchive(stream, options);

        public ValueTask<bool> IsArchiveAsync(
            Stream stream,
            ReaderOptions options,
            CancellationToken cancellationToken = default
        ) => inner.IsArchiveAsync(stream, options, cancellationToken);

        public FileInfo? GetFilePart(int index, FileInfo part1) => null;

        public IReader OpenReader(Stream stream, ReaderOptions? options)
        {
            OpenCount++;
            return inner.OpenReader(stream, options);
        }

        public ValueTask<IAsyncReader> OpenAsyncReader(
            Stream stream,
            ReaderOptions? options,
            CancellationToken cancellationToken = default
        )
        {
            OpenCount++;
            return inner.OpenAsyncReader(stream, options, cancellationToken);
        }
    }

    private sealed class TrackingDeflateProvider : CompressionProviderBase
    {
        private readonly DeflateCompressionProvider inner = new();
        public int CompressionCalls;
        public int DecompressionCalls;
        public override CompressionType CompressionType => CompressionType.Deflate;
        public override bool SupportsCompression => true;
        public override bool SupportsDecompression => true;

        public override Stream CreateCompressStream(Stream destination, int compressionLevel)
        {
            Interlocked.Increment(ref CompressionCalls);
            return inner.CreateCompressStream(destination, compressionLevel);
        }

        public override Stream CreateDecompressStream(Stream source)
        {
            Interlocked.Increment(ref DecompressionCalls);
            return inner.CreateDecompressStream(source);
        }
    }
}
