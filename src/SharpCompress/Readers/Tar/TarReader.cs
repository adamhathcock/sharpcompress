using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Xz;

namespace SharpCompress.Readers.Tar
{
    public class TarReader : AbstractReader<TarEntry, TarVolume>
    {
        private readonly CompressionType compressionType;

        internal TarReader(Stream stream, ReaderOptions options, CompressionType compressionType)
            : base(options, ArchiveType.Tar)
        {
            this.compressionType = compressionType;
            Volume = new TarVolume(stream, options);
        }

        public override TarVolume Volume { get; }

        protected override async ValueTask<Stream> RequestInitialStream(CancellationToken cancellationToken)
        {
            var stream = await base.RequestInitialStream(cancellationToken);
            switch (compressionType)
            {
               /* case CompressionType.BZip2:
                    {
                        return await BZip2Stream.CreateAsync(stream, CompressionMode.Decompress, false, cancellationToken);
                    }   */
                case CompressionType.GZip:
                    {
                        return new GZipStream(stream, CompressionMode.Decompress);
                    }
                case CompressionType.LZip:
                    {
                        return await LZipStream.CreateAsync(stream, CompressionMode.Decompress);
                    }
                case CompressionType.Xz:
                    {
                        return new XZStream(stream);
                    }
                case CompressionType.None:
                    {
                        return stream;
                    }
                default:
                    {
                        throw new NotSupportedException("Invalid compression type: " + compressionType);
                    }
            }
        }

        #region Open

        /// <summary>
        /// Opens a TarReader for Non-seeking usage with a single volume
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static async ValueTask<TarReader> OpenAsync(Stream stream, ReaderOptions? options = null, CancellationToken cancellationToken = default)
        {
            stream.CheckNotNull(nameof(stream));
            options ??= new ReaderOptions();
            RewindableStream rewindableStream = new(stream);
            rewindableStream.StartRecording();
            if (await GZipArchive.IsGZipFileAsync(rewindableStream, cancellationToken))
            {
                rewindableStream.Rewind(false);
                GZipStream testStream = new(rewindableStream, CompressionMode.Decompress);
                if (await TarArchive.IsTarFileAsync(testStream, cancellationToken))
                {
                    rewindableStream.Rewind(true);
                    return new TarReader(rewindableStream, options, CompressionType.GZip);
                }
                throw new InvalidFormatException("Not a tar file.");
            }

            /*rewindableStream.Rewind(false);
            if (await BZip2Stream.IsBZip2Async(rewindableStream, cancellationToken))
            {
                rewindableStream.Rewind(false);
                var testStream = await BZip2Stream.CreateAsync(rewindableStream, CompressionMode.Decompress, false, cancellationToken);
                if (await TarArchive.IsTarFileAsync(testStream, cancellationToken))
                {
                    rewindableStream.Rewind(true);
                    return new TarReader(rewindableStream, options, CompressionType.BZip2);
                }
                throw new InvalidFormatException("Not a tar file.");
            }   */

            rewindableStream.Rewind(false);
            if (await LZipStream.IsLZipFileAsync(rewindableStream))
            {
                rewindableStream.Rewind(false);
                var testStream = await LZipStream.CreateAsync(rewindableStream, CompressionMode.Decompress);
                if (await TarArchive.IsTarFileAsync(testStream, cancellationToken))
                {
                    rewindableStream.Rewind(true);
                    return new TarReader(rewindableStream, options, CompressionType.LZip);
                }
                throw new InvalidFormatException("Not a tar file.");
            }
            rewindableStream.Rewind(true);
            return new TarReader(rewindableStream, options, CompressionType.None);
        }

        #endregion Open

        protected override IAsyncEnumerable<TarEntry> GetEntries(Stream stream, CancellationToken cancellationToken)
        {
            return TarEntry.GetEntries(StreamingMode.Streaming, stream, compressionType, Options.ArchiveEncoding, cancellationToken);
        }
    }
}
