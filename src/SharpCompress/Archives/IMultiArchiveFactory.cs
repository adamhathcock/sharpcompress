using System.Collections.Generic;
using System.IO;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

/// <summary>
/// Represents a factory used to identify and open archives.
/// </summary>
/// <remarks>
/// Implemented by:<br/>
/// <list type="table">
/// <item><see cref="TarFactory"/></item>
/// <item><see cref="RarFactory"/></item>
/// <item><see cref="ZipFactory"/></item>
/// <item><see cref="GZipFactory"/></item>
/// <item><see cref="SevenZipFactory"/></item>
/// </list>
/// </remarks>
public interface IMultiArchiveFactory : IFactory
{
    /// <summary>
    /// Constructor with IEnumerable FileInfo objects, multi and split support.
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="readerOptions">reading options.</param>
    IArchive Open(IReadOnlyList<Stream> streams, ReaderOptions? readerOptions = null);

    /// <summary>
    /// Constructor with IEnumerable Stream objects, multi and split support.
    /// </summary>
    /// <param name="fileInfos"></param>
    /// <param name="readerOptions">reading options.</param>
    IArchive Open(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? readerOptions = null);
}
