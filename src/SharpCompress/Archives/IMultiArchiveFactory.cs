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
    public interface IMultiArchiveFactory : IFactory
    {
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
