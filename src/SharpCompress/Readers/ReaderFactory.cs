using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.GZip;
//using SharpCompress.Archives.Rar;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using SharpCompress.Readers.GZip;
//using SharpCompress.Readers.Rar;
using SharpCompress.Readers.Tar;
using SharpCompress.Readers.Zip;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Xz;

namespace SharpCompress.Readers
{
    public static class ReaderFactory
    {
        /// <summary>
        /// Opens a Reader for Non-seeking usage
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static async ValueTask<IReader> OpenAsync(Stream stream, ReaderOptions? options = null, CancellationToken cancellationToken = default)
        {
            stream.CheckNotNull(nameof(stream));
            options ??= new ReaderOptions()
                        {
                            LeaveStreamOpen = false
                        };
            RewindableStream rewindableStream = new(stream);
            rewindableStream.StartRecording();
            if (await ZipArchive.IsZipFileAsync(rewindableStream, options.Password))
            {
                rewindableStream.Rewind(true);
                return ZipReader.Open(rewindableStream, options);
            }
            rewindableStream.Rewind(false);
            if (await GZipArchive.IsGZipFileAsync(rewindableStream, cancellationToken))
            {
                rewindableStream.Rewind(false);
                GZipStream testStream = new(rewindableStream, CompressionMode.Decompress);
                if (await TarArchive.IsTarFileAsync(testStream, cancellationToken))
                {
                    rewindableStream.Rewind(true);
                    return new TarReader(rewindableStream, options, CompressionType.GZip);
                }          
                rewindableStream.Rewind(true);
                return GZipReader.Open(rewindableStream, options);
            }

            rewindableStream.Rewind(false);
            if (await BZip2Stream.IsBZip2Async(rewindableStream, cancellationToken))
            {
                rewindableStream.Rewind(false);
                BZip2Stream testStream = new(new NonDisposingStream(rewindableStream), CompressionMode.Decompress, false);
                if (await TarArchive.IsTarFileAsync(testStream, cancellationToken))
                {
                    rewindableStream.Rewind(true);
                    return new TarReader(rewindableStream, options, CompressionType.BZip2);
                }    
            }

            rewindableStream.Rewind(false);
            if (LZipStream.IsLZipFile(rewindableStream))
            {
                rewindableStream.Rewind(false);
                 LZipStream testStream = new(new NonDisposingStream(rewindableStream), CompressionMode.Decompress);
                 if (await TarArchive.IsTarFileAsync(testStream, cancellationToken))
                 {
                     rewindableStream.Rewind(true);
                     return new TarReader(rewindableStream, options, CompressionType.LZip);
                 }  
            }
            /*  rewindableStream.Rewind(false);
            if (RarArchive.IsRarFile(rewindableStream, options))
             {
                 rewindableStream.Rewind(true);
                 return RarReader.Open(rewindableStream, options);
             }   */

            rewindableStream.Rewind(false);
            if (await TarArchive.IsTarFileAsync(rewindableStream, cancellationToken))
            {
                rewindableStream.Rewind(true);
                return await TarReader.OpenAsync(rewindableStream, options, cancellationToken);
            }  
            rewindableStream.Rewind(false);
            if (await XZStream.IsXZStreamAsync(rewindableStream, cancellationToken))
            {
                rewindableStream.Rewind(true);
                XZStream testStream = new(rewindableStream);
                if (await TarArchive.IsTarFileAsync(testStream, cancellationToken))
                {
                    rewindableStream.Rewind(true);
                    return new TarReader(rewindableStream, options, CompressionType.Xz);
                }   
            }
            throw new InvalidOperationException("Cannot determine compressed stream type.  Supported Reader Formats: Zip, GZip, BZip2, Tar, Rar, LZip, XZ");
        }
    }
}
