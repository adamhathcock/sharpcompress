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
        private bool finalizeArchiveOnClose;

        private TarWriter(TarWriterOptions options)
            : base(ArchiveType.Tar, options)
        {
        }

        public static async ValueTask<TarWriter> CreateAsync(Stream destination, TarWriterOptions options, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            var tw = new TarWriter(options);
            tw.finalizeArchiveOnClose = options.FinalizeArchiveOnClose;

            if (!destination.CanWrite)
            {
                throw new ArgumentException("Tars require writable streams.");
            }
            if (tw.WriterOptions.LeaveStreamOpen)
            {
                destination = new NonDisposingStream(destination);
            }
            switch (options.CompressionType)
            {
                case CompressionType.None:
                    break;
               /* case CompressionType.BZip2:
                    {
                        destination = await BZip2Stream.CreateAsync(destination, CompressionMode.Compress, false, cancellationToken);
                    }
                    break;     */
                case CompressionType.GZip:
                    {
                        destination = new GZipStream(destination, CompressionMode.Compress);
                    }
                    break;
                case CompressionType.LZip:
                    {
                        destination = await LZipStream.CreateAsync(destination, CompressionMode.Compress);
                    }
                    break;
                default:
                    {
                        throw new InvalidFormatException("Tar does not support compression: " + options.CompressionType);
                    }
            }
            tw.InitializeStream(destination);
            return tw;
        }

        public override ValueTask WriteAsync(string filename, Stream source, DateTime? modificationTime, CancellationToken cancellationToken = default)
        {
            return WriteAsync(filename, source, modificationTime, null, cancellationToken);
        }

        public async ValueTask WriteAsync(string filename, Stream source, DateTime? modificationTime, long? size, CancellationToken cancellationToken = default)
        {
            if (!source.CanSeek && size == null)
            {
                throw new ArgumentException("Seekable stream is required if no size is given.");
            }

            long realSize = size ?? source.Length;

            TarHeader header = new(WriterOptions.ArchiveEncoding);

            header.LastModifiedTime = modificationTime ?? TarHeader.EPOCH;
            header.Name = NormalizeFilename(filename);
            header.Size = realSize;
            await header.WriteAsync(OutputStream);
            size = await source.TransferToAsync(OutputStream, cancellationToken);
            await PadTo512Async(size.Value, false);
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
            await OutputStream.WriteAsync(zeroBuffer.Memory.Slice(0, zeros));
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
              /*  case BZip2Stream b:
                    {
                        await b.FinishAsync(CancellationToken.None);
                        break;
                    }     */
                case LZipStream l:
                    {
                        await l.FinishAsync();
                        break;
                    }
            }
        }
    }
}
