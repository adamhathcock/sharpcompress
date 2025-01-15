using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Shrink
{
    internal class BitStream
    {
        private byte[] _src;
        private int _srcLen;
        private int _byteIdx;
        private int _bitIdx;
        private int _bitsLeft;
        private ulong _bitBuffer;
        private static uint[] _maskBits = new uint[17]
        {
            0U,
            1U,
            3U,
            7U,
            15U,
            31U,
            63U,
            (uint)sbyte.MaxValue,
            (uint)byte.MaxValue,
            511U,
            1023U,
            2047U,
            4095U,
            8191U,
            16383U,
            (uint)short.MaxValue,
            (uint)ushort.MaxValue,
        };

        public BitStream(byte[] src, int srcLen)
        {
            _src = src;
            _srcLen = srcLen;
            _byteIdx = 0;
            _bitIdx = 0;
        }

        public int BytesRead => (_byteIdx << 3) + _bitIdx;

        private int NextByte()
        {
            if (_byteIdx >= _srcLen)
            {
                return 0;
            }

            return _src[_byteIdx++];
        }

        public int NextBits(int nbits)
        {
            var result = 0;
            if (nbits > _bitsLeft)
            {
                int num;
                while (_bitsLeft <= 24 && (num = NextByte()) != 1234)
                {
                    _bitBuffer |= (ulong)num << _bitsLeft;
                    _bitsLeft += 8;
                }
            }
            result = (int)((long)_bitBuffer & (long)_maskBits[nbits]);
            _bitBuffer >>= nbits;
            _bitsLeft -= nbits;
            return result;
        }

        public bool Advance(int count)
        {
            if (_byteIdx > _srcLen)
            {
                return false;
            }
            return true;
        }
    }
}
