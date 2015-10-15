namespace SharpCompress.Compressor.LZMA.LZ
{
    using System;
    using System.IO;

    internal class InWindow
    {
        public uint _blockSize;
        public byte[] _bufferBase = null;
        public uint _bufferOffset;
        private uint _keepSizeAfter;
        private uint _keepSizeBefore;
        private uint _pointerToLastSafePosition;
        public uint _pos;
        private uint _posLimit;
        private Stream _stream;
        private bool _streamEndWasReached;
        public uint _streamPos;

        public void Create(uint keepSizeBefore, uint keepSizeAfter, uint keepSizeReserv)
        {
            this._keepSizeBefore = keepSizeBefore;
            this._keepSizeAfter = keepSizeAfter;
            uint num = (keepSizeBefore + keepSizeAfter) + keepSizeReserv;
            if ((this._bufferBase == null) || (this._blockSize != num))
            {
                this.Free();
                this._blockSize = num;
                this._bufferBase = new byte[this._blockSize];
            }
            this._pointerToLastSafePosition = this._blockSize - keepSizeAfter;
            this._streamEndWasReached = false;
        }

        private void Free()
        {
            this._bufferBase = null;
        }

        public byte GetIndexByte(int index)
        {
            return this._bufferBase[(int) ((IntPtr) ((this._bufferOffset + this._pos) + index))];
        }

        public uint GetMatchLen(int index, uint distance, uint limit)
        {
            if (this._streamEndWasReached && (((this._pos + index) + limit) > this._streamPos))
            {
                limit = this._streamPos - (this._pos + ((uint) index));
            }
            distance++;
            uint num = (this._bufferOffset + this._pos) + ((uint) index);
            uint num2 = 0;
            while ((num2 < limit) && (this._bufferBase[num + num2] == this._bufferBase[(num + num2) - distance]))
            {
                num2++;
            }
            return num2;
        }

        public uint GetNumAvailableBytes()
        {
            return (this._streamPos - this._pos);
        }

        public void Init()
        {
            this._bufferOffset = 0;
            this._pos = 0;
            this._streamPos = 0;
            this._streamEndWasReached = false;
            this.ReadBlock();
        }

        public void MoveBlock()
        {
            uint num = (this._bufferOffset + this._pos) - this._keepSizeBefore;
            if (num > 0)
            {
                num--;
            }
            uint num2 = (this._bufferOffset + this._streamPos) - num;
            for (uint i = 0; i < num2; i++)
            {
                this._bufferBase[i] = this._bufferBase[num + i];
            }
            this._bufferOffset -= num;
        }

        public void MovePos()
        {
            this._pos++;
            if (this._pos > this._posLimit)
            {
                uint num = this._bufferOffset + this._pos;
                if (num > this._pointerToLastSafePosition)
                {
                    this.MoveBlock();
                }
                this.ReadBlock();
            }
        }

        public virtual void ReadBlock()
        {
            bool flag;
            if (this._streamEndWasReached)
            {
                return;
            }
        Label_00F9:
            flag = true;
            int count = (int) (((0 - this._bufferOffset) + this._blockSize) - this._streamPos);
            if (count != 0)
            {
                int num2 = (this._stream != null) ? this._stream.Read(this._bufferBase, (int) (this._bufferOffset + this._streamPos), count) : 0;
                if (num2 == 0)
                {
                    this._posLimit = this._streamPos;
                    uint num3 = this._bufferOffset + this._posLimit;
                    if (num3 > this._pointerToLastSafePosition)
                    {
                        this._posLimit = this._pointerToLastSafePosition - this._bufferOffset;
                    }
                    this._streamEndWasReached = true;
                }
                else
                {
                    this._streamPos += (uint) num2;
                    if (this._streamPos >= (this._pos + this._keepSizeAfter))
                    {
                        this._posLimit = this._streamPos - this._keepSizeAfter;
                    }
                    goto Label_00F9;
                }
            }
        }

        public void ReduceOffsets(int subValue)
        {
            this._bufferOffset += (uint) subValue;
            this._posLimit -= (uint) subValue;
            this._pos -= (uint) subValue;
            this._streamPos -= (uint) subValue;
        }

        public void ReleaseStream()
        {
            this._stream = null;
        }

        public void SetStream(Stream stream)
        {
            this._stream = stream;
            if (this._streamEndWasReached)
            {
                this._streamEndWasReached = false;
                if (this.IsDataStarved)
                {
                    this.ReadBlock();
                }
            }
        }

        public bool IsDataStarved
        {
            get
            {
                return ((this._streamPos - this._pos) < this._keepSizeAfter);
            }
        }
    }
}

