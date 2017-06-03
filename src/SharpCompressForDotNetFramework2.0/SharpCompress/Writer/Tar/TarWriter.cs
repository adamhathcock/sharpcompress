using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressor;
using SharpCompress.Compressor.BZip2;
using SharpCompress.Compressor.Deflate;

namespace SharpCompress.Writer.Tar
{
    public class TarWriter : AbstractWriter
    {
        public TarWriter(Stream destination, CompressionInfo compressionInfo)
            : base(ArchiveType.Tar)
        {
            if (!destination.CanWrite)
            {
                throw new ArgumentException("Tars require writable streams.");
            }
            switch (compressionInfo.Type)
            {
                case CompressionType.None:
                    break;
                case CompressionType.BZip2:
                    {
                        destination = new BZip2Stream(destination, CompressionMode.Compress, false);
                    }
                    break;
                case CompressionType.GZip:
                    {
                        destination = new GZipStream(destination, CompressionMode.Compress, false);
                    }
                    break;
                default:
                    {
                        throw new InvalidFormatException("Tar does not support compression: " + compressionInfo.Type);
                    }
            }
            InitalizeStream(destination, false);
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
                filename = filename.Remove(0, pos + 1);

            return filename.Trim('/');
        }
        public void Write(string filename, Stream source, DateTime? modificationTime, long? size)
        {
            if (!source.CanSeek && size == null)
            {
                throw new ArgumentException("Seekable stream is required if no size is given.");
            }

            long realSize = size ?? source.Length;


            TarHeader header = new TarHeader();
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
                OutputStream.Dispose();
            }
            base.Dispose(isDisposing);
        }
    }
}
