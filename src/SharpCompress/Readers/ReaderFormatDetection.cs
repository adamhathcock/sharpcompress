using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;

namespace SharpCompress.Readers;

internal static class ReaderFormatDetection
{
    internal static IReader OpenReader(Stream stream, ReaderOptions options)
    {
        var sharpCompressStream = CreateDetectionStream(stream, options);

        var match = FactoryDetection.Detect(
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

    internal static async ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions options,
        CancellationToken cancellationToken = default
    )
    {
        var sharpCompressStream = CreateDetectionStream(stream, options);

        var match = await FactoryDetection
            .DetectAsync(
                sharpCompressStream,
                options,
                FactoryDetectionTarget.Reader,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (
            match.Result == FactoryDetectionResult.Match
            && match.Factory is IReaderFactory readerFactory
        )
        {
            sharpCompressStream.Rewind(true);
            return await readerFactory
                .OpenAsyncReader(sharpCompressStream, options, cancellationToken)
                .ConfigureAwait(false);
        }

        throw new InvalidFormatException(
            "Cannot determine compressed stream type.  Supported Reader Formats: Arc, Arj, Zip, GZip, BZip2, Tar, Rar, LZip, XZ, ZStandard"
        );
    }

    private static SharpCompressStream CreateDetectionStream(Stream stream, ReaderOptions options)
    {
        var factories = Factory.Factories.OfType<Factory>().ToList();
        var minimumDetectionBufferSize = factories.Max(a =>
            a.MinimumReaderDetectionBufferSize ?? 0
        );
        var bufferSize = Math.Max(options.RewindableBufferSize ?? 0, minimumDetectionBufferSize);
        var sharpCompressStream = SharpCompressStream.Create(
            stream,
            bufferSize: bufferSize == 0 ? options.RewindableBufferSize : bufferSize
        );
        sharpCompressStream.StartRecording();
        return sharpCompressStream;
    }
}
