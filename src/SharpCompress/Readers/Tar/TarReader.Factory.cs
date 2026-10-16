using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;
using SharpCompress.Providers;

namespace SharpCompress.Readers.Tar;

public partial class TarReader
#if NET8_0_OR_GREATER
    : IReaderOpenable
#endif
{
    private static SharpCompressStream CreateReaderProbeStream(Stream stream, ReaderOptions options)
    {
        if (stream is SharpCompressStream { IsPassthrough: false })
        {
            // Keep the caller's recording scope and logical position intact, including
            // frozen recordings whose buffered bytes have not been replayed yet.
            return new SeekableSharpCompressStream(stream);
        }

        return SharpCompressStream.Create(
            stream,
            bufferSize: Math.Max(
                options.RewindableBufferSize ?? 0,
                TarWrapper.MaximumRewindBufferSize
            )
        );
    }

    private static Stream CreateProbeDecompressionStream(
        Stream stream,
        CompressionType compressionType,
        CompressionProviderRegistry providers,
        ReaderOptions options
    )
    {
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(stream);
        if (compressionType == CompressionType.None)
        {
            return nonDisposingStream;
        }

        if (compressionType == CompressionType.GZip)
        {
            return TarFactory.PrepareProbeStream(
                providers.CreateDecompressStream(
                    compressionType,
                    nonDisposingStream,
                    CompressionContext.FromStream(nonDisposingStream).WithReaderOptions(options)
                )
            );
        }

        return providers.CreateDecompressStream(compressionType, nonDisposingStream);
    }

    private static async ValueTask<Stream> CreateProbeDecompressionStreamAsync(
        Stream stream,
        CompressionType compressionType,
        CompressionProviderRegistry providers,
        ReaderOptions options,
        CancellationToken cancellationToken = default
    )
    {
        var nonDisposingStream = SharpCompressStream.CreateNonDisposing(stream);
        if (compressionType == CompressionType.None)
        {
            return nonDisposingStream;
        }

        if (compressionType == CompressionType.GZip)
        {
            return TarFactory.PrepareProbeStream(
                await providers
                    .CreateDecompressStreamAsync(
                        compressionType,
                        nonDisposingStream,
                        CompressionContext
                            .FromStream(nonDisposingStream)
                            .WithReaderOptions(options),
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            );
        }

        return await providers
            .CreateDecompressStreamAsync(compressionType, nonDisposingStream, cancellationToken)
            .ConfigureAwait(false);
    }

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        string filePath,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenAsyncReader(new FileInfo(filePath), readerOptions, cancellationToken);
    }

    public static async ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        stream.NotNull(nameof(stream));
        readerOptions ??= ReaderOptions.ForExternalStream;
        if (readerOptions.LeaveStreamOpen)
        {
            stream = SharpCompressStream.CreateNonDisposing(stream);
            readerOptions = readerOptions with { LeaveStreamOpen = false };
        }
        var sharpCompressStream = CreateReaderProbeStream(stream, readerOptions);
        var transferred = false;
        try
        {
            sharpCompressStream.StartRecording();
            long pos = sharpCompressStream.Position;
            foreach (var wrapper in TarWrapper.Wrappers)
            {
                sharpCompressStream.Position = pos;
                if (
                    !await wrapper
                        .IsMatchAsync(sharpCompressStream, cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    continue;
                }

                sharpCompressStream.Position = pos;
                if (wrapper.CompressionType == CompressionType.LZip)
                {
                    sharpCompressStream = await TarFactory
                        .BufferLZipHeaderAsync(sharpCompressStream, cancellationToken)
                        .ConfigureAwait(false);
                    pos = sharpCompressStream.Position;
                }
                bool isTar;
                {
#if STREAM_DISPOSEASYNC
                    await using var testStream = await CreateProbeDecompressionStreamAsync(
#else
                    using var testStream = await CreateProbeDecompressionStreamAsync(
#endif
                            sharpCompressStream,
                            wrapper.CompressionType,
                            readerOptions.Providers,
                            readerOptions,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    isTar = await TarArchive
                        .IsTarFileAsync(testStream, cancellationToken)
                        .ConfigureAwait(false);
                }
                if (isTar)
                {
                    sharpCompressStream.Position = pos;
                    var reader = new TarReader(
                        sharpCompressStream,
                        readerOptions,
                        wrapper.CompressionType
                    );
                    transferred = true;
                    return reader;
                }

                if (wrapper.CompressionType != CompressionType.None)
                {
                    throw new InvalidFormatException("Not a tar file.");
                }
            }

            sharpCompressStream.Position = pos;
            var fallbackReader = new TarReader(
                sharpCompressStream,
                readerOptions,
                CompressionType.None
            );
            transferred = true;
            return fallbackReader;
        }
        finally
        {
            // The reader owns the probe wrappers only after a successful open.
            if (!transferred)
            {
#if STREAM_DISPOSEASYNC
                await sharpCompressStream.DisposeAsync().ConfigureAwait(false);
#else
                sharpCompressStream.Dispose();
#endif
            }
        }
    }

    public static async ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        readerOptions ??= ReaderOptions.ForFilePath;
        var stream = fileInfo.OpenAsyncReadStream(cancellationToken);
        return await OpenAsyncReader(stream, readerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public static IReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static IReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        readerOptions ??= ReaderOptions.ForFilePath;
        return OpenReader(fileInfo.OpenRead(), readerOptions);
    }

    /// <summary>
    /// Opens a TarReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    /// <returns></returns>
    public static IReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.RequireReadable();
        readerOptions ??= ReaderOptions.ForExternalStream;
        if (readerOptions.LeaveStreamOpen)
        {
            stream = SharpCompressStream.CreateNonDisposing(stream);
            readerOptions = readerOptions with { LeaveStreamOpen = false };
        }
        var sharpCompressStream = CreateReaderProbeStream(stream, readerOptions);
        sharpCompressStream.StartRecording();
        long pos = sharpCompressStream.Position;
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Position = pos;
            if (!wrapper.IsMatch(sharpCompressStream))
            {
                continue;
            }

            sharpCompressStream.Position = pos;
            using var testStream = CreateProbeDecompressionStream(
                sharpCompressStream,
                wrapper.CompressionType,
                readerOptions.Providers,
                readerOptions
            );
            if (TarArchive.IsTarFile(testStream))
            {
                sharpCompressStream.Position = pos;
                return new TarReader(sharpCompressStream, readerOptions, wrapper.CompressionType);
            }

            if (wrapper.CompressionType != CompressionType.None)
            {
                throw new InvalidFormatException("Not a tar file.");
            }
        }

        sharpCompressStream.Position = pos;
        return new TarReader(sharpCompressStream, readerOptions, CompressionType.None);
    }
}
