using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCompress.Readers;

namespace SharpCompress.Archives.Tar
{
    public class TarArchiveFactory : IArchiveFactory
    {
        /// <inheritdoc/>
        public string Name => "Tar";

        /// <inheritdoc/>
        public IEnumerable<string> GetSupportedExtensions()
        {
            yield return "tar";
        }

        /// <inheritdoc/>
        public bool IsArchive(Stream stream, string? password = null)
        {
            return TarArchive.IsTarFile(stream);
        }

        /// <inheritdoc/>
        public IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            return TarArchive.Open(stream, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
        {
            return TarArchive.Open(fileInfo, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
        {
            return TarArchive.Open(streams, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(IEnumerable<FileInfo> fileInfos, ReaderOptions? readerOptions = null)
        {
            return TarArchive.Open(fileInfos, readerOptions);
        }

    }
}
