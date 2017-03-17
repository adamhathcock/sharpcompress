using System;

namespace SharpCompress.IO
{
    public struct ByteArrayPoolScope : IDisposable
    {
        private readonly ArraySegment<byte> array;
        private int? overridenSize;
        public ByteArrayPoolScope(ArraySegment<byte> array)
        {
            this.array = array;
            overridenSize = null;
        }

        public byte[] Array => array.Array;
        public int Offset => array.Offset;
        public int Count => overridenSize ?? array.Count;

        public byte this[int index]
        {
            get { return Array[index]; }
            set { Array[index] = value; }
        }

        public void Dispose()
        {
            ByteArrayPool.Return(array);
        }

        public void OverrideSize(int newSize)
        {
            overridenSize = newSize;
        }
    }
}