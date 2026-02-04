using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;

namespace SharpCompress.Readers;

public static partial class ReaderFactory
{
    /// <summary>
    /// Opens a Reader from a filepath asynchronously
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static ValueTask<IAsyncReader> OpenAsyncReader(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenAsyncReader(new FileInfo(filePath), options, cancellationToken);
    }

    /// <summary>
    /// Opens a Reader from a FileInfo asynchronously
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new ReaderOptions { LeaveStreamOpen = false };
        return OpenAsyncReader(fileInfo.OpenRead(), options, cancellationToken);
    }

    public static async ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        stream.NotNull(nameof(stream));
        options ??= new ReaderOptions() { LeaveStreamOpen = false };

        var sharpCompressStream = new SharpCompressStream(stream);
        sharpCompressStream.StartRecording();

        var factories = Factory.Factories.OfType<Factory>();

        Factory? testedFactory = null;
        if (!string.IsNullOrWhiteSpace(options.ExtensionHint))
        {
            testedFactory = factories.FirstOrDefault(a =>
                a.GetSupportedExtensions()
                    .Contains(options.ExtensionHint, StringComparer.CurrentCultureIgnoreCase)
            );
            if (testedFactory is IReaderFactory readerFactory)
            {
                sharpCompressStream.Rewind();
                if (
                    await testedFactory.IsArchiveAsync(
                        sharpCompressStream,
                        cancellationToken: cancellationToken
                    )
                )
                {
                    sharpCompressStream.Rewind();
                    sharpCompressStream.StopRecording();
                    return await readerFactory.OpenAsyncReader(
                        sharpCompressStream,
                        options,
                        cancellationToken
                    );
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
            sharpCompressStream.Rewind();
            if (
                factory is IReaderFactory readerFactory
                && await factory.IsArchiveAsync(
                    sharpCompressStream,
                    cancellationToken: cancellationToken
                )
            )
            {
                sharpCompressStream.Rewind();
                sharpCompressStream.StopRecording();
                return await readerFactory.OpenAsyncReader(
                    sharpCompressStream,
                    options,
                    cancellationToken
                );
            }
        }

        throw new InvalidFormatException(
            "Cannot determine compressed stream type.  Supported Reader Formats: Arc, Arj, Zip, GZip, BZip2, Tar, Rar, LZip, XZ, ZStandard"
        );
    }
}
