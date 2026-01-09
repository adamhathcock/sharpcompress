using System;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;

namespace SharpCompress.Readers;

public static class ReaderFactory
{
    public static IReader Open(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), options);
    }

    public static IReader Open(FileInfo fileInfo, ReaderOptions? options = null)
    {
        options ??= new ReaderOptions { LeaveStreamOpen = false };
        return Open(fileInfo.OpenRead(), options);
    }

    /// <summary>
    /// Opens a Reader for Non-seeking usage
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IReader Open(Stream stream, ReaderOptions? options = null)
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
}
