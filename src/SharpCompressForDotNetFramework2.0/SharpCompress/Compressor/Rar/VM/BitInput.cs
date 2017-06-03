namespace SharpCompress.Compressor.Rar.VM
{
    internal class BitInput
    {
        /// <summary> the max size of the input</summary>
        internal const int MAX_SIZE = 0x8000;
        protected int inAddr;
        protected int inBit;

        /// <summary>  </summary>
        internal BitInput()
        {
            InBuf = new byte[MAX_SIZE];
        }

        internal byte[] InBuf
        {
            get;
            private set;
        }

        internal void InitBitInput()
        {
            inAddr = 0;
            inBit = 0;
        }

        /// <summary>
        /// also named faddbits
        /// </summary>
        /// <param name="bits"></param>
        internal void AddBits(int bits)
        {
            bits += inBit;
            inAddr += (bits >> 3);
            inBit = bits & 7;
        }

        /// <summary> 
        /// (also named fgetbits)
        /// </summary>
        /// <returns>
        /// the bits (unsigned short) 
        /// </returns>
        internal int GetBits()
        {
            //      int BitField=0;
            //      BitField|=(int)(inBuf[inAddr] << 16)&0xFF0000;
            //      BitField|=(int)(inBuf[inAddr+1] << 8)&0xff00;
            //      BitField|=(int)(inBuf[inAddr+2])&0xFF;
            //      BitField >>>= (8-inBit);
            //      return (BitField & 0xffff);
            return ((Utility.URShift((((InBuf[inAddr] & 0xff) << 16)
                + ((InBuf[inAddr + 1] & 0xff) << 8)
                + ((InBuf[inAddr + 2] & 0xff))), (8 - inBit))) & 0xffff);
        }

        /// <summary> Indicates an Overfow</summary>
        /// <param name="IncPtr">how many bytes to inc
        /// </param>
        /// <returns> true if an Oververflow would occur
        /// </returns>
        internal bool Overflow(int IncPtr)
        {
            return (inAddr + IncPtr >= MAX_SIZE);
        }
    }
}