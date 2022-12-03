using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace SharpCompress.Factories
{
    public class ZipFactory : Factory, IArchiveFactory, IMultiArchiveFactory, IReaderFactory, IWriterFactory
    {
        #region IFactory

        /// <inheritdoc/>
        public override string Name => "Zip";

        /// <inheritdoc/>
        public override ArchiveType? KnownArchiveType => ArchiveType.Zip;

        /// <inheritdoc/>
        public override IEnumerable<string> GetSupportedExtensions()
        {
            yield return "zip";
            yield return "zipx";
            yield return "cbz";
        }

        /// <inheritdoc/>
        public override bool IsArchive(Stream stream, string? password = null)
        {
            var startPosition = stream.CanSeek
                ? stream.Position
                : -1;

            // probe for single volume zip

            if (ZipArchive.IsZipFile(stream, password))
                return true;

            // probe for a multipart zip

            if (!stream.CanSeek)
                return false;

            stream.Position = startPosition;

            //test the zip (last) file of a multipart zip
            if (ZipArchive.IsZipMulti(stream))
                return true;

            return false;
        }

        /// <inheritdoc/>
        public override FileInfo? GetFilePart(int index, FileInfo part1)
        {
            return ZipArchiveVolumeFactory.GetFilePart(index, part1);
        }

        #endregion

        #region IArchiveFactory

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

        #endregion

        #region IMultiArchiveFactory        

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

        #endregion

        #region IReaderFactory        

        /// <inheritdoc/>
        public IReader OpenReader(Stream stream, ReaderOptions? options)
        {
            return ZipReader.Open(stream, options);
        }

        #endregion

        #region IWriterFactory

        /// <inheritdoc/>
        public IWriter Open(Stream stream, WriterOptions writerOptions)
        {
            return new ZipWriter(stream, new ZipWriterOptions(writerOptions));
        }

        #endregion
    }
}
