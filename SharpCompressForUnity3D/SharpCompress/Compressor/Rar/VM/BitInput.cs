namespace SharpCompress.Compressor.Rar.VM
{
    using SharpCompress;
    using System;
    using System.Runtime.CompilerServices;

    internal class BitInput
    {
        [CompilerGenerated]
        private byte[] _InBuf_k__BackingField;
        protected int inAddr;
        protected int inBit;
        internal const int MAX_SIZE = 0x8000;

        internal BitInput()
        {
            this.InBuf = new byte[0x8000];
        }

        internal void AddBits(int bits)
        {
            bits += this.inBit;
            this.inAddr += bits >> 3;
            this.inBit = bits & 7;
        }

        internal int GetBits()
        {
            return (Utility.URShift((int) ((((this.InBuf[this.inAddr] & 0xff) << 0x10) + ((this.InBuf[this.inAddr + 1] & 0xff) << 8)) + (this.InBuf[this.inAddr + 2] & 0xff)), (int) (8 - this.inBit)) & 0xffff);
        }

        internal void InitBitInput()
        {
            this.inAddr = 0;
            this.inBit = 0;
        }

        internal bool Overflow(int IncPtr)
        {
            return ((this.inAddr + IncPtr) >= 0x8000);
        }

        internal byte[] InBuf
        {
            [CompilerGenerated]
            get
            {
                return this._InBuf_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._InBuf_k__BackingField = value;
            }
        }
    }
}

