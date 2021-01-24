using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.IO;

namespace SharpCompress.Writers.Tar
{
    public class TarWriter : AbstractWriter
    {
        private readonly bool finalizeArchiveOnClose;

        public TarWriter(Stream destination, TarWriterOptions options)
            : base(ArchiveType.Tar, options)
        {
            finalizeArchiveOnClose = options.FinalizeArchiveOnClose;

            if (!destination.CanWrite)
            {
                throw new ArgumentException("Tars require writable streams.");
            }
            if (WriterOptions.LeaveStreamOpen)
            {
                destination = new NonDisposingStream(destination);
            }
            switch (options.CompressionType)
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
                        destination = new GZipStream(destination, CompressionMode.Compress);
                    }
                    break;
                case CompressionType.LZip:
                    {
                        destination = new LZipStream(destination, CompressionMode.Compress);
                    }
                    break;
                default:
                    {
                        throw new InvalidFormatException("Tar does not support compression: " + options.CompressionType);
                    }
            }
            InitializeStream(destination);
        }

        public override async Task WriteAsync(string filename, Stream source, DateTime? modificationTime, CancellationToken cancellationToken = default)
        {
            if (!source.CanSeek)
            {
                throw new ArgumentException("Seekable stream is required if no size is given.");
            }

            long realSize = source.Length;

            TarHeader header = new(WriterOptions.ArchiveEncoding);

            header.LastModifiedTime = modificationTime ?? TarHeader.EPOCH;
            header.Name = NormalizeFilename(filename);
            header.Size = realSize;
            await header.WriteAsync(OutputStream);
            var size = await source.TransferToAsync(OutputStream);
            await PadTo512Async(size, false);
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

        private async Task PadTo512Async(long size, bool forceZeros)
        {
            int zeros = (int)size % 512;
            if (zeros == 0 && !forceZeros)
            {
                return;
            }
            zeros = 512 - zeros;
            using var zeroBuffer = MemoryPool<byte>.Shared.Rent(zeros);
            zeroBuffer.Memory.Span.Clear();
            await OutputStream.WriteAsync(zeroBuffer.Memory);
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            if (finalizeArchiveOnClose)
            {
                await PadTo512Async(0, true);
                await PadTo512Async(0, true);
            }
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
    }
}