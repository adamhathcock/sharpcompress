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
    public override bool IsArchive(Stream stream, string? password = null) =>
        LzwStream.IsLzwStream(stream);

    /// <inheritdoc/>
    public override ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
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
                if (TarArchive.IsTarFile(testStream))
                {
                    sharpCompressStream.StopRecording();
                    reader = new TarReader(sharpCompressStream, options, CompressionType.Lzw);
                    return true;
                }
            }
            sharpCompressStream.StopRecording();
            reader = OpenReader(sharpCompressStream, options);
            return true;
        }
        sharpCompressStream.Rewind();
        return false;
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
