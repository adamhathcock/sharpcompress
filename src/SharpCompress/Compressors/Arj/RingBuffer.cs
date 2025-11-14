using System;
using System.Collections;
using System.Collections.Generic;

namespace SharpCompress.Compressors.Arj
{
    /// <summary>
    /// A fixed-size ring buffer where N must be a power of two.
    /// </summary>
    public class RingBuffer : IRingBuffer
    {
        private readonly byte[] _buffer;
        private int _cursor;

        public int BufferSize { get; }

        public int Cursor => _cursor;

        private readonly int _mask;

        public RingBuffer(int size)
        {
            if ((size & (size - 1)) != 0)
            {
                throw new ArgumentException("RingArrayBuffer size must be a power of two");
            }

            BufferSize = size;
            _buffer = new byte[size];
            _cursor = 0;
            _mask = size - 1;

            // Fill with spaces
            for (int i = 0; i < size; i++)
            {
                _buffer[i] = (byte)' ';
            }
        }

        public void SetCursor(int pos)
        {
            _cursor = pos & _mask;
        }

        public void Push(byte value)
        {
            int index = _cursor;
            _buffer[index & _mask] = value;
            _cursor = (index + 1) & _mask;
        }

        public byte this[int index] => _buffer[index & _mask];

        public HistoryIterator IterFromOffset(int offset)
        {
            int masked = (offset & _mask) + 1;
            int startIndex = _cursor + BufferSize - masked;
            return new HistoryIterator(this, startIndex);
        }

        public HistoryIterator IterFromPos(int pos)
        {
            int startIndex = pos & _mask;
            return new HistoryIterator(this, startIndex);
        }
    }
}
