namespace SharpCompress.Compressor.PPMd.H
{
    using SharpCompress;
    using System;
    using System.Text;

    internal class RarNode : Pointer
    {
        private int next;
        public const int size = 4;

        public RarNode(byte[] Memory) : base(Memory)
        {
        }

        internal int GetNext()
        {
            if (base.Memory != null)
            {
                this.next = Utility.readIntLittleEndian(base.Memory, this.Address);
            }
            return this.next;
        }

        internal void SetNext(RarNode next)
        {
            this.SetNext(next.Address);
        }

        internal void SetNext(int next)
        {
            this.next = next;
            if (base.Memory != null)
            {
                Utility.WriteLittleEndian(base.Memory, this.Address, next);
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("State[");
            builder.Append("\n  Address=");
            builder.Append(this.Address);
            builder.Append("\n  size=");
            builder.Append(4);
            builder.Append("\n  next=");
            builder.Append(this.GetNext());
            builder.Append("\n]");
            return builder.ToString();
        }
    }
}

