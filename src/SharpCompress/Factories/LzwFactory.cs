using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Compressors.Lzw;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Lzw;
using SharpCompress.Readers.Tar;

namespace SharpCompress.Factories;

/// <summary>
/// Represents the foundation factory of LZW archive.
/// </summary>
public class LzwFactory : Factory, IReaderFactory
{
    #region IFactory

    /// <inheritdoc/>
    public override string Name => "Lzw";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.Lzw;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        yield return "z";
    }

    /// <inheritdoc/>
    public override bool IsArchive(Stream stream, ReaderOptions readerOptions) =>
        LzwStream.IsLzwStream(stream);

    /// <inheritdoc/>
    public override ValueTask<bool> IsArchiveAsync(
        Stream stream,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken = default
    ) => LzwStream.IsLzwStreamAsync(stream, cancellationToken);

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    internal override bool TryOpenReader(
        SharpCompressStream sharpCompressStream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        reader = null;

        if (LzwStream.IsLzwStream(sharpCompressStream))
        {
            sharpCompressStream.Rewind();
            using (
                var testStream = options.Providers.CreateDecompressStream(
                    CompressionType.Lzw,
                    SharpCompressStream.CreateNonDisposing(sharpCompressStream)
                )
            )
            {
                var isTarArchive = TarArchive.IsTarFile(testStream);

                // The TAR probe can consume arbitrary compressed input before it rejects a stream.
                sharpCompressStream.Rewind();
                sharpCompressStream.StopRecording();
                if (isTarArchive)
                {
                    reader = new TarReader(sharpCompressStream, options, CompressionType.Lzw);
                    return true;
                }
            }
            reader = OpenReader(sharpCompressStream, options);
            return true;
        }
        sharpCompressStream.Rewind();
        return false;
    }

    internal override async ValueTask<IAsyncReader?> TryOpenReaderAsync(
        SharpCompressStream sharpCompressStream,
        ReaderOptions options,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !await LzwStream
                .IsLzwStreamAsync(sharpCompressStream, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            sharpCompressStream.Rewind();
            return null;
        }

        sharpCompressStream.Rewind();
        using var testStream = SharpCompressStream.CreateNonDisposing(
            await options
                .Providers.CreateDecompressStreamAsync(
                    CompressionType.Lzw,
                    SharpCompressStream.CreateNonDisposing(sharpCompressStream),
                    cancellationToken
                )
                .ConfigureAwait(false)
        );
        var isTarArchive = await TarArchive
            .IsTarFileAsync(testStream, cancellationToken)
            .ConfigureAwait(false);

        sharpCompressStream.Rewind();
        sharpCompressStream.StopRecording();
        if (isTarArchive)
        {
            return new TarReader(sharpCompressStream, options, CompressionType.Lzw);
        }

        return await OpenAsyncReader(sharpCompressStream, options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        LzwReader.OpenReader(stream, options);

    /// <inheritdoc/>
    public ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return LzwReader.OpenAsyncReader(stream, options, cancellationToken);
    }

    #endregion
}
