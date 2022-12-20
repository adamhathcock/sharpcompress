using System.Collections.Generic;
using System.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;

namespace SharpCompress.Factories;

/// <summary>
/// Represents the foundation factory of RAR archive.
/// </summary>
public class RarFactory : Factory, IArchiveFactory, IMultiArchiveFactory, IReaderFactory
{
    #region IArchive

    /// <inheritdoc/>
    public override string Name => "Rar";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.Rar;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        yield return "rar";
        yield return "cbr";
    }

    /// <inheritdoc/>
    public override bool IsArchive(Stream stream, string? password = null) =>
        RarArchive.IsRarFile(stream);

    /// <inheritdoc/>
    public override FileInfo? GetFilePart(int index, FileInfo part1) =>
        RarArchiveVolumeFactory.GetFilePart(index, part1);

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(Stream stream, ReaderOptions? readerOptions = null) =>
        RarArchive.Open(stream, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        RarArchive.Open(fileInfo, readerOptions);

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<Stream> streams, ReaderOptions? readerOptions = null) =>
        RarArchive.Open(streams, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? readerOptions = null) =>
        RarArchive.Open(fileInfos, readerOptions);

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        RarReader.Open(stream, options);

    #endregion
}
