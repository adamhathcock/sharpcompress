using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;

namespace SharpCompress.Writers.Tar
{
    public class TarWriter : AbstractWriter
    {
        public TarWriter(Stream destination, WriterOptions options)
            : base(ArchiveType.Tar, options)
        {
            if (!destination.CanWrite)
            {
                throw new ArgumentException("Tars require writable streams.");
            }
            switch (options.CompressionType)
            {
                case CompressionType.None:
                    break;
                case CompressionType.BZip2:
                {
                    destination = new BZip2Stream(destination, CompressionMode.Compress, true);
                }
                    break;
                case CompressionType.GZip:
                {
                    destination = new GZipStream(destination, CompressionMode.Compress, true);
                }
                    break;
                case CompressionType.LZip:
                {
                    destination = new LZipStream(destination, CompressionMode.Compress, true);
                }
                    break;
                default:
                {
                    throw new InvalidFormatException("Tar does not support compression: " + options.CompressionType);
                }
            }
            InitalizeStream(destination);
        }

        public override void Write(string filename, Stream source, DateTime? modificationTime)
        {
            Write(filename, source, modificationTime, null);
        }

        private string NormalizeFilename(string filename)
        {
            filename = filename.Replace('\\', '/');

            int pos = filename.IndexOf(':');
            if (pos >= 0)
            {
                filename = filename.Remove(0, pos + 1);
            }

            return filename.Trim('/');
        }

        public void Write(string filename, Stream source, DateTime? modificationTime, long? size)
        {
            if (!source.CanSeek && size == null)
            {
                throw new ArgumentException("Seekable stream is required if no size is given.");
            }

            long realSize = size ?? source.Length;

            TarHeader header = new TarHeader(WriterOptions.ArchiveEncoding);

            header.LastModifiedTime = modificationTime ?? TarHeader.Epoch;
            header.Name = NormalizeFilename(filename);
            header.Size = realSize;
            header.Write(OutputStream);
            size = source.TransferTo(OutputStream);
            PadTo512(size.Value, false);
        }

        private void PadTo512(long size, bool forceZeros)
        {
            int zeros = (int)size % 512;
            if (zeros == 0 && !forceZeros)
            {
                return;
            }
            zeros = 512 - zeros;
            OutputStream.Write(new byte[zeros], 0, zeros);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                PadTo512(0, true);
                PadTo512(0, true);
                switch (OutputStream)
                {
                    case BZip2Stream b:
                    {
                        b.Finish();
                        break;
                    }
                    case LZipStream l:
                    {
                        l.Finish();
                        break;
                    }
                }
            }
            base.Dispose(isDisposing);
        }
    }
}