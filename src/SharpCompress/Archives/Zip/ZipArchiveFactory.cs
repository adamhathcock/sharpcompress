using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCompress.Readers;

namespace SharpCompress.Archives.Zip
{
    public class ZipArchiveFactory : IArchiveFactory
    {
        /// <inheritdoc/>
        public string Name => "Zip";

        /// <inheritdoc/>
        public IEnumerable<string> GetSupportedExtensions()
        {
            yield return "zip";
            yield return "zipx";
            yield return "cbz";
        }

        /// <inheritdoc/>
        public bool IsArchive(Stream stream, string? password = null)
        {
            long startPosition = stream.Position;

            if (ZipArchive.IsZipFile(stream, password)) return true;

            stream.Seek(startPosition, SeekOrigin.Begin);

            //test the zip (last) file of a multipart zip
            if (ZipArchive.IsZipMulti(stream)) return true;

            return false;
        }

        /// <inheritdoc/>
        public IArchive Open(Stream stream, ReaderOptions? readerOptions = null)
        {
            return ZipArchive.Open(stream, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
        {
            return ZipArchive.Open(fileInfo, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
        {
            return ZipArchive.Open(streams, readerOptions);
        }

        /// <inheritdoc/>
        public IArchive Open(IEnumerable<FileInfo> fileInfos, ReaderOptions? readerOptions = null)
        {
            return ZipArchive.Open(fileInfos, readerOptions);
        }
    }
}
