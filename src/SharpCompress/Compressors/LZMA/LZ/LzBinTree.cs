using System;
using System.IO;

namespace SharpCompress.Compressors.LZMA.LZ
{
    internal class BinTree : InWindow
    {
        private UInt32 _cyclicBufferPos;
        private UInt32 _cyclicBufferSize;
        private UInt32 _matchMaxLen;

        private UInt32[] _son;
        private UInt32[] _hash;

        private UInt32 _cutValue = 0xFF;
        private UInt32 _hashMask;
        private UInt32 _hashSizeSum;

        private bool _hashArray = true;

        private const UInt32 K_HASH2_SIZE = 1 << 10;
        private const UInt32 K_HASH3_SIZE = 1 << 16;
        private const UInt32 K_BT2_HASH_SIZE = 1 << 16;
        private const UInt32 K_START_MAX_LEN = 1;
        private const UInt32 K_HASH3_OFFSET = K_HASH2_SIZE;
        private const UInt32 K_EMPTY_HASH_VALUE = 0;
        private const UInt32 K_MAX_VAL_FOR_NORMALIZE = ((UInt32)1 << 31) - 1;

        private UInt32 _kNumHashDirectBytes;
        private UInt32 _kMinMatchCheck = 4;
        private UInt32 _kFixHashSize = K_HASH2_SIZE + K_HASH3_SIZE;

        public void SetType(int numHashBytes)
        {
            _hashArray = (numHashBytes > 2);
            if (_hashArray)
            {
                _kNumHashDirectBytes = 0;
                _kMinMatchCheck = 4;
                _kFixHashSize = K_HASH2_SIZE + K_HASH3_SIZE;
            }
            else
            {
                _kNumHashDirectBytes = 2;
                _kMinMatchCheck = 2 + 1;
                _kFixHashSize = 0;
            }
        }

        public new void SetStream(Stream stream)
        {
            base.SetStream(stream);
        }

        public new void ReleaseStream()
        {
            base.ReleaseStream();
        }

        public new void Init()
        {
            base.Init();
            for (UInt32 i = 0; i < _hashSizeSum; i++)
            {
                _hash[i] = K_EMPTY_HASH_VALUE;
            }
            _cyclicBufferPos = 0;
            ReduceOffsets(-1);
        }

        public new void MovePos()
        {
            if (++_cyclicBufferPos >= _cyclicBufferSize)
            {
                _cyclicBufferPos = 0;
            }
            base.MovePos();
            if (_pos == K_MAX_VAL_FOR_NORMALIZE)
            {
                Normalize();
            }
        }

        public new Byte GetIndexByte(Int32 index)
        {
            return base.GetIndexByte(index);
        }

        public new UInt32 GetMatchLen(Int32 index, UInt32 distance, UInt32 limit)
        {
            return base.GetMatchLen(index, distance, limit);
        }

        public new UInt32 GetNumAvailableBytes()
        {
            return base.GetNumAvailableBytes();
        }

        public void Create(UInt32 historySize, UInt32 keepAddBufferBefore,
                           UInt32 matchMaxLen, UInt32 keepAddBufferAfter)
        {
            if (historySize > K_MAX_VAL_FOR_NORMALIZE - 256)
            {
                throw new Exception();
            }
            _cutValue = 16 + (matchMaxLen >> 1);

            UInt32 windowReservSize = (historySize + keepAddBufferBefore +
                                       matchMaxLen + keepAddBufferAfter) / 2 + 256;

            base.Create(historySize + keepAddBufferBefore, matchMaxLen + keepAddBufferAfter, windowReservSize);

            _matchMaxLen = matchMaxLen;

            UInt32 cyclicBufferSize = historySize + 1;
            if (_cyclicBufferSize != cyclicBufferSize)
            {
                _son = new UInt32[(_cyclicBufferSize = cyclicBufferSize) * 2];
            }

            UInt32 hs = K_BT2_HASH_SIZE;

            if (_hashArray)
            {
                hs = historySize - 1;
                hs |= (hs >> 1);
                hs |= (hs >> 2);
                hs |= (hs >> 4);
                hs |= (hs >> 8);
                hs >>= 1;
                hs |= 0xFFFF;
                if (hs > (1 << 24))
                {
                    hs >>= 1;
                }
                _hashMask = hs;
                hs++;
                hs += _kFixHashSize;
            }
            if (hs != _hashSizeSum)
            {
                _hash = new UInt32[_hashSizeSum = hs];
            }
        }

