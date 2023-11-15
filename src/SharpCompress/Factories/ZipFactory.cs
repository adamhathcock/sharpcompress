using System.Collections.Generic;
using System.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace SharpCompress.Factories;

/// <summary>
/// Represents the foundation factory of ZIP archive.
/// </summary>
public class ZipFactory
    : Factory,
        IArchiveFactory,
        IMultiArchiveFactory,
        IReaderFactory,
        IWriterFactory,
        IWriteableArchiveFactory
{
    #region IFactory

    /// <inheritdoc/>
    public override string Name => "Zip";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.Zip;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        yield return "zip";
        yield return "zipx";
        yield return "cbz";
    }

    /// <inheritdoc/>
    public override bool IsArchive(Stream stream, string? password = null)
    {
        var startPosition = stream.CanSeek ? stream.Position : -1;

        // probe for single volume zip

        if (ZipArchive.IsZipFile(stream, password))
        {
            return true;
        }

        // probe for a multipart zip

        if (!stream.CanSeek)
        {
            return false;
        }

        stream.Position = startPosition;

        //test the zip (last) file of a multipart zip
        if (ZipArchive.IsZipMulti(stream))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public override FileInfo? GetFilePart(int index, FileInfo part1) =>
        ZipArchiveVolumeFactory.GetFilePart(index, part1);

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(Stream stream, ReaderOptions? readerOptions = null) =>
        ZipArchive.Open(stream, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        ZipArchive.Open(fileInfo, readerOptions);

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<Stream> streams, ReaderOptions? readerOptions = null) =>
        ZipArchive.Open(streams, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? readerOptions = null) =>
        ZipArchive.Open(fileInfos, readerOptions);

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        ZipReader.Open(stream, options);

    #endregion

    #region IWriterFactory

    /// <inheritdoc/>
    public IWriter Open(Stream stream, WriterOptions writerOptions) =>
        new ZipWriter(stream, new ZipWriterOptions(writerOptions));

    #endregion

    #region IWriteableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive CreateWriteableArchive() => ZipArchive.Create();

    #endregion
}
