using System;
using System.Collections.Generic;
using System.IO;

using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives
{
    /// <summary>
    /// Represents a factory used to identify and open archives.
    /// </summary>
    /// <remarks>
    /// Currently implemented by:<br/>
    /// <list type="table">
    /// <item><see cref="Tar.TarArchiveFactory"/></item>
    /// <item><see cref="Rar.RarArchiveFactory"/></item>
    /// <item><see cref="Zip.ZipArchiveFactory"/></item>
    /// <item><see cref="GZip.GZipArchiveFactory"/></item>
    /// <item><see cref="SevenZip.SevenZipArchiveFactory"/></item>
    /// </list>
    /// </remarks>
    public interface IArchiveFactory
    {
        /// <summary>
        /// Gets the archive Type name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// returns the extensions typically used by this archive type.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetSupportedExtensions();

        /// <summary>
        /// Returns true if the stream represents an archive of the format defined by this type.
        /// </summary>
        /// <param name="stream"></param>
        /// /// <param name="password">optional password</param>
        bool IsArchive(Stream stream, string? password = null);

        /// <summary>
        /// Opens an Archive for random access.
        /// </summary>
        /// <param name="stream">An open, readable and seekable stream.</param>
        /// <param name="readerOptions">reading options.</param>
        IArchive Open(Stream stream, ReaderOptions? readerOptions = null);

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo">the file to open.</param>
        /// <param name="readerOptions">reading options.</param>
        IArchive Open(System.IO.FileInfo fileInfo, ReaderOptions? readerOptions = null);

        /// <summary>
        /// Constructor with IEnumerable FileInfo objects, multi and split support.
        /// </summary>
        /// <param name="streams"></param>
        /// <param name="readerOptions">reading options.</param>
        IArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null);

        /// <summary>
        /// Constructor with IEnumerable Stream objects, multi and split support.
        /// </summary>
        /// <param name="fileInfos"></param>
        /// <param name="readerOptions">reading options.</param>
        IArchive Open(IEnumerable<System.IO.FileInfo> fileInfos, ReaderOptions? readerOptions = null);
    }
}
