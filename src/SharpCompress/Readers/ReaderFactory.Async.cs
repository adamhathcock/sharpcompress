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
    public static async ValueTask<IAsyncReader> OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= ReaderOptions.ForFilePath;
        var stream = fileInfo.OpenAsyncReadStream(cancellationToken);
        return await OpenAsyncReader(stream, options, cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        options ??= ReaderOptions.ForExternalStream;

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

        var match = await Factory
            .DetectFactoryAsync(
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
}
