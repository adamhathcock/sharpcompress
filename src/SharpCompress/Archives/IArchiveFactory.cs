using System.IO;
using System.Threading;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

/// <summary>
/// Represents a factory used to identify and open archives.
/// </summary>
/// <remarks>
/// Currently implemented by:<br/>
/// <list type="table">
/// <item><see cref="TarFactory"/></item>
/// <item><see cref="RarFactory"/></item>
/// <item><see cref="ZipFactory"/></item>
/// <item><see cref="GZipFactory"/></item>
/// <item><see cref="SevenZipFactory"/></item>
/// </list>
/// </remarks>
public interface IArchiveFactory : IFactory
{
    /// <summary>
    /// Opens an Archive for random access.
    /// </summary>
    /// <param name="stream">An open, readable and seekable stream.</param>
    /// <param name="readerOptions">reading options.</param>
    IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null);

    /// <summary>
    /// Opens an Archive for random access asynchronously.
    /// </summary>
    /// <param name="stream">An open, readable and seekable stream.</param>
    /// <param name="readerOptions">reading options.</param>
    IAsyncArchive OpenAsyncArchive(Stream stream, ReaderOptions? readerOptions = null);

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo">the file to open.</param>
    /// <param name="readerOptions">reading options.</param>
    IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null);

    /// <summary>
    /// Opens an Archive from a FileInfo object asynchronously.
    /// </summary>
    /// <param name="fileInfo">the file to open.</param>
    /// <param name="readerOptions">reading options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncArchive OpenAsyncArchive(FileInfo fileInfo, ReaderOptions? readerOptions = null);
}
