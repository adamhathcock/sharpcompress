using System;
using System.IO;

namespace SharpCompress.Compressors.Filters
{
    internal class DeltaFilter : Filter
    {
        private const int DISTANCE_MIN = 1;
        private const int DISTANCE_MAX = 256;
        private const int DISTANCE_MASK = DISTANCE_MAX - 1;

        private int _distance;
        private byte[] _history;
        private int _position;

        public DeltaFilter(bool isEncoder, Stream baseStream, byte[] info)
            : base(isEncoder, baseStream, 1)
        {
            _distance = info[0];
            _history = new byte[DISTANCE_MAX];
            _position = 0;
        }

        protected override int Transform(byte[] buffer, int offset, int count)
        {
            var end = offset + count;

            for (var i = offset; i < end; i++)
            {
                buffer[i] += _history[(_distance + _position--) & DISTANCE_MASK];
                _history[_position & DISTANCE_MASK] = buffer[i];
            }

            return count;
        }
    }
}
