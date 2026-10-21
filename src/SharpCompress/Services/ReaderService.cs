using System;
using System.IO;
using System.Linq;
using Pure.DI;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Services;

[GenerateInterface(asInternal: true)]
internal sealed partial class ReaderService(IFactoryCatalog catalog) : IReaderService
{
    public IReader OpenReader(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), options ?? ReaderOptions.ForFilePath);
    }

    public IReader OpenReader(FileInfo fileInfo, ReaderOptions? options = null)
    {
        options ??= ReaderOptions.ForFilePath;
        return OpenReader(fileInfo.OpenRead(), options);
    }

    /// <summary>
    /// Opens a Reader for Non-seeking usage
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public IReader OpenReader(Stream stream, ReaderOptions? options = null)
    {
        stream.RequireReadable();
        options ??= ReaderOptions.ForExternalStream;

        if (options.LeaveStreamOpen)
        {
            // Protect the caller's stream, not the internal wrappers and their pooled buffers.
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
            if (
                testedFactory?.TryOpenReader(sharpCompressStream, options, out var reader) == true
                && reader != null
            )
            {
                sharpCompressStream.Rewind(true);
                return reader;
            }
        }

        foreach (var factory in factories)
        {
            if (testedFactory == factory)
            {
                continue; // Already tested above
            }
            sharpCompressStream.Rewind();
            if (
                factory.TryOpenReader(sharpCompressStream, options, out var reader)
                && reader != null
            )
            {
                return reader;
            }
        }

        throw new InvalidFormatException(
            "Cannot determine compressed stream type.  Supported Reader Formats: Ace, Arc, Arj, Zip, GZip, BZip2, Tar, Rar, LZip, Lzw, XZ, ZStandard"
        );
    }
}
