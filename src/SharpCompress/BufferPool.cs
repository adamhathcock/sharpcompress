using System.Buffers;

namespace SharpCompress.Helpers;

internal static class BufferPool
{
    /// <summary>
    ///     gets a buffer from the pool
    /// </summary>
    /// <param name="bufferSize">size of the buffer</param>
    /// <returns>the buffer</returns>
    public static byte[] Rent(int bufferSize)
    {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
        return ArrayPool<byte>.Shared.Rent(bufferSize);
#else
        return new byte[bufferSize];
#endif
    }

    /// <summary>
    ///     returns a buffer to the pool
    /// </summary>
    /// <param name="buffer">the buffer to return</param>
    public static void Return(byte[] buffer)
    {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
        ArrayPool<byte>.Shared.Return(buffer);
#else
        // no-op
#endif
    }
}