        public UInt32 GetMatches(UInt32[] distances)
        {
            UInt32 lenLimit;
            if (_pos + _matchMaxLen <= _streamPos)
            {
                lenLimit = _matchMaxLen;
            }
            else
            {
                lenLimit = _streamPos - _pos;
                if (lenLimit < _kMinMatchCheck)
                {
                    MovePos();
                    return 0;
                }
            }

            UInt32 offset = 0;
            UInt32 matchMinPos = (_pos > _cyclicBufferSize) ? (_pos - _cyclicBufferSize) : 0;
            UInt32 cur = _bufferOffset + _pos;
            UInt32 maxLen = K_START_MAX_LEN; // to avoid items for len < hashSize;
            UInt32 hashValue, hash2Value = 0, hash3Value = 0;

            if (_hashArray)
            {
                UInt32 temp = Crc.TABLE[_bufferBase[cur]] ^ _bufferBase[cur + 1];
                hash2Value = temp & (K_HASH2_SIZE - 1);
                temp ^= ((UInt32)(_bufferBase[cur + 2]) << 8);
                hash3Value = temp & (K_HASH3_SIZE - 1);
                hashValue = (temp ^ (Crc.TABLE[_bufferBase[cur + 3]] << 5)) & _hashMask;
            }
            else
            {
                hashValue = _bufferBase[cur] ^ ((UInt32)(_bufferBase[cur + 1]) << 8);
            }

            UInt32 curMatch = _hash[_kFixHashSize + hashValue];
            if (_hashArray)
            {
                UInt32 curMatch2 = _hash[hash2Value];
                UInt32 curMatch3 = _hash[K_HASH3_OFFSET + hash3Value];
                _hash[hash2Value] = _pos;
                _hash[K_HASH3_OFFSET + hash3Value] = _pos;
                if (curMatch2 > matchMinPos)
                {
                    if (_bufferBase[_bufferOffset + curMatch2] == _bufferBase[cur])
                    {
                        distances[offset++] = maxLen = 2;
                        distances[offset++] = _pos - curMatch2 - 1;
                    }
                }
                if (curMatch3 > matchMinPos)
                {
                    if (_bufferBase[_bufferOffset + curMatch3] == _bufferBase[cur])
                    {
                        if (curMatch3 == curMatch2)
                        {
                            offset -= 2;
                        }
                        distances[offset++] = maxLen = 3;
                        distances[offset++] = _pos - curMatch3 - 1;
                        curMatch2 = curMatch3;
                    }
                }
                if (offset != 0 && curMatch2 == curMatch)
                {
                    offset -= 2;
                    maxLen = K_START_MAX_LEN;
                }
            }

            _hash[_kFixHashSize + hashValue] = _pos;

            UInt32 ptr0 = (_cyclicBufferPos << 1) + 1;
            UInt32 ptr1 = (_cyclicBufferPos << 1);

            UInt32 len0, len1;
            len0 = len1 = _kNumHashDirectBytes;

            if (_kNumHashDirectBytes != 0)
            {
                if (curMatch > matchMinPos)
                {
                    if (_bufferBase[_bufferOffset + curMatch + _kNumHashDirectBytes] !=
                        _bufferBase[cur + _kNumHashDirectBytes])
                    {
                        distances[offset++] = maxLen = _kNumHashDirectBytes;
                        distances[offset++] = _pos - curMatch - 1;
                    }
                }
            }

            UInt32 count = _cutValue;

            while (true)
            {
                if (curMatch <= matchMinPos || count-- == 0)
                {
                    _son[ptr0] = _son[ptr1] = K_EMPTY_HASH_VALUE;
                    break;
                }
                UInt32 delta = _pos - curMatch;
                UInt32 cyclicPos = ((delta <= _cyclicBufferPos)
                                        ? (_cyclicBufferPos - delta)
                                        : (_cyclicBufferPos - delta + _cyclicBufferSize)) << 1;

                UInt32 pby1 = _bufferOffset + curMatch;
                UInt32 len = Math.Min(len0, len1);
                if (_bufferBase[pby1 + len] == _bufferBase[cur + len])
                {
                    while (++len != lenLimit)
                    {
                        if (_bufferBase[pby1 + len] != _bufferBase[cur + len])
                        {
                            break;
                        }
                    }
                    if (maxLen < len)
                    {
                        distances[offset++] = maxLen = len;
                        distances[offset++] = delta - 1;
                        if (len == lenLimit)
                        {
                            _son[ptr1] = _son[cyclicPos];
                            _son[ptr0] = _son[cyclicPos + 1];
                            break;
                        }
                    }
                }
                if (_bufferBase[pby1 + len] < _bufferBase[cur + len])
                {
                    _son[ptr1] = curMatch;
                    ptr1 = cyclicPos + 1;
                    curMatch = _son[ptr1];
                    len1 = len;
                }
                else
                {
                    _son[ptr0] = curMatch;
                    ptr0 = cyclicPos;
                    curMatch = _son[ptr0];
                    len0 = len;
                }
            }
            MovePos();
            return offset;
        }

