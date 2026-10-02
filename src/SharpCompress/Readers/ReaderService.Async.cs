using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;

namespace SharpCompress.Readers;

internal sealed partial class ReaderService
{
    /// <summary>
    /// Opens a Reader from a filepath asynchronously
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public ValueTask<IAsyncReader> OpenAsyncReader(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenAsyncReader(
            new FileInfo(filePath),
            options ?? ReaderOptions.ForFilePath,
            cancellationToken
        );
    }

    /// <summary>
    /// Opens a Reader from a FileInfo asynchronously
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= ReaderOptions.ForFilePath;
        var stream = fileInfo.OpenAsyncReadStream(cancellationToken);
        try
        {
            return await OpenAsyncReader(stream, options, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await using var streamScope = stream.DisposeAsyncScope().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        cancellationToken.ThrowIfCancellationRequested();
        options = configuration.PrepareReaderOptions(options, true);

        var sharpCompressStream = SharpCompressStream.Create(
            stream,
            bufferSize: options.RewindableBufferSize
        );
        sharpCompressStream.StartRecording();

        var factories = configuration.Formats.Factories;

        IFactory? testedFactory = null;
        if (!string.IsNullOrWhiteSpace(options.ExtensionHint))
        {
            testedFactory = factories.FirstOrDefault(a =>
                a.GetSupportedExtensions()
                    .Contains(options.ExtensionHint, StringComparer.CurrentCultureIgnoreCase)
            );
            if (testedFactory is not null)
            {
                var reader = await TryOpenReaderAsync(
                        testedFactory,
                        sharpCompressStream,
                        options,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (reader is not null)
                {
                    return reader;
                }
            }
            sharpCompressStream.Rewind();
        }

        foreach (var factory in factories)
        {
            if (testedFactory == factory)
            {
                continue; // Already tested above
            }
            var reader = await TryOpenReaderAsync(
                    factory,
                    sharpCompressStream,
                    options,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (reader is not null)
            {
                return reader;
            }
        }

        throw new InvalidFormatException(
            "Cannot determine compressed stream type. Supported Reader Formats: "
                + string.Join(
                    ", ",
                    factories.OfType<IReaderFactory>().Select(factory => factory.Name)
                )
        );
    }

    private static async ValueTask<IAsyncReader?> TryOpenReaderAsync(
        IFactory factory,
        SharpCompressStream stream,
        ReaderOptions options,
        CancellationToken cancellationToken
    )
    {
        if (factory is Factory builtInFactory)
        {
            return await builtInFactory
                .TryOpenReaderAsync(stream, options, cancellationToken)
                .ConfigureAwait(false);
        }
        stream.Rewind();
        if (
            factory is IReaderFactory readerFactory
            && await factory
                .IsArchiveAsync(stream, options, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            stream.Rewind(true);
            return await readerFactory
                .OpenAsyncReader(stream, options, cancellationToken)
                .ConfigureAwait(false);
        }
        stream.Rewind();
        return null;
    }
}
