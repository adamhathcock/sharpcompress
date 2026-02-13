using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    public override ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        CancellationToken cancellationToken = default
    ) => RarArchive.IsRarFileAsync(stream, cancellationToken: cancellationToken);

    /// <inheritdoc/>
    public override FileInfo? GetFilePart(int index, FileInfo part1) =>
        RarArchiveVolumeFactory.GetFilePart(index, part1);

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null) =>
        RarArchive.OpenArchive(stream, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncArchive)OpenArchive(stream, readerOptions));
    }

    /// <inheritdoc/>
    public IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        RarArchive.OpenArchive(fileInfo, readerOptions);

    /// <inheritdoc/>
    public ValueTask<IAsyncArchive> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncArchive)OpenArchive(fileInfo, readerOptions));
    }

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive OpenArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => RarArchive.OpenArchive(streams, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => (IAsyncArchive)OpenArchive(streams, readerOptions);

    /// <inheritdoc/>
    public IArchive OpenArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    ) => RarArchive.OpenArchive(fileInfos, readerOptions);

    /// <inheritdoc/>
    public IAsyncArchive OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        return (IAsyncArchive)OpenArchive(fileInfos, readerOptions);
    }

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        RarReader.OpenReader(stream, options);

    /// <inheritdoc/>
    public ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncReader)RarReader.OpenReader(stream, options));
    }

    #endregion
}
