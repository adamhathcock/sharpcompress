using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCompress.Readers;

namespace SharpCompress.Archives.GZip
{
    public class GZipArchiveFactory : IArchiveFactory
    {
        /// <inheritdoc/>
        public string Name => "GZip";

        /// <inheritdoc/>
        public IEnumerable<string> GetSupportedExtensions()
        {
            yield return "gz";
        }

        /// <inheritdoc/>
        public bool IsArchive(Stream stream, string? password = null)
        {
            return GZipArchive.IsGZipFile(stream);
        }

        /// <inheritdoc/>
        public IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            return GZipArchive.Open(stream, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
        {
            return GZipArchive.Open(fileInfo, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
        {
            return GZipArchive.Open(streams, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(IEnumerable<FileInfo> fileInfos, ReaderOptions? readerOptions = null)
        {
            return GZipArchive.Open(fileInfos, readerOptions);
        }
    }
}
