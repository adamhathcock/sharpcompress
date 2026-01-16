using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;

namespace SharpCompress.Readers;

public static class ReaderFactory
{
    public static IReader OpenReader(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), options);
    }

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

    public static IReader OpenReader(FileInfo fileInfo, ReaderOptions? options = null)
    {
        options ??= new ReaderOptions { LeaveStreamOpen = false };
        return OpenReader(fileInfo.OpenRead(), options);
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

    /// <summary>
    /// Opens a Reader for Non-seeking usage
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IReader OpenReader(Stream stream, ReaderOptions? options = null)
    {
        stream.NotNull(nameof(stream));
        options ??= new ReaderOptions() { LeaveStreamOpen = false };

        var bStream = new SharpCompressStream(stream, bufferSize: options.BufferSize);

        long pos = ((IStreamStack)bStream).GetPosition();

        var factories = Factories.Factory.Factories.OfType<Factories.Factory>();

        Factory? testedFactory = null;

        if (!string.IsNullOrWhiteSpace(options.ExtensionHint))
        {
            testedFactory = factories.FirstOrDefault(a =>
                a.GetSupportedExtensions()
                    .Contains(options.ExtensionHint, StringComparer.CurrentCultureIgnoreCase)
            );
            if (
                testedFactory?.TryOpenReader(bStream, options, out var reader) == true
                && reader != null
            )
            {
                return reader;
            }
            ((IStreamStack)bStream).StackSeek(pos);
        }

        foreach (var factory in factories)
        {
            if (testedFactory == factory)
            {
                continue; // Already tested above
            }
            ((IStreamStack)bStream).StackSeek(pos);
            if (factory.TryOpenReader(bStream, options, out var reader) && reader != null)
            {
                return reader;
            }
        }

        throw new InvalidFormatException(
            "Cannot determine compressed stream type.  Supported Reader Formats: Ace, Arc, Arj, Zip, GZip, BZip2, Tar, Rar, LZip, XZ, ZStandard"
        );
    }

    public static async ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        stream.NotNull(nameof(stream));
        options ??= new ReaderOptions() { LeaveStreamOpen = false };

        var bStream = new SharpCompressStream(stream, bufferSize: options.BufferSize);

        long pos = ((IStreamStack)bStream).GetPosition();

        var factories = Factories.Factory.Factories.OfType<Factories.Factory>();

        Factory? testedFactory = null;

        if (!string.IsNullOrWhiteSpace(options.ExtensionHint))
        {
            testedFactory = factories.FirstOrDefault(a =>
                a.GetSupportedExtensions()
                    .Contains(options.ExtensionHint, StringComparer.CurrentCultureIgnoreCase)
            );
            if (testedFactory is IReaderFactory readerFactory)
            {
                ((IStreamStack)bStream).StackSeek(pos);
                if (await testedFactory.IsArchiveAsync(bStream, cancellationToken: cancellationToken))
                {
                    ((IStreamStack)bStream).StackSeek(pos);
                    return readerFactory.OpenAsyncReader(bStream, options, cancellationToken);
                }
            }
            ((IStreamStack)bStream).StackSeek(pos);
        }

        foreach (var factory in factories)
        {
            if (testedFactory == factory)
            {
                continue; // Already tested above
            }
            ((IStreamStack)bStream).StackSeek(pos);
            if (factory is IReaderFactory readerFactory && await factory.IsArchiveAsync(bStream, cancellationToken: cancellationToken))
            {
                ((IStreamStack)bStream).StackSeek(pos);
                return readerFactory.OpenAsyncReader(bStream, options, cancellationToken);
            }
        }

        throw new InvalidFormatException(
            "Cannot determine compressed stream type.  Supported Reader Formats: Arc, Arj, Zip, GZip, BZip2, Tar, Rar, LZip, XZ, ZStandard"
        );
    }
}
