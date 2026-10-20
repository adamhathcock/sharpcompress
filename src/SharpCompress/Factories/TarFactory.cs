using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Compressors.Deflate;
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

    #endregion

    internal static Stream PrepareProbeStream(Stream stream)
    {
        if (stream is GZipStream gzip)
        {
            gzip.BaseStream.IsFormatProbe = true;
        }
        return stream;
    }

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
            return PrepareProbeStream(
                providers.CreateDecompressStream(
                    compressionType,
                    nonDisposingStream,
                    CompressionContext
                        .FromStream(nonDisposingStream)
                        .WithReaderOptions(readerOptions)
                )
            );
        }

        return PrepareProbeStream(
            providers.CreateDecompressStream(compressionType, nonDisposingStream)
        );
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
            return PrepareProbeStream(
                await providers
                    .CreateDecompressStreamAsync(
                        compressionType,
                        nonDisposingStream,
                        CompressionContext
                            .FromStream(nonDisposingStream)
                            .WithReaderOptions(readerOptions),
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            );
        }

        return PrepareProbeStream(
            await providers
                .CreateDecompressStreamAsync(compressionType, nonDisposingStream, cancellationToken)
                .ConfigureAwait(false)
        );
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
                using var decompressedStream = CreateProbeDecompressionStream(
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
                await using var disposal = decompressedStream
                    .DisposeAsyncScope()
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

    /// <summary>
    /// Checks whether <paramref name="stream"/> can use native seeking without bypassing buffered data.
    /// Note: recording wrappers simulate seeking via a ring buffer, even over a non-seekable
    /// underlying stream. These wrappers must remain in the read chain, regardless of whether
    /// their backing stream supports native seeking.
    /// </summary>
    private static bool IsGenuinelySeekable(Stream stream)
    {
        while (stream is SharpCompressStream scs)
        {
            if (!scs.IsPassthrough && scs is not SeekableSharpCompressStream)
            {
                // A recording wrapper may have unread bytes even over a seekable source.
                // Give the probe its own buffer rather than seeking past those bytes.
                return false;
            }
            stream = scs.BaseStream();
        }
        return stream.CanSeek;
    }

    /// <summary>
    /// Creates a new <see cref="SharpCompressStream"/> wrapper around <paramref name="stream"/> with
    /// its own independent recording/rewind scope, isolated from any recording session the caller may
    /// already have in progress on <paramref name="stream"/> itself.
    /// </summary>
    /// <remarks>
    /// When the stream is genuinely seekable, a <see cref="SeekableSharpCompressStream"/> is used:
    /// it delegates recording/rewinding to the underlying stream's native <see cref="Stream.Seek"/> and
    /// never allocates a rewind ring buffer. For non-seekable streams, a ring-buffered
    /// <see cref="SharpCompressStream"/> is required to support rewinding during format detection.
    /// </remarks>
    private static SharpCompressStream CreateNestedRecordingStream(Stream stream)
    {
        if (!IsGenuinelySeekable(stream))
        {
            return new SharpCompressStream(SharpCompressStream.CreateNonDisposing(stream));
        }

        return new SeekableSharpCompressStream(SharpCompressStream.CreateNonDisposing(stream));
    }

    private static SharpCompressStream CreateReaderProbeStream(Stream stream, ReaderOptions options)
    {
        var bufferSize = Math.Max(
            options.RewindableBufferSize ?? 0,
            TarWrapper.MaximumRewindBufferSize
        );
        var probe = CreateNestedRecordingStream(stream);
        probe.StartRecording(bufferSize);
        return probe;
    }

    internal static async ValueTask<SharpCompressStream> BufferLZipHeaderAsync(
        SharpCompressStream stream,
        CancellationToken cancellationToken
    )
    {
        if (stream is not SeekableSharpCompressStream)
        {
            return stream;
        }
        // LZip initializes both its six-byte header and five-byte range coder
        // synchronously. Cache those bytes with async I/O before creating it.
        var buffered = new SharpCompressStream(stream);
        buffered.StartRecording();
        try
        {
            var header = new byte[11];
            await buffered
                .ReadFullyAsync(header, 0, header.Length, cancellationToken)
                .ConfigureAwait(false);
            buffered.Rewind();
            return buffered;
        }
        catch
        {
            await buffered.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options)
    {
        options ??= ReaderOptions.ForExternalStream;
        var sharpCompressStream = CreateReaderProbeStream(stream, options);
        var transferred = false;
        try
        {
            foreach (var wrapper in TarWrapper.Wrappers)
            {
                sharpCompressStream.Rewind();
                if (wrapper.IsMatch(sharpCompressStream))
                {
                    sharpCompressStream.Rewind();
                    bool isTar;
                    using (
                        var decompressedStream = CreateProbeDecompressionStream(
                            sharpCompressStream,
                            wrapper.CompressionType,
                            options
                        )
                    )
                    {
                        isTar = TarArchive.IsTarFile(decompressedStream);
                    }
                    if (isTar)
                    {
                        sharpCompressStream.StopRecording();
                        var reader = new TarReader(
                            sharpCompressStream,
                            options with
                            {
                                LeaveStreamOpen = false,
                            },
                            wrapper.CompressionType,
                            options.LeaveStreamOpen ? null : stream
                        );
                        transferred = true;
                        return reader;
                    }
                }
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        finally
        {
            if (!transferred)
            {
                sharpCompressStream.Dispose();
            }
        }
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
        var sharpCompressStream = CreateReaderProbeStream(stream, options);
        var transferred = false;
        try
        {
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
                    if (wrapper.CompressionType == CompressionType.LZip)
                    {
                        sharpCompressStream = await BufferLZipHeaderAsync(
                                sharpCompressStream,
                                cancellationToken
                            )
                            .ConfigureAwait(false);
                    }
                    bool isTar;
                    {
                        var decompressedStream = await CreateProbeDecompressionStreamAsync(
                                sharpCompressStream,
                                wrapper.CompressionType,
                                options,
                                cancellationToken
                            )
                            .ConfigureAwait(false);
                        await using var disposal = decompressedStream
                            .DisposeAsyncScope()
                            .ConfigureAwait(false);
                        isTar = await TarArchive
                            .IsTarFileAsync(decompressedStream, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    if (isTar)
                    {
                        sharpCompressStream.StopRecording();
                        var reader = new TarReader(
                            sharpCompressStream,
                            options with
                            {
                                LeaveStreamOpen = false,
                            },
                            wrapper.CompressionType,
                            options.LeaveStreamOpen ? null : stream
                        );
                        transferred = true;
                        return reader;
                    }
                }
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        finally
        {
            if (!transferred)
            {
                await sharpCompressStream.DisposeAsync().ConfigureAwait(false);
            }
        }
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
