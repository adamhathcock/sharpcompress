using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;

namespace SharpCompress.Factories
{
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
        public override bool IsArchive(Stream stream, string? password = null)
        {
            return RarArchive.IsRarFile(stream);
        }

        /// <inheritdoc/>
        public override FileInfo? GetFilePart(int index, FileInfo part1)
        {
            return RarArchiveVolumeFactory.GetFilePart(index, part1);
        }

        #endregion

        #region IArchiveFactory        

        /// <inheritdoc/>
        public IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            return RarArchive.Open(stream, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
        {
            return RarArchive.Open(fileInfo, readerOptions);
        }

        #endregion

        #region IMultiArchiveFactory

        /// <inheritdoc/>
        public IArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
        {
            return RarArchive.Open(streams, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(IEnumerable<FileInfo> fileInfos, ReaderOptions? readerOptions = null)
        {
            return RarArchive.Open(fileInfos, readerOptions);
        }

        #endregion

        #region IReaderFactory        

        /// <inheritdoc/>
        public IReader OpenReader(Stream stream, ReaderOptions? options)
        {
            return RarReader.Open(stream, options);
        }

        #endregion
    }
}
