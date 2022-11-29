using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Factories
{
    public class SevenZipFactory : Factory, IArchiveFactory
    {
        #region IFactory

        /// <inheritdoc/>
        public override string Name => "7Zip";

        /// <inheritdoc/>
        public override ArchiveType? KnownArchiveType => ArchiveType.SevenZip;

        /// <inheritdoc/>
        public override IEnumerable<string> GetSupportedExtensions()
        {
            yield return "7z";
        }

        /// <inheritdoc/>
        public override bool IsArchive(Stream stream, string? password = null)
        {
            return SevenZipArchive.IsSevenZipFile(stream);
        }

        #endregion

        #region IArchiveFactory

        /// <inheritdoc/>
        public IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            return SevenZipArchive.Open(stream, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
        {
            return SevenZipArchive.Open(fileInfo, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
        {
            return SevenZipArchive.Open(streams, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(IEnumerable<FileInfo> fileInfos, ReaderOptions? readerOptions = null)
        {
            return SevenZipArchive.Open(fileInfos, readerOptions);
        }        

        #endregion

        #region reader

        internal override bool TryOpenReader(RewindableStream rewindableStream, ReaderOptions options, out IReader? reader)
        {
            reader = null;
            return false;
        }
        
        #endregion
    }
}
