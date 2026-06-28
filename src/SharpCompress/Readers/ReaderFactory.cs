using System;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;

namespace SharpCompress.Readers;

public static partial class ReaderFactory
{
    public static IReader OpenReader(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), options ?? ReaderOptions.ForFilePath);
    }

    public static IReader OpenReader(FileInfo fileInfo, ReaderOptions? options = null)
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
    public static IReader OpenReader(Stream stream, ReaderOptions? options = null)
    {
        stream.RequireReadable();
        options ??= ReaderOptions.ForExternalStream;

        var factories = Factories.Factory.Factories.OfType<Factories.Factory>().ToList();
        var minimumDetectionBufferSize = factories.Max(a =>
            a.MinimumReaderDetectionBufferSize ?? 0
        );
        var bufferSize = Math.Max(options.RewindableBufferSize ?? 0, minimumDetectionBufferSize);
        var sharpCompressStream = SharpCompressStream.Create(
            stream,
            bufferSize: bufferSize == 0 ? options.RewindableBufferSize : bufferSize
        );
        sharpCompressStream.StartRecording();

        var match = Factories.Factory.DetectFactory(
            sharpCompressStream,
            options,
            FactoryDetectionTarget.Reader
        );
        if (
            match.Result == FactoryDetectionResult.Match
            && match.Factory is IReaderFactory readerFactory
        )
        {
            sharpCompressStream.Rewind(true);
            return readerFactory.OpenReader(sharpCompressStream, options);
        }

        throw new InvalidFormatException(
            "Cannot determine compressed stream type.  Supported Reader Formats: Ace, Arc, Arj, Zip, GZip, BZip2, Tar, Rar, LZip, Lzw, XZ, ZStandard"
        );
    }
}
