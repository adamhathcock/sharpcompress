using System;
using System.IO;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using SharpCompress.Readers.GZip;
using SharpCompress.Readers.Rar;
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
        public static IReader Open(Stream stream, ReaderOptions options = null)
        {
            stream.CheckNotNull(nameof(stream));
            options = options ?? new ReaderOptions()
            {
                LeaveStreamOpen = false
            };
            RewindableStream rewindableStream = new RewindableStream(stream);
            rewindableStream.StartRecording();
            if (ZipArchive.IsZipFile(rewindableStream, options.Password))
            {
                rewindableStream.Rewind(true);
                return ZipReader.Open(rewindableStream, options);
            }
            rewindableStream.Rewind(false);
            if (GZipArchive.IsGZipFile(rewindableStream))
            {
                rewindableStream.Rewind(false);
                GZipStream testStream = new GZipStream(rewindableStream, CompressionMode.Decompress);
                if (TarArchive.IsTarFile(testStream))
                {
                    rewindableStream.Rewind(true);
                    return new TarReader(rewindableStream, options, CompressionType.GZip);
                }
                rewindableStream.Rewind(true);
                return GZipReader.Open(rewindableStream, options);
            }

            rewindableStream.Rewind(false);
            if (BZip2Stream.IsBZip2(rewindableStream))
            {
                rewindableStream.Rewind(false);
                BZip2Stream testStream = new BZip2Stream(new NonDisposingStream(rewindableStream), CompressionMode.Decompress, false);
                if (TarArchive.IsTarFile(testStream))
                {
                    rewindableStream.Rewind(true);
                    return new TarReader(rewindableStream, options, CompressionType.BZip2);
                }
            }

            rewindableStream.Rewind(false);
            if (LZipStream.IsLZipFile(rewindableStream))
            {
                rewindableStream.Rewind(false);
                LZipStream testStream = new LZipStream(new NonDisposingStream(rewindableStream), CompressionMode.Decompress);
                if (TarArchive.IsTarFile(testStream))
                {
                    rewindableStream.Rewind(true);
                    return new TarReader(rewindableStream, options, CompressionType.LZip);
                }
            }
            rewindableStream.Rewind(false);
            if (RarArchive.IsRarFile(rewindableStream, options))
            {
                rewindableStream.Rewind(true);
                return RarReader.Open(rewindableStream, options);
            }

            rewindableStream.Rewind(false);
            if (TarArchive.IsTarFile(rewindableStream))
            {
                rewindableStream.Rewind(true);
                return TarReader.Open(rewindableStream, options);
            }
            rewindableStream.Rewind(false);
            if (XZStream.IsXZStream(rewindableStream))
            {
                rewindableStream.Rewind(true);
                XZStream testStream = new XZStream(rewindableStream);
                if (TarArchive.IsTarFile(testStream))
                {
                    rewindableStream.Rewind(true);
                    return new TarReader(rewindableStream, options, CompressionType.Xz);
                }
            }
            throw new InvalidOperationException("Cannot determine compressed stream type.  Supported Reader Formats: Zip, GZip, BZip2, Tar, Rar, LZip, XZ");
        }


        public static bool TryOpen(Stream stream, ReaderOptions options,ArchiveTypeMask archiveTypes, out IReader reader)
        {
            stream.CheckNotNull("stream");
            options = options ?? new ReaderOptions()
            {
                LeaveStreamOpen = false
            };
            RewindableStream rewindableStream = new RewindableStream(stream);
            rewindableStream.StartRecording();
            if (archiveTypes.HasFlag(ArchiveTypeMask.Zip)&& ZipArchive.IsZipFile(rewindableStream, options.Password))
            {
                rewindableStream.Rewind(true);
                reader = ZipReader.Open(rewindableStream, options);
                return true;
            }
            rewindableStream.Rewind(false);
            if (archiveTypes.HasFlag(ArchiveTypeMask.GZip) && GZipArchive.IsGZipFile(rewindableStream))
            {
                rewindableStream.Rewind(false);
                GZipStream testStream = new GZipStream(rewindableStream, CompressionMode.Decompress);
                if (archiveTypes.HasFlag(ArchiveTypeMask.Tar) && TarArchive.IsTarFile(testStream))
                {
                    rewindableStream.Rewind(true);
                    reader = new TarReader(rewindableStream, options, CompressionType.GZip);
                    return true;
                }
                rewindableStream.Rewind(true);
                reader = GZipReader.Open(rewindableStream, options);
                return true;
            }

            rewindableStream.Rewind(false);
            if (archiveTypes.HasFlag(ArchiveTypeMask.BZip2) && BZip2Stream.IsBZip2(rewindableStream))
            {
                rewindableStream.Rewind(false);
                BZip2Stream testStream = new BZip2Stream(new NonDisposingStream(rewindableStream), CompressionMode.Decompress, false);
                if (archiveTypes.HasFlag(ArchiveTypeMask.Tar) && TarArchive.IsTarFile(testStream))
                {
                    rewindableStream.Rewind(true);
                    reader = new TarReader(rewindableStream, options, CompressionType.BZip2);
                    return true;
                }
            }

            rewindableStream.Rewind(false);
            if (archiveTypes.HasFlag(ArchiveTypeMask.LZip) && LZipStream.IsLZipFile(rewindableStream))
            {
                rewindableStream.Rewind(false);
                LZipStream testStream = new LZipStream(new NonDisposingStream(rewindableStream), CompressionMode.Decompress);
                if (archiveTypes.HasFlag(ArchiveTypeMask.Tar) && TarArchive.IsTarFile(testStream))
                {
                    rewindableStream.Rewind(true);
                    reader = new TarReader(rewindableStream, options, CompressionType.LZip);
                    return true;
                }
            }
            rewindableStream.Rewind(false);
            if (archiveTypes.HasFlag(ArchiveTypeMask.Rar) && RarArchive.IsRarFile(rewindableStream, options))
            {
                rewindableStream.Rewind(true);
                reader = RarReader.Open(rewindableStream, options);
                return true;
            }

            rewindableStream.Rewind(false);
            if (archiveTypes.HasFlag(ArchiveTypeMask.Tar) && TarArchive.IsTarFile(rewindableStream))
            {
                rewindableStream.Rewind(true);
                reader = TarReader.Open(rewindableStream, options);
                return true;
            }
            rewindableStream.Rewind(false);
            if (XZStream.IsXZStream(rewindableStream))
            {
                rewindableStream.Rewind(true);
                XZStream testStream = new XZStream(rewindableStream);
                if (archiveTypes.HasFlag(ArchiveTypeMask.Tar) && TarArchive.IsTarFile(testStream))
                {
                    rewindableStream.Rewind(true);
                    reader = new TarReader(rewindableStream, options, CompressionType.Xz);
                    return true;
                }
            }
            reader = null;
            return false;
        }
    }
}
