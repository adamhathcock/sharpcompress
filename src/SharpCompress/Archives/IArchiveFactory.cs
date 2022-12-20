using System;
using System.Collections.Generic;
using System.IO;

using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Archives
{
    /// <summary>
    /// Represents a factory used to identify and open archives.
    /// </summary>
    /// <remarks>
    /// Currently implemented by:<br/>
    /// <list type="table">
    /// <item><see cref="Factories.TarFactory"/></item>
    /// <item><see cref="Factories.RarFactory"/></item>
    /// <item><see cref="Factories.ZipFactory"/></item>
    /// <item><see cref="Factories.GZipFactory"/></item>
    /// <item><see cref="Factories.SevenZipFactory"/></item>
    /// </list>
    /// </remarks>
    public interface IArchiveFactory : IFactory
    {
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
    }
}
