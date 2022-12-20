#nullable disable

using System.IO;

namespace SharpCompress.Compressors.LZMA.LZ;

internal class InWindow
{
    public byte[] _bufferBase; // pointer to buffer with data
    private Stream _stream;
    private uint _posLimit; // offset (from _buffer) of first byte when new block reading must be done
    private bool _streamEndWasReached; // if (true) then _streamPos shows real end of stream

    private uint _pointerToLastSafePosition;

    public uint _bufferOffset;

    public uint _blockSize; // Size of Allocated memory block
    public uint _pos; // offset (from _buffer) of curent byte
    private uint _keepSizeBefore; // how many BYTEs must be kept in buffer before _pos
    private uint _keepSizeAfter; // how many BYTEs must be kept buffer after _pos
    public uint _streamPos; // offset (from _buffer) of first not read byte from Stream

    public void MoveBlock()
    {
        var offset = _bufferOffset + _pos - _keepSizeBefore;

        // we need one additional byte, since MovePos moves on 1 byte.
        if (offset > 0)
        {
            offset--;
        }

        var numBytes = _bufferOffset + _streamPos - offset;

        // check negative offset ????
        for (uint i = 0; i < numBytes; i++)
        {
            _bufferBase[i] = _bufferBase[offset + i];
        }
        _bufferOffset -= offset;
    }

    public virtual void ReadBlock()
    {
        if (_streamEndWasReached)
        {
            return;
        }
        while (true)
        {
            var size = (int)((0 - _bufferOffset) + _blockSize - _streamPos);
            if (size == 0)
            {
                return;
            }
            var numReadBytes =
                _stream != null
                    ? _stream.Read(_bufferBase, (int)(_bufferOffset + _streamPos), size)
                    : 0;
            if (numReadBytes == 0)
            {
                _posLimit = _streamPos;
                var pointerToPostion = _bufferOffset + _posLimit;
                if (pointerToPostion > _pointerToLastSafePosition)
                {
                    _posLimit = _pointerToLastSafePosition - _bufferOffset;
                }

                _streamEndWasReached = true;
                return;
            }
            _streamPos += (uint)numReadBytes;
            if (_streamPos >= _pos + _keepSizeAfter)
            {
                _posLimit = _streamPos - _keepSizeAfter;
            }
        }
    }

    private void Free() => _bufferBase = null;

    public void Create(uint keepSizeBefore, uint keepSizeAfter, uint keepSizeReserv)
    {
        _keepSizeBefore = keepSizeBefore;
        _keepSizeAfter = keepSizeAfter;
        var blockSize = keepSizeBefore + keepSizeAfter + keepSizeReserv;
        if (_bufferBase is null || _blockSize != blockSize)
        {
            Free();
            _blockSize = blockSize;
            _bufferBase = new byte[_blockSize];
        }
        _pointerToLastSafePosition = _blockSize - keepSizeAfter;
        _streamEndWasReached = false;
    }

    public void SetStream(Stream stream)
    {
        _stream = stream;
        if (_streamEndWasReached)
        {
            _streamEndWasReached = false;
            if (IsDataStarved)
            {
                ReadBlock();
            }
        }
    }

    public void ReleaseStream() => _stream = null;

    public void Init()
    {
        _bufferOffset = 0;
        _pos = 0;
        _streamPos = 0;
        _streamEndWasReached = false;
        ReadBlock();
    }

    public void MovePos()
    {
        _pos++;
        if (_pos > _posLimit)
        {
            var pointerToPostion = _bufferOffset + _pos;
            if (pointerToPostion > _pointerToLastSafePosition)
            {
                MoveBlock();
            }
            ReadBlock();
        }
    }

    public byte GetIndexByte(int index) => _bufferBase[_bufferOffset + _pos + index];

    // index + limit have not to exceed _keepSizeAfter;
    public uint GetMatchLen(int index, uint distance, uint limit)
    {
        if (_streamEndWasReached)
        {
            if ((_pos + index) + limit > _streamPos)
            {
                limit = _streamPos - (uint)(_pos + index);
            }
        }
        distance++;

        // Byte *pby = _buffer + (size_t)_pos + index;
        var pby = _bufferOffset + _pos + (uint)index;

        uint i;
        for (i = 0; i < limit && _bufferBase[pby + i] == _bufferBase[pby + i - distance]; i++)
        {
            ;
        }
        return i;
    }

    public uint GetNumAvailableBytes() => _streamPos - _pos;

    public void ReduceOffsets(int subValue)
    {
        _bufferOffset += (uint)subValue;
        _posLimit -= (uint)subValue;
        _pos -= (uint)subValue;
        _streamPos -= (uint)subValue;
    }

    public bool IsDataStarved => _streamPos - _pos < _keepSizeAfter;
}
