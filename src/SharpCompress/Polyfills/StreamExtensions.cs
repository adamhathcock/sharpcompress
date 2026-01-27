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

            // For BufferedSubStream, use internal fast skip to avoid multiple cache refills
            if (stream is IO.BufferedSubStream bufferedSubStream)
            {
                bufferedSubStream.SkipInternal(advanceAmount);
                return;
            }

            // Use a very large buffer (1MB) to minimize Read() calls when skipping
            // This is critical for solid 7zip archives with LZMA compression
            var buffer = ArrayPool<byte>.Shared.Rent(1048576); // 1MB buffer
            try
            {
                long remaining = advanceAmount;
                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(remaining, buffer.Length);
                    var read = stream.Read(buffer, 0, toRead);
                    if (read == 0)
                    {
                        break; // End of stream
                    }
                    remaining -= read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void Skip() => stream.CopyTo(Stream.Null);

        public Task SkipAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if NET8_0_OR_GREATER
            return stream.CopyToAsync(Stream.Null, cancellationToken);
#else
            return stream.CopyToAsync(Stream.Null);
#endif
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
    }
}
