using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Factories;

/// <inheritdoc/>
public abstract class Factory : IFactory
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public virtual ArchiveType? KnownArchiveType => null;

    /// <inheritdoc/>
    public abstract IEnumerable<string> GetSupportedExtensions();

    /// <inheritdoc/>
    public abstract bool IsArchive(Stream stream, ReaderOptions readerOptions);
    public abstract ValueTask<bool> IsArchiveAsync(
        Stream stream,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken = default
    );

    /// <inheritdoc/>
    public virtual FileInfo? GetFilePart(int index, FileInfo part1) => null;

    /// <summary>
    /// Tries to open an <see cref="IReader"/> from a <see cref="SharpCompressStream"/>.
    /// </summary>
    /// <remarks>
    /// This method provides extra insight to support loading compressed TAR files.
    /// </remarks>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    internal virtual bool TryOpenReader(
        SharpCompressStream stream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        reader = null;

        if (this is IReaderFactory readerFactory)
        {
            stream.Rewind();
            if (IsArchive(stream, options))
            {
                stream.Rewind(true);
                reader = readerFactory.OpenReader(stream, options);
                return true;
            }
        }
        stream.Rewind();
        return false;
    }

    internal virtual async ValueTask<IAsyncReader?> TryOpenReaderAsync(
        SharpCompressStream stream,
        ReaderOptions options,
        CancellationToken cancellationToken = default
    )
    {
        if (this is IReaderFactory readerFactory)
        {
            stream.Rewind();
            if (await IsArchiveAsync(stream, options, cancellationToken).ConfigureAwait(false))
            {
                stream.Rewind(true);
                return await readerFactory
                    .OpenAsyncReader(stream, options, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        stream.Rewind();
        return null;
    }
}
