using System;
using System.Buffers.Binary;

namespace SharpCompress.Compressors.PPMd.H
{
    internal class RarMemBlock : Pointer
    {
        public const int SIZE = 12;

        private int _stamp, _nu;

        private int _next, _prev; // Pointer RarMemBlock

        public RarMemBlock(byte[] memory)
            : base(memory)
        {
        }

        internal int Stamp
        {
            get
            {
                if (Memory != null)
                {
                    _stamp = BinaryPrimitives.ReadInt16LittleEndian(Memory.AsSpan(Address)) & 0xffff;
                }
                return _stamp;
            }

            set
            {
                _stamp = value;
                if (Memory != null)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(Memory.AsSpan(Address), (short)value);
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
                _next = BinaryPrimitives.ReadInt32LittleEndian(Memory.AsSpan(Address + 4));
            }
            return _next;
        }

        internal void SetNext(RarMemBlock next)
        {
            SetNext(next.Address);
        }

        internal void SetNext(int next)
        {
            _next = next;
            if (Memory != null)
            {
                BinaryPrimitives.WriteInt32LittleEndian(Memory.AsSpan(Address + 4), next);
            }
        }

        internal int GetNu()
        {
            if (Memory != null)
            {
                _nu = BinaryPrimitives.ReadInt16LittleEndian(Memory.AsSpan(Address + 2)) & 0xffff;
            }
            return _nu;
        }

        internal void SetNu(int nu)
        {
            _nu = nu & 0xffff;
            if (Memory != null)
            {
                BinaryPrimitives.WriteInt16LittleEndian(Memory.AsSpan(Address + 2), (short)nu);
            }
        }

        internal int GetPrev()
        {
            if (Memory != null)
            {
                _prev = BinaryPrimitives.ReadInt32LittleEndian(Memory.AsSpan(Address + 8));
            }
            return _prev;
        }

        internal void SetPrev(RarMemBlock prev)
        {
            SetPrev(prev.Address);
        }

        internal void SetPrev(int prev)
        {
            _prev = prev;
            if (Memory != null)
            {
                BinaryPrimitives.WriteInt32LittleEndian(Memory.AsSpan(Address + 8), prev);
            }
        }
    }
}
