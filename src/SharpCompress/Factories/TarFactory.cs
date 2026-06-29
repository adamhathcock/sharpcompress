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
        IWritableArchiveFactory<TarWriterOptions>
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
    public override bool IsArchive(Stream stream, ReaderOptions readerOptions)
    {
        return TarArchive.IsTarFile(stream);
    }

    /// <inheritdoc/>
    public override async ValueTask<bool> IsArchiveAsync(
        Stream stream,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        return await TarArchive.IsTarFileAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    internal override int? MinimumReaderDetectionBufferSize => TarWrapper.MaximumRewindBufferSize;

    internal override FactoryDetectionResult Detect(
        Stream stream,
        ReaderOptions readerOptions,
        FactoryDetectionTarget target
    )
    {
        return DetectMatch(stream, readerOptions, target).Result;
    }

    internal override FactoryDetectionMatch DetectMatch(
        Stream stream,
        ReaderOptions readerOptions,
        FactoryDetectionTarget target
    )
    {
        var startPosition = stream.Position;
        if (TarArchive.IsTarFile(stream))
        {
            return new FactoryDetectionMatch(
                FactoryDetectionResult.Match,
                this,
                CompressionType.None
            );
        }

        stream.Seek(startPosition, SeekOrigin.Begin);
        if (!TryGetCompressedTarType(stream, readerOptions, out var compressionType))
        {
            return new FactoryDetectionMatch(FactoryDetectionResult.NoMatch, null, null);
        }

        var result =
            target == FactoryDetectionTarget.Archive
                ? FactoryDetectionResult.Unsupported
                : FactoryDetectionResult.Match;
        return new FactoryDetectionMatch(result, this, compressionType);
    }

    internal override async ValueTask<FactoryDetectionResult> DetectAsync(
        Stream stream,
        ReaderOptions readerOptions,
        FactoryDetectionTarget target,
        CancellationToken cancellationToken = default
    )
    {
        var match = await DetectMatchAsync(stream, readerOptions, target, cancellationToken)
            .ConfigureAwait(false);
        return match.Result;
    }

    internal override async ValueTask<FactoryDetectionMatch> DetectMatchAsync(
        Stream stream,
        ReaderOptions readerOptions,
        FactoryDetectionTarget target,
        CancellationToken cancellationToken = default
    )
    {
        var startPosition = stream.Position;
        if (await TarArchive.IsTarFileAsync(stream, cancellationToken).ConfigureAwait(false))
        {
            return new FactoryDetectionMatch(
                FactoryDetectionResult.Match,
                this,
                CompressionType.None
            );
        }

        stream.Seek(startPosition, SeekOrigin.Begin);
        var compressionType = await TryGetCompressedTarTypeAsync(
                stream,
                readerOptions,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (compressionType is null)
        {
            return new FactoryDetectionMatch(FactoryDetectionResult.NoMatch, null, null);
        }

        var result =
            target == FactoryDetectionTarget.Archive
                ? FactoryDetectionResult.Unsupported
                : FactoryDetectionResult.Match;
        return new FactoryDetectionMatch(result, this, compressionType);
    }

    #endregion

    private static Stream CreateProbeDecompressionStream(
        Stream stream,
        CompressionType compressionType,
        IReaderOptions? readerOptions = null
    )
    {
        var providers = readerOptions?.Providers ?? CompressionProviderRegistry.Default;
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(stream);
        if (compressionType == CompressionType.None)
        {
            return nonDisposingStream;
        }

        if (compressionType == CompressionType.GZip && readerOptions is not null)
        {
            return providers.CreateDecompressStream(
                compressionType,
                nonDisposingStream,
                CompressionContext.FromStream(nonDisposingStream).WithReaderOptions(readerOptions)
            );
        }

        return providers.CreateDecompressStream(compressionType, nonDisposingStream);
    }

    private static async ValueTask<Stream> CreateProbeDecompressionStreamAsync(
        Stream stream,
        CompressionType compressionType,
        IReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(stream);
        if (compressionType == CompressionType.None)
        {
            return nonDisposingStream;
        }
        var providers = readerOptions?.Providers ?? CompressionProviderRegistry.Default;

        if (compressionType == CompressionType.GZip && readerOptions is not null)
        {
            return await providers
                .CreateDecompressStreamAsync(
                    compressionType,
                    nonDisposingStream,
                    CompressionContext
                        .FromStream(nonDisposingStream)
                        .WithReaderOptions(readerOptions),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return await providers
            .CreateDecompressStreamAsync(compressionType, nonDisposingStream, cancellationToken)
            .ConfigureAwait(false);
    }

    public static CompressionType GetCompressionType(
        Stream stream,
        IReaderOptions? readerOptions = null
    )
    {
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
                    readerOptions
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
        IReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
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
                        readerOptions,
                        cancellationToken: cancellationToken
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

    private static bool TryGetCompressedTarType(
        Stream stream,
        ReaderOptions readerOptions,
        out CompressionType compressionType
    )
    {
        var startPosition = stream.Position;
        compressionType = CompressionType.Unknown;
        try
        {
            foreach (var wrapper in TarWrapper.Wrappers)
            {
                if (wrapper.CompressionType == CompressionType.None)
                {
                    continue;
                }

                stream.Seek(startPosition, SeekOrigin.Begin);
                if (!wrapper.IsMatch(stream))
                {
                    continue;
                }

                stream.Seek(startPosition, SeekOrigin.Begin);
                var decompressedStream = CreateProbeDecompressionStream(
                    stream,
                    wrapper.CompressionType,
                    readerOptions
                );
                if (TarArchive.IsTarFile(decompressedStream))
                {
                    compressionType = wrapper.CompressionType;
                    return true;
                }
            }

            return false;
        }
        finally
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
        }
    }

    private static async ValueTask<CompressionType?> TryGetCompressedTarTypeAsync(
        Stream stream,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        var startPosition = stream.Position;
        try
        {
            foreach (var wrapper in TarWrapper.Wrappers)
            {
                if (wrapper.CompressionType == CompressionType.None)
                {
                    continue;
                }

                stream.Seek(startPosition, SeekOrigin.Begin);
                if (!await wrapper.IsMatchAsync(stream, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                stream.Seek(startPosition, SeekOrigin.Begin);
                var decompressedStream = await CreateProbeDecompressionStreamAsync(
                        stream,
                        wrapper.CompressionType,
                        readerOptions,
                        cancellationToken: cancellationToken
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
        catch (InvalidFormatException) { }
        finally
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
        }

        return null;
    }

    internal override bool TryOpenReader(
        SharpCompressStream stream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        try
        {
            stream.Rewind();
            reader = OpenReader(stream, options);
            return true;
        }
        catch (InvalidFormatException)
        {
            stream.Rewind();
            reader = null;
            return false;
        }
    }

    internal override async ValueTask<IAsyncReader?> TryOpenReaderAsync(
        SharpCompressStream stream,
        ReaderOptions options,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            stream.Rewind();
            return await OpenAsyncReader(stream, options, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidFormatException)
        {
            stream.Rewind();
            return null;
        }
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
    public async ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await TarArchive
            .OpenAsyncArchive(streams, readerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IArchive OpenArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    ) => TarArchive.OpenArchive(fileInfos, readerOptions);

    /// <inheritdoc/>
    public async ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await TarArchive
            .OpenAsyncArchive(fileInfos, readerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options)
    {
        options ??= ReaderOptions.ForExternalStream;
        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording(TarWrapper.MaximumRewindBufferSize);
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Rewind();
            if (wrapper.IsMatch(sharpCompressStream))
            {
                sharpCompressStream.Rewind();
                var decompressedStream = CreateProbeDecompressionStream(
                    sharpCompressStream,
                    wrapper.CompressionType,
                    options
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
        options ??= ReaderOptions.ForExternalStream;
        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording(TarWrapper.MaximumRewindBufferSize);
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
                        options,
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

        throw new InvalidFormatException("Not a tar file.");
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

        if (!stream.CanWrite)
        {
            throw new ArgumentException("Tars require writable streams.");
        }
        return new TarWriter(stream, tarOptions);
    }

    /// <inheritdoc/>
    public async ValueTask<IAsyncWriter> OpenAsyncWriter(
        Stream stream,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        TarWriterOptions tarOptions = writerOptions switch
        {
            TarWriterOptions two => two,
            WriterOptions wo => new TarWriterOptions(wo),
            _ => throw new ArgumentException(
                $"Expected WriterOptions or TarWriterOptions, got {writerOptions.GetType().Name}",
                nameof(writerOptions)
            ),
        };

        if (!stream.CanWrite)
        {
            throw new ArgumentException("Tars require writable streams.");
        }
        if (writerOptions.LeaveStreamOpen)
        {
            stream = SharpCompressStream.CreateNonDisposing(stream);
        }

        var providers = writerOptions.Providers;

        stream = writerOptions.CompressionType switch
        {
            CompressionType.None => stream,
            CompressionType.BZip2 => await providers
                .CreateCompressStreamAsync(
                    CompressionType.BZip2,
                    stream,
                    writerOptions.CompressionLevel,
                    cancellationToken
                )
                .ConfigureAwait(false),
            CompressionType.GZip => await providers
                .CreateCompressStreamAsync(
                    CompressionType.GZip,
                    stream,
                    writerOptions.CompressionLevel,
                    cancellationToken
                )
                .ConfigureAwait(false),
            CompressionType.LZip => await providers
                .CreateCompressStreamAsync(
                    CompressionType.LZip,
                    stream,
                    writerOptions.CompressionLevel,
                    cancellationToken
                )
                .ConfigureAwait(false),
            _ => throw new InvalidFormatException(
                "Tar does not support compression: " + writerOptions.CompressionType
            ),
        };
        return new TarWriter(stream, tarOptions, streamIsPrepared: true);
    }

    #endregion

    #region IWritableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive<TarWriterOptions> CreateArchive() => TarArchive.CreateArchive();

    #endregion
}
