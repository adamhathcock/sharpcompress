using System;
#if !NO_BUFFERS
using System.Buffers;
#endif

namespace SharpCompress.IO
{
    public static class ByteArrayPool
    {
#if !NO_BUFFERS
        private static readonly ArrayPool<byte> POOL = ArrayPool<byte>.Create();
#endif
        public static ArraySegment<byte> Rent(int size)
        {
#if NO_BUFFERS
            return new ArraySegment<byte>(new byte[size]);
#else
            return new ArraySegment<byte>(POOL.Rent(size), 0, size);
#endif
        }

        public static byte[] RentWritable(int size)
        {
#if NO_BUFFERS
            return new byte[size];
#else
            return POOL.Rent(size);
#endif
        }

        public static ByteArrayPoolScope RentScope(int size)
        {
            return new ByteArrayPoolScope(Rent(size));
        }


        public static void Return(ArraySegment<byte> array)
        {
#if NO_BUFFERS
#else
            POOL.Return(array.Array);
#endif
        }
        public static void Return(byte[] array)
        {
#if NO_BUFFERS
#else
            POOL.Return(array);
#endif
        }
    }
}
