using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress;

public static class StreamExtensions
{
    extension(Stream stream)
    {
        public void Skip(long advanceAmount)
        {
            if (stream.CanSeek)
            {
                stream.Position += advanceAmount;
                return;
            }

            using var readOnlySubStream = new IO.ReadOnlySubStream(stream, advanceAmount);
            readOnlySubStream.CopyTo(Stream.Null);
        }

        public void Skip() => stream.CopyTo(Stream.Null);

        public Task SkipAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return stream.CopyToAsync(Stream.Null);
        }

        internal int Read(Span<byte> buffer)
        {
            var temp = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                var read = stream.Read(temp, 0, buffer.Length);

                temp.AsSpan(0, read).CopyTo(buffer);

                return read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        internal void Write(ReadOnlySpan<byte> buffer)
        {
            var temp = ArrayPool<byte>.Shared.Rent(buffer.Length);

            buffer.CopyTo(temp);

            try
            {
                stream.Write(temp, 0, buffer.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        internal async Task ReadExactlyAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) =>
            await stream
                .ReadExactAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
    }
}
