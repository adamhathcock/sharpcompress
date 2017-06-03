
namespace SharpCompress.Compressor.PPMd.H
{
    internal class RarMemBlock : Pointer
    {
        public const int size = 12;

        private int stamp, NU;

        private int next, prev; // Pointer RarMemBlock

        public RarMemBlock(byte[] Memory)
            : base(Memory)
        {
        }

        internal int Stamp
        {
            get
            {
                if (Memory != null)
                {
                    stamp = Utility.readShortLittleEndian(Memory, Address) & 0xffff;
                }
                return stamp;
            }

            set
            {
                this.stamp = value;
                if (Memory != null)
                {
                    Utility.WriteLittleEndian(Memory, Address, (short)value);
                }
            }

        }

        internal void InsertAt(RarMemBlock p)
        {
            RarMemBlock temp = new RarMemBlock(Memory);
            SetPrev(p.Address);
            temp.Address = GetPrev();
            SetNext(temp.GetNext()); // prev.getNext();
            temp.SetNext(this); // prev.setNext(this);
            temp.Address = GetNext();
            temp.SetPrev(this); // next.setPrev(this);
        }

        internal void Remove()
        {
            RarMemBlock temp = new RarMemBlock(Memory);
            temp.Address = GetPrev();
            temp.SetNext(GetNext()); // prev.setNext(next);
            temp.Address = GetNext();
            temp.SetPrev(GetPrev()); // next.setPrev(prev);
            //		next = -1;
            //		prev = -1;
        }

        internal int GetNext()
        {
            if (Memory != null)
            {
                next = Utility.readIntLittleEndian(Memory, Address + 4);
            }
            return next;
        }

        internal void SetNext(RarMemBlock next)
        {
            SetNext(next.Address);
        }

        internal void SetNext(int next)
        {
            this.next = next;
            if (Memory != null)
            {
                Utility.WriteLittleEndian(Memory, Address + 4, next);
            }
        }

        internal int GetNU()
        {
            if (Memory != null)
            {
                NU = Utility.readShortLittleEndian(Memory, Address + 2) & 0xffff;
            }
            return NU;
        }

        internal void SetNU(int nu)
        {
            NU = nu & 0xffff;
            if (Memory != null)
            {
                Utility.WriteLittleEndian(Memory, Address + 2, (short)nu);
            }
        }

        internal int GetPrev()
        {
            if (Memory != null)
            {
                prev = Utility.readIntLittleEndian(Memory, Address + 8);
            }
            return prev;
        }

        internal void SetPrev(RarMemBlock prev)
        {
            SetPrev(prev.Address);
        }

        internal void SetPrev(int prev)
        {
            this.prev = prev;
            if (Memory != null)
            {
                Utility.WriteLittleEndian(Memory, Address + 8, prev);
            }
        }
    }
}