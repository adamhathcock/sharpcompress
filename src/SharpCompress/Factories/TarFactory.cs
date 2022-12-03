using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Xz;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Factories
{
    public class TarFactory : Factory, IArchiveFactory, IMultiArchiveFactory, IReaderFactory, IWriterFactory
    {
        #region IFactory

        /// <inheritdoc/>
        public override string Name => "Tar";

        /// <inheritdoc/>
        public override ArchiveType? KnownArchiveType => ArchiveType.Tar;

        /// <inheritdoc/>
        public override IEnumerable<string> GetSupportedExtensions()
        {
            yield return "tar";
            yield return "tgz";
        }

        /// <inheritdoc/>
        public override bool IsArchive(Stream stream, string? password = null)
        {
            return TarArchive.IsTarFile(stream);
        }

        #endregion

        #region IArchiveFactory

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

        #endregion

        #region IMultiArchiveFactory

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

        #endregion

        #region IReaderFactory

        /// <inheritdoc/>
        internal override bool TryOpenReader(RewindableStream rewindableStream, ReaderOptions options, out IReader? reader)
        {
            reader = null;

            rewindableStream.Rewind(false);
            if (TarArchive.IsTarFile(rewindableStream))
            {
                rewindableStream.Rewind(true);
                reader = OpenReader(rewindableStream, options);
                return true;
            }

            rewindableStream.Rewind(false);
            if (BZip2Stream.IsBZip2(rewindableStream))
            {
                rewindableStream.Rewind(false);
                var testStream = new BZip2Stream(NonDisposingStream.Create(rewindableStream), CompressionMode.Decompress, false);
                if (TarArchive.IsTarFile(testStream))
                {
                    rewindableStream.Rewind(true);
                    reader = new TarReader(rewindableStream, options, CompressionType.BZip2);
                    return true;
                }
            }

            rewindableStream.Rewind(false);
            if (LZipStream.IsLZipFile(rewindableStream))
            {
                rewindableStream.Rewind(false);
                var testStream = new LZipStream(NonDisposingStream.Create(rewindableStream), CompressionMode.Decompress);
                if (TarArchive.IsTarFile(testStream))
                {
                    rewindableStream.Rewind(true);
                    reader = new TarReader(rewindableStream, options, CompressionType.LZip);
                    return true;
                }
            }

            rewindableStream.Rewind(false);
            if (XZStream.IsXZStream(rewindableStream))
            {
                rewindableStream.Rewind(true);
                var testStream = new XZStream(rewindableStream);
                if (TarArchive.IsTarFile(testStream))
                {
                    rewindableStream.Rewind(true);
                    reader = new TarReader(rewindableStream, options, CompressionType.Xz);
                    return true;
                }
            }
            
            return false;
        }

        /// <inheritdoc/>
        public IReader OpenReader(Stream stream, ReaderOptions? options)
        {
            return TarReader.Open(stream, options);
        }

        #endregion

        #region IWriterFactory

        /// <inheritdoc/>
        public IWriter Open(Stream stream, WriterOptions writerOptions)
        {
            return new TarWriter(stream, new TarWriterOptions(writerOptions));
        }

        #endregion

    }
}
