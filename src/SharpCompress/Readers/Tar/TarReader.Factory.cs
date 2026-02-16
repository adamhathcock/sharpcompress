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
            return providers.CreateDecompressStream(
                compressionType,
                nonDisposingStream,
                CompressionContext.FromStream(nonDisposingStream).WithReaderOptions(options)
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
            return await providers
                .CreateDecompressStreamAsync(
                    compressionType,
                    nonDisposingStream,
                    CompressionContext.FromStream(nonDisposingStream).WithReaderOptions(options),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return await providers
            .CreateDecompressStreamAsync(compressionType, nonDisposingStream, cancellationToken)
            .ConfigureAwait(false);
    }

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        string path,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        path.NotNullOrEmpty(nameof(path));
        return OpenAsyncReader(new FileInfo(path), readerOptions, cancellationToken);
    }

    public static async ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        stream.NotNull(nameof(stream));
        readerOptions ??= new ReaderOptions();
        var sharpCompressStream = SharpCompressStream.Create(
            stream,
            bufferSize: readerOptions.RewindableBufferSize
        );
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
            var testStream = await CreateProbeDecompressionStreamAsync(
                    sharpCompressStream,
                    wrapper.CompressionType,
                    readerOptions.Providers,
                    readerOptions,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (
                await TarArchive.IsTarFileAsync(testStream, cancellationToken).ConfigureAwait(false)
            )
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

    public static ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        readerOptions ??= new ReaderOptions() { LeaveStreamOpen = false };
        return OpenAsyncReader(fileInfo.OpenRead(), readerOptions, cancellationToken);
    }

    public static IReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static IReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        readerOptions ??= new ReaderOptions() { LeaveStreamOpen = false };
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
        stream.NotNull(nameof(stream));
        readerOptions ??= new ReaderOptions();
        var sharpCompressStream = SharpCompressStream.Create(
            stream,
            bufferSize: readerOptions.RewindableBufferSize
        );
        long pos = sharpCompressStream.Position;
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            sharpCompressStream.Position = pos;
            if (!wrapper.IsMatch(sharpCompressStream))
            {
                continue;
            }

            sharpCompressStream.Position = pos;
            var testStream = CreateProbeDecompressionStream(
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
