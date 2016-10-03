using System;
using System.IO;

namespace SharpCompress.IO
{
    public struct ByteArrayPoolScope : IDisposable
    {
        private readonly ArraySegment<byte> array;
        public ByteArrayPoolScope(ArraySegment<byte> array)
        {
            this.array = array;
        }

        public byte[] Array => array.Array;
        public int Offset => array.Offset;
        public int Count => array.Count;

        public byte this[int index]
        {
            get { return Array[index]; }
            set { Array[index] = value; }
        }

        public void Dispose()
        {
            ByteArrayPool.Return(array);
        }
    }

    public static class ByteArrayPoolScopeExtensions
    {
        public static int Read(this Stream stream, ByteArrayPoolScope scope)
        {
            return stream.Read(scope.Array, scope.Offset, scope.Count);
        }
    }
}