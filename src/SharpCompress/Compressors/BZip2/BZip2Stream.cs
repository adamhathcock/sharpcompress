using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.BZip2
{
    public sealed class BZip2Stream : Stream
    {
        private readonly Stream stream;
        private bool isDisposed;

        /// <summary>
        /// Create a BZip2Stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="compressionMode">Compression Mode</param>
        /// <param name="decompressConcatenated">Decompress Concatenated</param>
        public BZip2Stream(Stream stream, CompressionMode compressionMode,
                           bool decompressConcatenated)
        {
            Mode = compressionMode;
            if (Mode == CompressionMode.Compress)
            {
                this.stream = new CBZip2OutputStream(stream);
            }
            else
            {
                this.stream = new CBZip2InputStream(stream, decompressConcatenated);
            }
        }

        public void Finish()
        {
            (stream as CBZip2OutputStream)?.Finish();
        }

        protected override void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            if (disposing)
            {
                stream.Dispose();
            }
        }

        public CompressionMode Mode { get; }

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override void Flush()
        {
            stream.Flush();
        }

        public override long Length => stream.Length;

        public override long Position { get => stream.Position; set => stream.Position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return stream.ReadAsync(buffer, cancellationToken);
        }

        public override int ReadByte()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
        {
            return stream.WriteAsync(buffer, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void WriteByte(byte value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Consumes two bytes to test if there is a BZip2 header
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static async ValueTask<bool> IsBZip2Async(Stream stream, CancellationToken cancellationToken)
        {
            using var rented = MemoryPool<byte>.Shared.Rent(2);
            var chars = rented.Memory.Slice(0, 2);
            await stream.ReadAsync(chars, cancellationToken);
            if (chars.Length < 2 || chars.Span[0] != 'B' || chars.Span[1] != 'Z')
            {
                return false;
            }
            return true;
        }
    }
}