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
        /// returns the extensions typically used by this archive type.
        /// </summary>
        /// <returns></returns>
        IEnumerable<KeyValuePair<string, string>> GetSupportedExtensions();

        /// <summary>
        /// Returns true if the stream represents an archive of the format defined by this type.
        /// </summary>
        /// <param name="stream"></param>        
        bool IsArchive(Stream stream);

        /// <summary>
        /// Opens an Archive for random access.
        /// </summary>
        /// <param name="stream">An open, readable and seekable stream.</param>
        /// <param name="readerOptions">reading options.</param>        
        IArchive Open(Stream stream, ReaderOptions? readerOptions = null);
    }
}
