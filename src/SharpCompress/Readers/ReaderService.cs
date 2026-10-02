using System;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;

namespace SharpCompress.Readers;

internal sealed partial class ReaderService(SharpCompressConfiguration configuration)
{
    public IReader OpenReader(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), options ?? ReaderOptions.ForFilePath);
    }

    public IReader OpenReader(FileInfo fileInfo, ReaderOptions? options = null)
    {
        options ??= ReaderOptions.ForFilePath;
        var stream = fileInfo.OpenRead();
        try
        {
            return OpenReader(stream, options);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
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
            if (
                testedFactory is not null
                && TryOpenReader(testedFactory, sharpCompressStream, options, out var reader)
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
                TryOpenReader(factory, sharpCompressStream, options, out var reader)
                && reader != null
            )
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

    private static bool TryOpenReader(
        IFactory factory,
        SharpCompressStream stream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        if (factory is Factory builtInFactory)
        {
            return builtInFactory.TryOpenReader(stream, options, out reader);
        }
        reader = null;
        stream.Rewind();
        if (factory is IReaderFactory readerFactory && factory.IsArchive(stream, options))
        {
            stream.Rewind(true);
            reader = readerFactory.OpenReader(stream, options);
            return true;
        }
        stream.Rewind();
        return false;
    }
}
