using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Services;

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
        return await OpenAsyncReader(stream, options, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        options ??= ReaderOptions.ForExternalStream;

        if (options.LeaveStreamOpen)
        {
            // Internal wrappers must still be disposed when the caller retains its stream.
            stream = SharpCompressStream.CreateNonDisposing(stream);
            options = options with { LeaveStreamOpen = false };
        }

        var sharpCompressStream = SharpCompressStream.Create(
            stream,
            bufferSize: options.RewindableBufferSize
        );
        sharpCompressStream.StartRecording();

        var factories = catalog.Factories.OfType<Factory>();

        Factory? testedFactory = null;
        if (!string.IsNullOrWhiteSpace(options.ExtensionHint))
        {
            testedFactory = factories.FirstOrDefault(a =>
                a.GetSupportedExtensions()
                    .Contains(options.ExtensionHint, StringComparer.CurrentCultureIgnoreCase)
            );
            if (testedFactory is not null)
            {
                var reader = await testedFactory
                    .TryOpenReaderAsync(sharpCompressStream, options, cancellationToken)
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
            var reader = await factory
                .TryOpenReaderAsync(sharpCompressStream, options, cancellationToken)
                .ConfigureAwait(false);
            if (reader is not null)
            {
                return reader;
            }
        }

        throw new InvalidFormatException(
            "Cannot determine compressed stream type.  Supported Reader Formats: Arc, Arj, Zip, GZip, BZip2, Tar, Rar, LZip, XZ, ZStandard"
        );
    }
}
