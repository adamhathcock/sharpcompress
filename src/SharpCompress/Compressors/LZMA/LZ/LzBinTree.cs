#nullable disable

using System;
using System.IO;

namespace SharpCompress.Compressors.LZMA.LZ;

internal sealed class BinTree : InWindow
{
    private uint _cyclicBufferPos;
    private uint _cyclicBufferSize;
    private uint _matchMaxLen;

    private uint[] _son;
    private uint[] _hash;

    private uint _cutValue = 0xFF;
    private uint _hashMask;
    private uint _hashSizeSum;

    private bool _hashArray = true;

    private const uint K_HASH2_SIZE = 1 << 10;
    private const uint K_HASH3_SIZE = 1 << 16;
    private const uint K_BT2_HASH_SIZE = 1 << 16;
    private const uint K_START_MAX_LEN = 1;
    private const uint K_HASH3_OFFSET = K_HASH2_SIZE;
    private const uint K_EMPTY_HASH_VALUE = 0;
    private const uint K_MAX_VAL_FOR_NORMALIZE = ((uint)1 << 31) - 1;

    private uint _kNumHashDirectBytes;
    private uint _kMinMatchCheck = 4;
    private uint _kFixHashSize = K_HASH2_SIZE + K_HASH3_SIZE;

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

    public new void SetStream(Stream stream) => base.SetStream(stream);

    public new void ReleaseStream() => base.ReleaseStream();

    public new void Init()
    {
        base.Init();
        for (uint i = 0; i < _hashSizeSum; i++)
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

    public new byte GetIndexByte(int index) => base.GetIndexByte(index);

    public new uint GetMatchLen(int index, uint distance, uint limit) =>
        base.GetMatchLen(index, distance, limit);

    public new uint GetNumAvailableBytes() => base.GetNumAvailableBytes();

    public void Create(
        uint historySize,
        uint keepAddBufferBefore,
        uint matchMaxLen,
        uint keepAddBufferAfter
    )
    {
        if (historySize > K_MAX_VAL_FOR_NORMALIZE - 256)
        {
            throw new ArgumentOutOfRangeException(nameof(historySize));
        }
        _cutValue = 16 + (matchMaxLen >> 1);

        var windowReservSize =
            ((historySize + keepAddBufferBefore + matchMaxLen + keepAddBufferAfter) / 2) + 256;

        Create(
            historySize + keepAddBufferBefore,
            matchMaxLen + keepAddBufferAfter,
            windowReservSize
        );

        _matchMaxLen = matchMaxLen;

        var cyclicBufferSize = historySize + 1;
        if (_cyclicBufferSize != cyclicBufferSize)
        {
            _son = new uint[(_cyclicBufferSize = cyclicBufferSize) * 2];
        }

        var hs = K_BT2_HASH_SIZE;

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
            _hash = new uint[_hashSizeSum = hs];
        }
    }

