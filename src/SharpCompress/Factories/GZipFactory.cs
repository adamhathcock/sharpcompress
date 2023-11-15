using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(Stream stream, ReaderOptions? readerOptions = null) =>
        GZipArchive.Open(stream, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        GZipArchive.Open(fileInfo, readerOptions);

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<Stream> streams, ReaderOptions? readerOptions = null) =>
        GZipArchive.Open(streams, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? readerOptions = null) =>
        GZipArchive.Open(fileInfos, readerOptions);

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    internal override bool TryOpenReader(
        RewindableStream rewindableStream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        reader = null;

        rewindableStream.Rewind(false);
        if (GZipArchive.IsGZipFile(rewindableStream))
        {
            rewindableStream.Rewind(false);
            var testStream = new GZipStream(rewindableStream, CompressionMode.Decompress);
            if (TarArchive.IsTarFile(testStream))
            {
                rewindableStream.Rewind(true);
                reader = new TarReader(rewindableStream, options, CompressionType.GZip);
                return true;
            }

            rewindableStream.Rewind(true);
            reader = OpenReader(rewindableStream, options);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        GZipReader.Open(stream, options);

    #endregion

    #region IWriterFactory

    /// <inheritdoc/>
    public IWriter Open(Stream stream, WriterOptions writerOptions)
    {
        if (writerOptions.CompressionType != CompressionType.GZip)
        {
            throw new InvalidFormatException("GZip archives only support GZip compression type.");
        }
        return new GZipWriter(stream, new GZipWriterOptions(writerOptions));
    }

    #endregion

    #region IWriteableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive CreateWriteableArchive() => GZipArchive.Create();

    #endregion
}
