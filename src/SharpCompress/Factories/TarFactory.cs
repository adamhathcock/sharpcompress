using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.IO;
using SharpCompress.Providers;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Factories;

/// <summary>
/// Represents the foundation factory of TAR archive.
/// </summary>
public class TarFactory
    : Factory,
        IArchiveFactory,
        IMultiArchiveFactory,
        IReaderFactory,
        IWriterFactory,
        IWriteableArchiveFactory<TarWriterOptions>
{
    #region IFactory

    /// <inheritdoc/>
    public override string Name => "Tar";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.Tar;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        foreach (var testOption in TarWrapper.Wrappers)
        {
            foreach (var ext in testOption.KnownExtensions)
            {
                yield return ext;
            }
        }
    }

    /// <inheritdoc/>
    public override bool IsArchive(Stream stream, string? password = null)
    {
        var providers = CompressionProviderRegistry.Default;
        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording();
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Rewind();
            if (wrapper.IsMatch(sharpCompressStream))
            {
                sharpCompressStream.Rewind();
                var decompressedStream = CreateProbeDecompressionStream(
                    sharpCompressStream,
                    wrapper.CompressionType,
                    providers
                );
                if (TarArchive.IsTarFile(decompressedStream))
                {
                    sharpCompressStream.Rewind();
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public override async ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        CancellationToken cancellationToken = default
    )
    {
        var providers = CompressionProviderRegistry.Default;
        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording();
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Rewind();
            if (
                await wrapper
                    .IsMatchAsync(sharpCompressStream, cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                sharpCompressStream.Rewind();
                var decompressedStream = await CreateProbeDecompressionStreamAsync(
                        sharpCompressStream,
                        wrapper.CompressionType,
                        providers,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (
                    await TarArchive
                        .IsTarFileAsync(decompressedStream, cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    sharpCompressStream.Rewind();
                    return true;
                }
            }
        }

        return false;
    }

    #endregion

    private static Stream CreateProbeDecompressionStream(
        Stream stream,
        CompressionType compressionType,
        CompressionProviderRegistry providers
    )
    {
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(stream);
        return compressionType == CompressionType.None
            ? nonDisposingStream
            : providers.CreateDecompressStream(compressionType, nonDisposingStream);
    }

    private static async ValueTask<Stream> CreateProbeDecompressionStreamAsync(
        Stream stream,
        CompressionType compressionType,
        CompressionProviderRegistry providers,
        CancellationToken cancellationToken = default
    )
    {
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(stream);
        if (compressionType == CompressionType.None)
        {
            return nonDisposingStream;
        }
        return await providers
            .CreateDecompressStreamAsync(compressionType, nonDisposingStream, cancellationToken)
            .ConfigureAwait(false);
    }

    public static CompressionType GetCompressionType(
        Stream stream,
        CompressionProviderRegistry? providers = null
    )
    {
        providers ??= CompressionProviderRegistry.Default;
        stream.Seek(0, SeekOrigin.Begin);
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            stream.Seek(0, SeekOrigin.Begin);
            if (wrapper.IsMatch(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                var decompressedStream = CreateProbeDecompressionStream(
                    stream,
                    wrapper.CompressionType,
                    providers
                );
                if (TarArchive.IsTarFile(decompressedStream))
                {
                    return wrapper.CompressionType;
                }
            }
        }
        throw new InvalidFormatException("Not a tar file.");
    }

    public static async ValueTask<CompressionType> GetCompressionTypeAsync(
        Stream stream,
        CompressionProviderRegistry? providers = null,
        CancellationToken cancellationToken = default
    )
    {
        providers ??= CompressionProviderRegistry.Default;
        stream.Seek(0, SeekOrigin.Begin);
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            stream.Seek(0, SeekOrigin.Begin);
            if (await wrapper.IsMatchAsync(stream, cancellationToken).ConfigureAwait(false))
            {
                stream.Seek(0, SeekOrigin.Begin);
                var decompressedStream = await CreateProbeDecompressionStreamAsync(
                        stream,
                        wrapper.CompressionType,
                        providers,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (
                    await TarArchive
                        .IsTarFileAsync(decompressedStream, cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    return wrapper.CompressionType;
                }
            }
        }
        throw new InvalidFormatException("Not a tar file.");
    }

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null) =>
        TarArchive.OpenArchive(stream, readerOptions);

    /// <inheritdoc/>
    public async ValueTask<IAsyncArchive> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) =>
        await TarArchive
            .OpenAsyncArchive(stream, readerOptions, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        TarArchive.OpenArchive(fileInfo, readerOptions);

    /// <inheritdoc/>
    public async ValueTask<IAsyncArchive> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) =>
        await TarArchive
            .OpenAsyncArchive(fileInfo, readerOptions, cancellationToken)
            .ConfigureAwait(false);

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive OpenArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => TarArchive.OpenArchive(streams, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => (IAsyncArchive)OpenArchive(streams, readerOptions);

    /// <inheritdoc/>
    public IArchive OpenArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    ) => TarArchive.OpenArchive(fileInfos, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    ) => (IAsyncArchive)OpenArchive(fileInfos, readerOptions);

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options)
    {
        options ??= new ReaderOptions();
        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording();
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Rewind();
            if (wrapper.IsMatch(sharpCompressStream))
            {
                sharpCompressStream.Rewind();
                var decompressedStream = CreateProbeDecompressionStream(
                    sharpCompressStream,
                    wrapper.CompressionType,
                    options.Providers
                );
                if (TarArchive.IsTarFile(decompressedStream))
                {
                    sharpCompressStream.StopRecording();
                    return new TarReader(sharpCompressStream, options, wrapper.CompressionType);
                }
            }
        }
        throw new InvalidFormatException("Not a tar file.");
    }

    /// <inheritdoc/>
    public async ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new ReaderOptions();
        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording();
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Rewind();
            if (
                await wrapper
                    .IsMatchAsync(sharpCompressStream, cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                sharpCompressStream.Rewind();
                var decompressedStream = await CreateProbeDecompressionStreamAsync(
                        sharpCompressStream,
                        wrapper.CompressionType,
                        options.Providers,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (
                    await TarArchive
                        .IsTarFileAsync(decompressedStream, cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    sharpCompressStream.Rewind();
                    sharpCompressStream.StopRecording();
                    return new TarReader(sharpCompressStream, options, wrapper.CompressionType);
                }
            }
        }

        sharpCompressStream.Rewind();
        return (IAsyncReader)TarReader.OpenReader(sharpCompressStream, options);
    }

    #endregion

    #region IWriterFactory

    /// <inheritdoc/>
    public IWriter OpenWriter(Stream stream, IWriterOptions writerOptions)
    {
        TarWriterOptions tarOptions = writerOptions switch
        {
            TarWriterOptions two => two,
            WriterOptions wo => new TarWriterOptions(wo),
            _ => throw new ArgumentException(
                $"Expected WriterOptions or TarWriterOptions, got {writerOptions.GetType().Name}",
                nameof(writerOptions)
            ),
        };
        return new TarWriter(stream, tarOptions);
    }

    /// <inheritdoc/>
    public IAsyncWriter OpenAsyncWriter(
        Stream stream,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncWriter)OpenWriter(stream, writerOptions);
    }

    #endregion

    #region IWriteableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive<TarWriterOptions> CreateArchive() => TarArchive.CreateArchive();

    #endregion
}
