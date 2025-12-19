#if NETFRAMEWORK || NETSTANDARD2_0

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress;

internal static class StreamExtensions
{
    internal static int Read(this Stream stream, Span<byte> buffer)
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

    internal static void Write(this Stream stream, ReadOnlySpan<byte> buffer)
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

    internal static async Task ReadExactlyAsync(
        this Stream stream,
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream
                .ReadAsync(buffer, offset + totalRead, count - totalRead, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }
            totalRead += read;
        }
    }
}

#endif
