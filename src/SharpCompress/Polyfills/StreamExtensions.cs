#if NET461 || NETSTANDARD2_0

using System.Buffers;

namespace System.IO
{
    public static class StreamExtensions
    {
        public static int Read(this Stream stream, Span<byte> buffer)
        {
            byte[] temp = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                int read = stream.Read(buffer);

                temp.AsSpan(0, read).CopyTo(buffer);

                return read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }

        public static void Write(this Stream stream, ReadOnlySpan<byte> buffer)
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
