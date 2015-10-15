namespace SharpCompress.Compressor.LZMA
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;

    internal class BitVector
    {
        private uint[] mBits;
        private int mLength;

        public BitVector(List<bool> bits) : this(bits.Count)
        {
            for (int i = 0; i < bits.Count; i++)
            {
                if (bits[i])
                {
                    this.SetBit(i);
                }
            }
        }

        public BitVector(int length)
        {
            this.mLength = length;
            this.mBits = new uint[(length + 0x1f) >> 5];
        }

        public BitVector(int length, bool initValue)
        {
            this.mLength = length;
            this.mBits = new uint[(length + 0x1f) >> 5];
            if (initValue)
            {
                for (int i = 0; i < this.mBits.Length; i++)
                {
                    this.mBits[i] = uint.MaxValue;
                }
            }
        }

        internal bool GetAndSet(int index)
        {
            if ((index < 0) || (index >= this.mLength))
            {
                throw new ArgumentOutOfRangeException("index");
            }
            uint num = this.mBits[index >> 5];
            uint num2 = ((uint) 1) << index;
            this.mBits[index >> 5] |= num2;
            return ((num & num2) != 0);
        }

        public void SetBit(int index)
        {
            if ((index < 0) || (index >= this.mLength))
            {
                throw new ArgumentOutOfRangeException("index");
            }
            this.mBits[index >> 5] |= ((uint) 1) << index;
        }

        public bool[] ToArray()
        {
            bool[] flagArray = new bool[this.mLength];
            for (int i = 0; i < flagArray.Length; i++)
            {
                flagArray[i] = this[i];
            }
            return flagArray;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder(this.mLength);
            for (int i = 0; i < this.mLength; i++)
            {
                builder.Append(this[i] ? 'x' : '.');
            }
            return builder.ToString();
        }

        public bool this[int index]
        {
            get
            {
                if ((index < 0) || (index >= this.mLength))
                {
                    throw new ArgumentOutOfRangeException("index");
                }
                return ((this.mBits[index >> 5] & (((int) 1) << index)) != 0);
            }
        }

        public int Length
        {
            get
            {
                return this.mLength;
            }
        }
    }
}