        public void Skip(UInt32 num)
        {
            do
            {
                UInt32 lenLimit;
                if (_pos + _matchMaxLen <= _streamPos)
                {
                    lenLimit = _matchMaxLen;
                }
                else
                {
                    lenLimit = _streamPos - _pos;
                    if (lenLimit < _kMinMatchCheck)
                    {
                        MovePos();
                        continue;
                    }
                }

                UInt32 matchMinPos = (_pos > _cyclicBufferSize) ? (_pos - _cyclicBufferSize) : 0;
                UInt32 cur = _bufferOffset + _pos;

                UInt32 hashValue;

                if (_hashArray)
                {
                    UInt32 temp = Crc.TABLE[_bufferBase[cur]] ^ _bufferBase[cur + 1];
                    UInt32 hash2Value = temp & (K_HASH2_SIZE - 1);
                    _hash[hash2Value] = _pos;
                    temp ^= ((UInt32)(_bufferBase[cur + 2]) << 8);
                    UInt32 hash3Value = temp & (K_HASH3_SIZE - 1);
                    _hash[K_HASH3_OFFSET + hash3Value] = _pos;
                    hashValue = (temp ^ (Crc.TABLE[_bufferBase[cur + 3]] << 5)) & _hashMask;
                }
                else
                {
                    hashValue = _bufferBase[cur] ^ ((UInt32)(_bufferBase[cur + 1]) << 8);
                }

                UInt32 curMatch = _hash[_kFixHashSize + hashValue];
                _hash[_kFixHashSize + hashValue] = _pos;

                UInt32 ptr0 = (_cyclicBufferPos << 1) + 1;
                UInt32 ptr1 = (_cyclicBufferPos << 1);

                UInt32 len0, len1;
                len0 = len1 = _kNumHashDirectBytes;

                UInt32 count = _cutValue;
                while (true)
                {
                    if (curMatch <= matchMinPos || count-- == 0)
                    {
                        _son[ptr0] = _son[ptr1] = K_EMPTY_HASH_VALUE;
                        break;
                    }

                    UInt32 delta = _pos - curMatch;
                    UInt32 cyclicPos = ((delta <= _cyclicBufferPos)
                                            ? (_cyclicBufferPos - delta)
                                            : (_cyclicBufferPos - delta + _cyclicBufferSize)) << 1;

                    UInt32 pby1 = _bufferOffset + curMatch;
                    UInt32 len = Math.Min(len0, len1);
                    if (_bufferBase[pby1 + len] == _bufferBase[cur + len])
                    {
                        while (++len != lenLimit)
                        {
                            if (_bufferBase[pby1 + len] != _bufferBase[cur + len])
                            {
                                break;
                            }
                        }
                        if (len == lenLimit)
                        {
                            _son[ptr1] = _son[cyclicPos];
                            _son[ptr0] = _son[cyclicPos + 1];
                            break;
                        }
                    }
                    if (_bufferBase[pby1 + len] < _bufferBase[cur + len])
                    {
                        _son[ptr1] = curMatch;
                        ptr1 = cyclicPos + 1;
                        curMatch = _son[ptr1];
                        len1 = len;
                    }
                    else
                    {
                        _son[ptr0] = curMatch;
                        ptr0 = cyclicPos;
                        curMatch = _son[ptr0];
                        len0 = len;
                    }
                }
                MovePos();
            }
            while (--num != 0);
        }

        private void NormalizeLinks(UInt32[] items, UInt32 numItems, UInt32 subValue)
        {
            for (UInt32 i = 0; i < numItems; i++)
            {
                UInt32 value = items[i];
                if (value <= subValue)
                {
                    value = K_EMPTY_HASH_VALUE;
                }
                else
                {
                    value -= subValue;
                }
                items[i] = value;
            }
        }

        private void Normalize()
        {
            UInt32 subValue = _pos - _cyclicBufferSize;
            NormalizeLinks(_son, _cyclicBufferSize * 2, subValue);
            NormalizeLinks(_hash, _hashSizeSum, subValue);
            ReduceOffsets((Int32)subValue);
        }

        public void SetCutValue(UInt32 cutValue)
        {
            _cutValue = cutValue;
        }
    }
}