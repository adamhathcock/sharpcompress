#if NETFRAMEWORK || NETSTANDARD2_0

using System;
using System.Buffers;
using System.IO;

namespace SharpCompress
{
    internal static class StreamExtensions
    {
        internal static int Read(this Stream stream, Span<byte> buffer)
        {
            byte[] temp = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                int read = stream.Read(temp, 0, buffer.Length);

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
            byte[] temp = ArrayPool<byte>.Shared.Rent(buffer.Length);

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

#endif
