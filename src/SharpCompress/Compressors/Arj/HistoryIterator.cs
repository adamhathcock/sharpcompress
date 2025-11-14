using System;
using System.Collections;
using System.Collections.Generic;

namespace SharpCompress.Compressors.Arj
{
    /// <summary>
    /// Iterator that reads & pushes values back into the ring buffer.
    /// </summary>
    public class HistoryIterator : IEnumerator<byte>
    {
        private int _index;
        private readonly IRingBuffer _ring;

        public HistoryIterator(IRingBuffer ring, int startIndex)
        {
            _ring = ring;
            _index = startIndex;
        }

        public bool MoveNext()
        {
            Current = _ring[_index];
            _index = unchecked(_index + 1);

            // Push value back into the ring buffer
            _ring.Push(Current);

            return true; // iterator is infinite
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        public byte Current { get; private set; }

        object IEnumerator.Current => Current;

        public void Dispose() { }
    }
}
