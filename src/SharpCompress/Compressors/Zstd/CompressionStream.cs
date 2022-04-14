using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ZstdSharp.Unsafe;

namespace ZstdSharp
{
    public class CompressionStream : Stream
    {
        private readonly Stream innerStream;
        private readonly byte[] outputBuffer;
        private Compressor compressor;
        private ZSTD_outBuffer_s output;

        public CompressionStream(Stream stream, int level = Compressor.DefaultCompressionLevel,
            int bufferSize = 0)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanWrite)
                throw new ArgumentException("Stream is not writable", nameof(stream));

            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            innerStream = stream;
            compressor = new Compressor(level);

            var outputBufferSize =
                bufferSize > 0 ? bufferSize : (int) Methods.ZSTD_CStreamOutSize().EnsureZstdSuccess();
            outputBuffer = ArrayPool<byte>.Shared.Rent(outputBufferSize);
            output = new ZSTD_outBuffer_s {pos = 0, size = (nuint) outputBufferSize};
        }

        public void SetParameter(ZSTD_cParameter parameter, int value)
        {
            EnsureNotDisposed();
            compressor.SetParameter(parameter, value);
        }

        public int GetParameter(ZSTD_cParameter parameter)
        {
            EnsureNotDisposed();
            return compressor.GetParameter(parameter);
        }

        public void LoadDictionary(byte[] dict)
        {
            EnsureNotDisposed();
            compressor.LoadDictionary(dict);
        }

        ~CompressionStream() => Dispose(false);

#if !NETSTANDARD2_0 && !NET461
        public override async ValueTask DisposeAsync()
#else
        public async ValueTask DisposeAsync()
#endif
        {
            if (compressor == null)
                return;

            try
            {
                await FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (compressor == null)
                return;

            try
            {
                if (disposing)
                    Flush();
            }
            finally
            {
                ReleaseUnmanagedResources();
            }
        }

        private void ReleaseUnmanagedResources()
        {
            compressor.Dispose();
            ArrayPool<byte>.Shared.Return(outputBuffer);
        }

        public override void Flush() 
            => WriteInternal(null, true);

        public override async Task FlushAsync(CancellationToken cancellationToken)
            => await WriteInternalAsync(null, true, cancellationToken).ConfigureAwait(false);

        public override void Write(byte[] buffer, int offset, int count)
            => Write(new ReadOnlySpan<byte>(buffer, offset, count));

#if !NETSTANDARD2_0 && !NET461
        public override void Write(ReadOnlySpan<byte> buffer)
            => WriteInternal(buffer, false);
#else
        public void Write(ReadOnlySpan<byte> buffer)
            => WriteInternal(buffer, false);
#endif

        private void WriteInternal(ReadOnlySpan<byte> buffer, bool lastChunk)
        {
            EnsureNotDisposed();

            var input = new ZSTD_inBuffer_s {pos = 0, size = buffer != null ? (nuint) buffer.Length : 0};
            nuint remaining;
            do
            {
                output.pos = 0;
                remaining = CompressStream(ref input, buffer,
                    lastChunk ? ZSTD_EndDirective.ZSTD_e_end : ZSTD_EndDirective.ZSTD_e_continue);

                var written = (int) output.pos;
                if (written > 0)
                    innerStream.Write(outputBuffer, 0, written);
            } while (lastChunk ? remaining > 0 : input.pos < input.size);
        }

        private async ValueTask WriteInternalAsync(ReadOnlyMemory<byte>? buffer, bool lastChunk,
            CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();

            var input = new ZSTD_inBuffer_s { pos = 0, size = buffer.HasValue ? (nuint)buffer.Value.Length : 0 };
            nuint remaining;
            do
            {
                output.pos = 0;
                remaining = CompressStream(ref input, buffer.HasValue ? buffer.Value.Span : null,
                    lastChunk ? ZSTD_EndDirective.ZSTD_e_end : ZSTD_EndDirective.ZSTD_e_continue);

                var written = (int) output.pos;
                if (written > 0)
                    await innerStream.WriteAsync(outputBuffer, 0, written, cancellationToken).ConfigureAwait(false);
            } while (lastChunk ? remaining > 0 : input.pos < input.size);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();

#if !NETSTANDARD2_0 && !NET461
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
            => await WriteInternalAsync(buffer, false, cancellationToken).ConfigureAwait(false);
#else
        public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
            => await WriteInternalAsync(buffer, false, cancellationToken).ConfigureAwait(false);
#endif

        internal unsafe nuint CompressStream(ref ZSTD_inBuffer_s input, ReadOnlySpan<byte> inputBuffer,
            ZSTD_EndDirective directive)
        {
            fixed (byte* inputBufferPtr = inputBuffer)
            fixed (byte* outputBufferPtr = outputBuffer)
            {
                input.src = inputBufferPtr;
                output.dst = outputBufferPtr;
                return compressor.CompressStream(ref input, ref output, directive).EnsureZstdSuccess();
            }
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private void EnsureNotDisposed()
        {
            if (compressor == null)
                throw new ObjectDisposedException(nameof(CompressionStream));
        }
    }
}
