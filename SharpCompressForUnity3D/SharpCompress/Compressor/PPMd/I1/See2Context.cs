namespace SharpCompress.Compressor.PPMd.I1
{
    using System;

    internal class See2Context
    {
        public byte Count;
        private const byte PeriodBitCount = 7;
        public byte Shift;
        public ushort Summary;

        public void Initialize(uint initialValue)
        {
            this.Shift = 3;
            this.Summary = (ushort) (initialValue << this.Shift);
            this.Count = 7;
        }

        public uint Mean()
        {
            uint num = (uint) (this.Summary >> this.Shift);
            this.Summary = (ushort) (this.Summary - num);
            return (num + ((num == 0) ? 1 : 0));
        }

        public void Update()
        {
            if ((this.Shift < 7) && ((this.Count = (byte) (this.Count - 1)) == 0))
            {
                byte num;
                this.Summary = (ushort) (this.Summary + this.Summary);
                this.Shift = (byte) ((num = this.Shift) + 1);
                this.Count = (byte) (((int) 3) << num);
            }
        }
    }
}

