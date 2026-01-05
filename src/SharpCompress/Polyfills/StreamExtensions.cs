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

            using var buffer = MemoryPool<byte>.Shared.Rent(Utility.TEMP_BUFFER_SIZE);
            while (advanceAmount > 0)
            {
                var toRead = (int)Math.Min(buffer.Memory.Length, advanceAmount);
                var read = stream.Read(buffer.Memory.Slice(0, toRead).Span);
                if (read <= 0)
                {
                    break;
                }
                advanceAmount -= read;
            }
        }

        public void Skip()
        {
            using var buffer = MemoryPool<byte>.Shared.Rent(Utility.TEMP_BUFFER_SIZE);
            while (stream.Read(buffer.Memory.Span) > 0) { }
        }

        public async Task SkipAsync(CancellationToken cancellationToken = default)
        {
            var array = ArrayPool<byte>.Shared.Rent(Utility.TEMP_BUFFER_SIZE);
            try
            {
                while (true)
                {
                    var read = await stream
                        .ReadAsync(array, 0, array.Length, cancellationToken)
                        .ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
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
