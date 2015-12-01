namespace SharpCompress.Compressor.PPMd.H
{
    using SharpCompress;
    using System;

    internal class RarMemBlock : Pointer
    {
        private int next;
        private int NU;
        private int prev;
        public const int size = 12;
        private int stamp;

        public RarMemBlock(byte[] Memory) : base(Memory)
        {
        }

        internal int GetNext()
        {
            if (base.Memory != null)
            {
                this.next = Utility.readIntLittleEndian(base.Memory, this.Address + 4);
            }
            return this.next;
        }

        internal int GetNU()
        {
            if (base.Memory != null)
            {
                this.NU = Utility.readShortLittleEndian(base.Memory, this.Address + 2) & 0xffff;
            }
            return this.NU;
        }

        internal int GetPrev()
        {
            if (base.Memory != null)
            {
                this.prev = Utility.readIntLittleEndian(base.Memory, this.Address + 8);
            }
            return this.prev;
        }

        internal void InsertAt(RarMemBlock p)
        {
            RarMemBlock block = new RarMemBlock(base.Memory);
            this.SetPrev(p.Address);
            block.Address = this.GetPrev();
            this.SetNext(block.GetNext());
            block.SetNext(this);
            block.Address = this.GetNext();
            block.SetPrev(this);
        }

        internal void Remove()
        {
            RarMemBlock block = new RarMemBlock(base.Memory);
            block.Address = this.GetPrev();
            block.SetNext(this.GetNext());
            block.Address = this.GetNext();
            block.SetPrev(this.GetPrev());
        }

        internal void SetNext(RarMemBlock next)
        {
            this.SetNext(next.Address);
        }

        internal void SetNext(int next)
        {
            this.next = next;
            if (base.Memory != null)
            {
                Utility.WriteLittleEndian(base.Memory, this.Address + 4, next);
            }
        }

        internal void SetNU(int nu)
        {
            this.NU = nu & 0xffff;
            if (base.Memory != null)
            {
                Utility.WriteLittleEndian(base.Memory, this.Address + 2, (short) nu);
            }
        }

        internal void SetPrev(RarMemBlock prev)
        {
            this.SetPrev(prev.Address);
        }

        internal void SetPrev(int prev)
        {
            this.prev = prev;
            if (base.Memory != null)
            {
                Utility.WriteLittleEndian(base.Memory, this.Address + 8, prev);
            }
        }

        internal int Stamp
        {
            get
            {
                if (base.Memory != null)
                {
                    this.stamp = Utility.readShortLittleEndian(base.Memory, this.Address) & 0xffff;
                }
                return this.stamp;
            }
            set
            {
                this.stamp = value;
                if (base.Memory != null)
                {
                    Utility.WriteLittleEndian(base.Memory, this.Address, (short) value);
                }
            }
        }
    }
}