    public uint GetMatches(uint[] distances)
    {
        uint lenLimit;
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

        uint offset = 0;
        var matchMinPos = (_pos > _cyclicBufferSize) ? (_pos - _cyclicBufferSize) : 0;
        var cur = _bufferOffset + _pos;
        var maxLen = K_START_MAX_LEN; // to avoid items for len < hashSize;
        uint hashValue,
            hash2Value = 0,
            hash3Value = 0;

        if (_hashArray)
        {
            var temp = Crc.TABLE[_bufferBase[cur]] ^ _bufferBase[cur + 1];
            hash2Value = temp & (K_HASH2_SIZE - 1);
            temp ^= ((uint)(_bufferBase[cur + 2]) << 8);
            hash3Value = temp & (K_HASH3_SIZE - 1);
            hashValue = (temp ^ (Crc.TABLE[_bufferBase[cur + 3]] << 5)) & _hashMask;
        }
        else
        {
            hashValue = _bufferBase[cur] ^ ((uint)(_bufferBase[cur + 1]) << 8);
        }

        var curMatch = _hash[_kFixHashSize + hashValue];
        if (_hashArray)
        {
            var curMatch2 = _hash[hash2Value];
            var curMatch3 = _hash[K_HASH3_OFFSET + hash3Value];
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

        var ptr0 = (_cyclicBufferPos << 1) + 1;
        var ptr1 = (_cyclicBufferPos << 1);

        uint len0,
            len1;
        len0 = len1 = _kNumHashDirectBytes;

        if (_kNumHashDirectBytes != 0)
        {
            if (curMatch > matchMinPos)
            {
                if (
                    _bufferBase[_bufferOffset + curMatch + _kNumHashDirectBytes]
                    != _bufferBase[cur + _kNumHashDirectBytes]
                )
                {
                    distances[offset++] = maxLen = _kNumHashDirectBytes;
                    distances[offset++] = _pos - curMatch - 1;
                }
            }
        }

        var count = _cutValue;

        while (true)
        {
            if (curMatch <= matchMinPos || count-- == 0)
            {
                _son[ptr0] = _son[ptr1] = K_EMPTY_HASH_VALUE;
                break;
            }
            var delta = _pos - curMatch;
            var cyclicPos =
                (
                    (delta <= _cyclicBufferPos)
                        ? (_cyclicBufferPos - delta)
                        : (_cyclicBufferPos - delta + _cyclicBufferSize)
                ) << 1;

            var pby1 = _bufferOffset + curMatch;
            var len = Math.Min(len0, len1);
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

    public void Skip(uint num)
    {
        do
        {
            uint lenLimit;
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

            var matchMinPos = (_pos > _cyclicBufferSize) ? (_pos - _cyclicBufferSize) : 0;
            var cur = _bufferOffset + _pos;

            uint hashValue;

            if (_hashArray)
            {
                var temp = Crc.TABLE[_bufferBase[cur]] ^ _bufferBase[cur + 1];
                var hash2Value = temp & (K_HASH2_SIZE - 1);
                _hash[hash2Value] = _pos;
                temp ^= ((uint)(_bufferBase[cur + 2]) << 8);
                var hash3Value = temp & (K_HASH3_SIZE - 1);
                _hash[K_HASH3_OFFSET + hash3Value] = _pos;
                hashValue = (temp ^ (Crc.TABLE[_bufferBase[cur + 3]] << 5)) & _hashMask;
            }
            else
            {
                hashValue = _bufferBase[cur] ^ ((uint)(_bufferBase[cur + 1]) << 8);
            }

            var curMatch = _hash[_kFixHashSize + hashValue];
            _hash[_kFixHashSize + hashValue] = _pos;

            var ptr0 = (_cyclicBufferPos << 1) + 1;
            var ptr1 = (_cyclicBufferPos << 1);

            uint len0,
                len1;
            len0 = len1 = _kNumHashDirectBytes;

            var count = _cutValue;
            while (true)
            {
                if (curMatch <= matchMinPos || count-- == 0)
                {
                    _son[ptr0] = _son[ptr1] = K_EMPTY_HASH_VALUE;
                    break;
                }

                var delta = _pos - curMatch;
                var cyclicPos =
                    (
                        (delta <= _cyclicBufferPos)
                            ? (_cyclicBufferPos - delta)
                            : (_cyclicBufferPos - delta + _cyclicBufferSize)
                    ) << 1;

                var pby1 = _bufferOffset + curMatch;
                var len = Math.Min(len0, len1);
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
        } while (--num != 0);
    }

    private void NormalizeLinks(uint[] items, uint numItems, uint subValue)
    {
        for (uint i = 0; i < numItems; i++)
        {
            var value = items[i];
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
        var subValue = _pos - _cyclicBufferSize;
        NormalizeLinks(_son, _cyclicBufferSize * 2, subValue);
        NormalizeLinks(_hash, _hashSizeSum, subValue);
        ReduceOffsets((int)subValue);
    }

    public void SetCutValue(uint cutValue) => _cutValue = cutValue;
}
