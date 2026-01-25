using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;

namespace SharpCompress.Factories;

/// <summary>
/// Represents the foundation factory of GZip archive.
/// </summary>
public class GZipFactory
    : Factory,
        IArchiveFactory,
        IMultiArchiveFactory,
        IReaderFactory,
        IWriterFactory,
        IWriteableArchiveFactory
{
    #region IFactory

    /// <inheritdoc/>
    public override string Name => "GZip";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.GZip;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        yield return "gz";
    }

    /// <inheritdoc/>
    public override bool IsArchive(Stream stream, string? password = null) =>
        GZipArchive.IsGZipFile(stream);

    /// <inheritdoc/>
    public override ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        CancellationToken cancellationToken = default
    ) => GZipArchive.IsGZipFileAsync(stream, cancellationToken);

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null) =>
        GZipArchive.OpenArchive(stream, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(Stream stream, ReaderOptions? readerOptions = null) =>
        (IAsyncArchive)OpenArchive(stream, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        (IAsyncArchive)OpenArchive(fileInfo, readerOptions);

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive OpenArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => GZipArchive.OpenArchive(streams, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => (IAsyncArchive)OpenArchive(streams, readerOptions);

    /// <inheritdoc/>
    public IArchive OpenArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    ) => GZipArchive.OpenArchive(fileInfos, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (IAsyncArchive)OpenArchive(fileInfos, readerOptions);
    }

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    internal override bool TryOpenReader(
        SharpCompressStream rewindableStream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        reader = null;

        long pos = ((IStreamStack)rewindableStream).GetPosition();

        if (GZipArchive.IsGZipFile(rewindableStream))
        {
            ((IStreamStack)rewindableStream).StackSeek(pos);
            var testStream = new GZipStream(rewindableStream, CompressionMode.Decompress);
            if (TarArchive.IsTarFile(testStream))
            {
                ((IStreamStack)rewindableStream).StackSeek(pos);
                reader = new TarReader(rewindableStream, options, CompressionType.GZip);
                return true;
            }

            ((IStreamStack)rewindableStream).StackSeek(pos);
            reader = OpenReader(rewindableStream, options);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        GZipReader.OpenReader(stream, options);

    /// <inheritdoc/>
    public ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncReader)GZipReader.OpenReader(stream, options));
    }

    /// <inheritdoc/>
    public IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        GZipArchive.OpenArchive(fileInfo, readerOptions);

    #endregion

    #region IWriterFactory

    /// <inheritdoc/>
    public IWriter OpenWriter(Stream stream, WriterOptions writerOptions)
    {
        if (writerOptions.CompressionType != CompressionType.GZip)
        {
            throw new InvalidFormatException("GZip archives only support GZip compression type.");
        }
        return new GZipWriter(stream, new GZipWriterOptions(writerOptions));
    }

    /// <inheritdoc/>
    public IAsyncWriter OpenAsyncWriter(
        Stream stream,
        WriterOptions writerOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (writerOptions.CompressionType != CompressionType.GZip)
        {
            throw new InvalidFormatException("GZip archives only support GZip compression type.");
        }
        return (IAsyncWriter)OpenWriter(stream, writerOptions);
    }

    #endregion

    #region IWriteableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive CreateArchive() => GZipArchive.CreateArchive();

    #endregion
}
