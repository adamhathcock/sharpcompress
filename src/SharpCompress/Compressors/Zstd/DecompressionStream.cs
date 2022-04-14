using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ZstdSharp.Unsafe;

namespace ZstdSharp
{
    public class DecompressionStream : Stream
    {
        private readonly Stream innerStream;
        private readonly byte[] inputBuffer;
        private readonly int inputBufferSize;
        private Decompressor decompressor;
        private ZSTD_inBuffer_s input;
        private nuint lastDecompressResult = 0;

        public DecompressionStream(Stream stream, int bufferSize = 0)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead)
                throw new ArgumentException("Stream is not readable", nameof(stream));

            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            innerStream = stream;
            decompressor = new Decompressor();

            inputBufferSize = bufferSize > 0 ? bufferSize : (int) Methods.ZSTD_CStreamInSize().EnsureZstdSuccess();
            inputBuffer = ArrayPool<byte>.Shared.Rent(inputBufferSize);
            input = new ZSTD_inBuffer_s {pos = (nuint) inputBufferSize, size = (nuint) inputBufferSize};
        }

        public void SetParameter(ZSTD_dParameter parameter, int value)
        {
            EnsureNotDisposed();
            decompressor.SetParameter(parameter, value);
        }

        public int GetParameter(ZSTD_dParameter parameter)
        {
            EnsureNotDisposed();
            return decompressor.GetParameter(parameter);
        }

        public void LoadDictionary(byte[] dict)
        {
            EnsureNotDisposed();
            decompressor.LoadDictionary(dict);
        }

        ~DecompressionStream() => Dispose(false);

        protected override void Dispose(bool disposing)
        {
            if (decompressor == null)
                return;

            decompressor.Dispose();
            ArrayPool<byte>.Shared.Return(inputBuffer);
        }

        public override int Read(byte[] buffer, int offset, int count)
            => Read(new Span<byte>(buffer, offset, count));

#if !NETSTANDARD2_0 && !NETFRAMEWORK
        public override int Read(Span<byte> buffer)
#else
        public int Read(Span<byte> buffer)
#endif
        {
            EnsureNotDisposed();

            var output = new ZSTD_outBuffer_s {pos = 0, size = (nuint) buffer.Length};
            while (output.pos < output.size)
            {
                if (input.pos >= input.size)
                {
                    int bytesRead;
                    if ((bytesRead = innerStream.Read(inputBuffer, 0, inputBufferSize)) == 0)
                    {
                        if (lastDecompressResult != 0)
                            throw new EndOfStreamException("Premature end of stream");

                        break;
                    }

                    input.size = (nuint) bytesRead;
                    input.pos = 0;
                }

                lastDecompressResult = DecompressStream(ref output, buffer);
            }

            return (int) output.pos;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();

#if !NETSTANDARD2_0 && !NET461
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
#else
        public async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
#endif
        {
            EnsureNotDisposed();

            var output = new ZSTD_outBuffer_s { pos = 0, size = (nuint)buffer.Length};
            while (output.pos < output.size)
            {
                if (input.pos >= input.size)
                {
                    int bytesRead;
                    if ((bytesRead = await innerStream.ReadAsync(inputBuffer, 0, inputBufferSize, cancellationToken)
                        .ConfigureAwait(false)) == 0)
                    {
                        if (lastDecompressResult != 0)
                            throw new EndOfStreamException("Premature end of stream");

                        break;
                    }

                    input.size = (nuint) bytesRead;
                    input.pos = 0;
                }

                lastDecompressResult = DecompressStream(ref output, buffer.Span);
            }

            return (int) output.pos;
        }

        private unsafe nuint DecompressStream(ref ZSTD_outBuffer_s output, Span<byte> outputBuffer)
        {
            fixed (byte* inputBufferPtr = inputBuffer)
            fixed (byte* outputBufferPtr = outputBuffer)
            {
                input.src = inputBufferPtr;
                output.dst = outputBufferPtr;
                return decompressor.DecompressStream(ref input, ref output);
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private void EnsureNotDisposed()
        {
            if (decompressor == null)
                throw new ObjectDisposedException(nameof(DecompressionStream));
        }

#if NETSTANDARD2_0 || NET461
        public virtual ValueTask DisposeAsync()
        {
            try
            {
                Dispose();
                return default;
            }
            catch (Exception exc)
            {
                return new ValueTask(Task.FromException(exc));
            }
        }
#endif
    }
}
