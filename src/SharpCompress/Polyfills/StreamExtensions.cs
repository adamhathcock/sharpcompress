#if !NETSTANDARD2_1

using System.Buffers;

namespace System.IO
{
    public static class StreamExtensions
    {
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